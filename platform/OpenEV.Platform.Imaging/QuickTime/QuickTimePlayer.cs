namespace OpenEV.Platform.Imaging.QuickTime;

// Plays the video tracks of a flattened QuickTime movie: sequential decode of due
// samples against a caller-supplied clock, one decoder per track (segmented movies
// chain several video tracks with edit-list delays). Audio tracks ('musi'/'Qclp'/
// 'QDMC'…) are NOT decoded — surfaced in SkippedTracks for the caller to log;
// playback is silent. Frames are cloned into CurrentFrame so deferred draw-queue
// blits never see a half-decoded buffer.
public sealed class QuickTimePlayer
{
    public int Width { get; }
    public int Height { get; }
    public double DurationMs { get; }
    public string VideoFourCC { get; }
    public bool HasVideo => _schedule.Length > 0;
    public Rgba8Image? CurrentFrame { get; private set; }
    public IReadOnlyList<string> SkippedTracks => _movie.SkippedTracks;

    private readonly QuickTimeMovieFile _movie;
    private readonly byte[] _data;
    private readonly (int track, QuickTimeMovieFile.QtVideoSample sample)[] _schedule;
    private readonly object?[] _decoders;
    private int _next;

    private QuickTimePlayer(QuickTimeMovieFile movie, byte[] data)
    {
        _movie = movie;
        _data = data;
        foreach (var t in movie.VideoTracks)
        {
            Width = Math.Max(Width, t.Width);
            Height = Math.Max(Height, t.Height);
        }
        DurationMs = movie.DurationMs;
        VideoFourCC = string.Join("+", movie.VideoTracks.Select(t => t.FourCC).Distinct());
        _schedule = movie.VideoTracks
            .SelectMany((t, i) => t.Samples.Select(s => (i, s)))
            .OrderBy(x => x.s.StartMs)
            .ToArray();
        _decoders = new object?[movie.VideoTracks.Count];
    }

    public static QuickTimePlayer? TryOpen(byte[] data)
    {
        var movie = QuickTimeMovieFile.TryParse(data);
        return movie is null ? null : new QuickTimePlayer(movie, data);
    }

    public void Rewind()
    {
        _next = 0;
        Array.Clear(_decoders, 0, _decoders.Length);
        CurrentFrame = null;
    }

    /// Decode every sample due at `ms`; true when CurrentFrame changed.
    public bool AdvanceTo(double ms)
    {
        bool changed = false;
        while (_next < _schedule.Length && _schedule[_next].sample.StartMs <= ms)
        {
            var (trackIdx, s) = _schedule[_next++];
            if (s.Offset < 0 || s.Size <= 0 || s.Offset + s.Size > _data.Length) continue;
            var track = _movie.VideoTracks[trackIdx];
            var payload = new ReadOnlySpan<byte>(_data, s.Offset, s.Size);
            Rgba8Image? frame = track.FourCC switch
            {
                "rpza" => ((RpzaDecoder)(_decoders[trackIdx] ??= new RpzaDecoder(track.Width, track.Height)))
                    .DecodeFrame(payload),
                "jpeg" => JpegBaselineDecoder.Decode(payload),
                "SVQ1" => ((Svq1Decoder)(_decoders[trackIdx] ??= new Svq1Decoder(track.Width, track.Height)))
                    .DecodeFrame(payload),
                _ => null,
            };
            if (frame is not null)
            {
                CurrentFrame = Clone(frame);
                changed = true;
            }
        }
        return changed;
    }

    public bool Done(double ms) =>
        !HasVideo || (ms >= DurationMs && _next >= _schedule.Length);

    private static Rgba8Image Clone(Rgba8Image src)
    {
        var copy = new Rgba8Image(src.Width, src.Height);
        Array.Copy(src.Pixels, copy.Pixels, src.Pixels.Length);
        return copy;
    }
}
