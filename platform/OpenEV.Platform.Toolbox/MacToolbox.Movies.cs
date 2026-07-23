using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;
using OpenEV.Platform.Imaging.QuickTime;

namespace OpenEV.Platform.Toolbox;

// QuickTime Movie Toolbox traps, backed by the managed movie player
// (OpenEV.Platform.Imaging.QuickTime): flattened .mov container + rpza/jpeg/SVQ1
// video. Audio tracks ('musi' QT-Music, 'Qclp', 'QDMC' …) are NOT decoded — playback
// is silent; the skipped fourccs are logged once per movie. With no host resolver
// installed EnterMovies reports -1 and every movie path degrades exactly like a Mac
// without QuickTime — the pre-implementation behaviour.
public static partial class MacToolbox
{
    /// Host hook: movie file name → file bytes (the game passes names from 'dëqt'
    /// records; the original resolves them in the EV Plug-Ins folder).
    public static Func<string, byte[]?>? MovieFileResolver;

    /// Optional host audio hooks: decode a movie file's voice track to interleaved
    /// s16 PCM (ffmpeg-backed where natives exist; null = stay silent), start it as
    /// a mixer voice (returns an opaque token), stop that voice.
    public static Func<byte[], MovieAudioTrack?>? MovieAudioDecoder;
    public static Func<short[], int, int, object?>? MovieAudioPlayer;
    public static Action<object?>? MovieAudioStopper;

    public sealed record MovieAudioTrack(short[] Pcm, int Rate, int Channels, string FourCC);

    private sealed class MovieInstance
    {
        public QuickTimePlayer? Player;
        public string Name = "";
        public readonly short[] Box = new short[4];   // last SetMovieBox rect (window-local after rebase)
        public int TargetPort;
        public long StartTick;
        public bool Started;
        public MovieAudioTrack? Audio;
        public object? AudioVoice;
    }

    private const int MovieHandleBase = 0x4D760000;   // 'Mv' band — only movie traps consume these
    private static readonly Dictionary<int, MovieInstance> _movies = new();
    private static readonly Dictionary<short, byte[]> _movieFiles = new();
    private static int _movieHandleNext = MovieHandleBase;
    private static short _movieFileRefNext = 1;

    public static short EnterMovies() => MovieFileResolver is null ? (short)-1 : (short)0;
    public static void ExitMovies() { }

    public static short OpenMovieFile(string fileName, out short refNum)
    {
        refNum = 0;
        byte[]? bytes = MovieFileResolver?.Invoke(fileName);
        if (bytes is null) return -43;   // fnfErr
        refNum = _movieFileRefNext++;
        _movieFiles[refNum] = bytes;
        return 0;
    }

    public static void CloseMovieFile(short refNum) => _movieFiles.Remove(refNum);

    public static void NewMovieFromFile(out int movie, short refNum, string fileName)
    {
        movie = 0;
        if (!_movieFiles.TryGetValue(refNum, out byte[]? bytes)) return;
        var player = QuickTimePlayer.TryOpen(bytes);
        movie = _movieHandleNext += 4;
        var inst = new MovieInstance { Player = player, Name = fileName };
        _movies[movie] = inst;
        if (player is null)
        {
            Console.WriteLine($"[QT] '{fileName}': unparseable movie — skipped");
            return;
        }
        if (player.HasVideo) inst.Audio = TryDecodeAudio(bytes);
        var silent = new List<string>(player.SkippedTracks);
        foreach (var t in player.AudioTracks)
            if (inst.Audio is null || inst.Audio.FourCC != t.FourCC) silent.Add(t.FourCC);
        string audio =
            (inst.Audio is not null ? $", audio={inst.Audio.FourCC}" : "") +
            (silent.Count > 0 ? $", silent tracks ({string.Join(", ", silent)})" : "");
        Console.WriteLine(player.HasVideo
            ? $"[QT] '{fileName}': video={player.VideoFourCC} {player.Width}x{player.Height}, " +
              $"{player.DurationMs / 1000.0:0.0}s{audio}"
            : $"[QT] '{fileName}': audio-only movie — skipped{audio}");
    }

    /// GetMovieBox — the movie's natural box {0, 0, height, width}.
    public static void GetMovieBox(int movie, short[] rect)
    {
        Array.Clear(rect, 0, 4);
        if (_movies.TryGetValue(movie, out var m) && m.Player is not null)
        {
            rect[2] = (short)m.Player.Height;
            rect[3] = (short)m.Player.Width;
        }
    }

    public static void SetMovieBox(int movie, short[] rect)
    {
        if (_movies.TryGetValue(movie, out var m)) Array.Copy(rect, m.Box, 4);
    }

    public static void SetMovieGWorld(int movie, int port, int device)
    {
        if (_movies.TryGetValue(movie, out var m)) m.TargetPort = port;
    }

    public static void GoToBeginningOfMovie(int movie)
    {
        if (_movies.TryGetValue(movie, out var m)) m.Player?.Rewind();
    }

    public static void SetMovieRate(int movie, int rateFixed) { }   // always played at 1.0

    private static MovieAudioTrack? TryDecodeAudio(byte[] movieBytes)
    {
        try { return MovieAudioDecoder?.Invoke(movieBytes); }
        catch (Exception ex) { Console.WriteLine($"[QT] audio decode failed: {ex.Message}"); return null; }
    }

    public static void StartMovie(int movie)
    {
        if (!_movies.TryGetValue(movie, out var m)) return;
        m.StartTick = Environment.TickCount64;
        m.Started = true;
        if (m.Audio is not null && m.AudioVoice is null)
            m.AudioVoice = MovieAudioPlayer?.Invoke(m.Audio.Pcm, m.Audio.Rate, m.Audio.Channels);
    }

    public static bool IsMovieDone(int movie)
    {
        if (!_movies.TryGetValue(movie, out var m) || m.Player is null) return true;
        double ms = m.Started ? Environment.TickCount64 - m.StartTick : 0;
        return m.Player.Done(ms);
    }

    /// Decode any due frame and blit it into the movie window's compositor layer.
    /// The Mac trap cooperatively yields inside MoviesTask; the sleep below is the
    /// host equivalent so the Button()-poll loop doesn't spin a core.
    public static void MoviesTask(int movie, int maxMillisecs)
    {
        if (!_movies.TryGetValue(movie, out var m) || m.Player is null) return;
        double ms = m.Started ? Environment.TickCount64 - m.StartTick : 0;
        bool newFrame = m.Player.AdvanceTo(ms);
        var frame = m.Player.CurrentFrame;
        if (newFrame && frame is not null && m.TargetPort != 0)
        {
            short[] origin = GetPortRectShorts(m.TargetPort);
            var dst = new RectI(origin[1] + m.Box[1], origin[0] + m.Box[0],
                                m.Box[3] - m.Box[1], m.Box[2] - m.Box[0]);
            EnqueueDrawTo(m.TargetPort + 2, c => c.Blit(frame, dst, RgbaColor.White));
        }
        else
        {
            System.Threading.Thread.Sleep(4);
        }
    }

    public static void DisposeMovie(int movie)
    {
        if (_movies.TryGetValue(movie, out var m) && m.AudioVoice is not null)
            MovieAudioStopper?.Invoke(m.AudioVoice);
        _movies.Remove(movie);
    }

    /// The movie window: NewCWindow(0, &bounds, title, 0, plainDBox, 0, goAway, -1)
    /// in the original — here a bare window record on the dialog compositor stack
    /// (screen-sized layer buffer at handle+2, created hidden; ShowWindow reveals,
    /// CloseWindow disposes). Item-less, so nothing but the movie body draws in it.
    public static int NewMovieWindow(short[] boundsRect)
    {
        int screenW = _mainScreenWidth > 0 ? _mainScreenWidth : 800;
        int screenH = _mainScreenHeight > 0 ? _mainScreenHeight : 600;
        int handle = DlgAlloc(64);
        var rec = new DlgRecord
        {
            Handle = handle, DlogId = 0, ProcId = 2,   // plainDBox
            WinTop = boundsRect[0], WinLeft = boundsRect[1],
            WinBottom = boundsRect[2], WinRight = boundsRect[3],
            Visible = false,
            BufferKey = handle + 2,
            Buffer = new Rgba8Image(screenW, screenH),
        };
        RegisterRenderTarget(rec.BufferKey, rec.Buffer);
        _dialogs[handle] = rec;
        _dialogStack.Push(rec);
        RebuildWindowLayers();
        // A movie can start from inside the game tick's draw batch (mission encounter
        // in flight) — suspend it for the window's lifetime like every dialog open,
        // or nothing enqueued while the movie plays would ever drain.
        rec.SavedBatchDepth = SuspendDrawBatchForModal();
        return handle;
    }
}
