using System;
using System.Collections.Generic;
using OpenEV.Platform.EvoData;
using OpenEV.Platform.Toolbox;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;   // Silk.NET.SDL also exports a Thread

namespace OpenEV.Override.Game;

// The game's host-side audio engine — pure-C# software mixer on an SDL2 audio device.
// The Mac Sound Manager (SndNewChannel / SndPlayDoubleBuffer / the game's own
// software mixer) isn't portable 1:1, so — exactly like the QuickDraw→Canvas
// bridge — the MacToolbox sound traps forward here (installed as delegates by
// TitleAdapter).
//
// Decoding reuses the proven MacSnd decoder (fmt-1/2 stdSH + extSH, 8/16-bit)
// and OverrideGameData.Snds, both referenced read-only. PCM is fed to SDL via
// the push model (SDL_QueueAudio): the device opens with a NULL callback and a
// background mixer thread sums active voices into the queue — no unmanaged
// audio callback to marshal.
//
// Three playback roles, mirroring the original audio paths:
//   • SFX one-shot  — SndPlay (FUN_10060288): title-menu clicks (snd 600),
//                     row-reveal chimes (601/602), alerts.
//   • File music    — SndStartFilePlay (FUN_1004227c): the title music stream
//                     (snd 30000), plays once, stopped when About opens.
//   • Pair music    — LoadAndStartSoundPair (FUN_100423f4): the About-EVÉ
//                     credits music (30001+30002), looping, stopped on exit.
internal sealed unsafe class SoundEngine : IDisposable
{
    private readonly OverrideGameData _data;

    private sealed class Decoded { public short[] Samples = Array.Empty<short>(); public int Rate; public int Channels; }
    private sealed class Voice
    {
        public short[] Samples = Array.Empty<short>();
        public int SrcChannels;
        public int Frames;          // Samples.Length / SrcChannels
        public double Pos;          // source-frame cursor
        public double Step;         // srcRate / deviceRate
        public bool Loop;
        public float Vol;
        public bool Active = true;
    }

    private readonly Dictionary<int, Decoded?> _cache = new();
    private readonly Dictionary<long, Decoded?> _pairCache = new();
    private readonly Dictionary<int, Voice> _sfxLive = new();
    private Voice? _fileMusic; private int _fileMusicId = -1;
    private Voice? _pairMusic; private int _pairMusicId = -1;

    private readonly List<Voice> _voices = new();
    private readonly object _lock = new();

    private readonly Sdl _sdl;
    private uint _dev;
    private int _deviceRate = 44100;
    private int _deviceChannels = 2;
    private Thread? _mixThread;
    private volatile bool _running;

    public float MasterVolume { get; set; } = 1.0f;

    public SoundEngine(OverrideGameData data)
    {
        _data = data;
        _sdl = Sdl.GetApi();
        try { OpenDevice(); }
        catch (Exception ex) { Console.WriteLine($"[SoundEngine] audio init failed: {ex.Message}"); }
    }

    private void OpenDevice()
    {
        // The host inits SDL with InitAudio; ensure the subsystem is up either way.
        _sdl.InitSubSystem(Sdl.InitAudio);
        var want = new AudioSpec
        {
            Freq = 44100,
            Format = Sdl.AudioS16Lsb,
            Channels = 2,
            Samples = 1024,
        };
        AudioSpec have;
        _dev = _sdl.OpenAudioDevice((byte*)null, 0, &want, &have, 0);
        if (_dev == 0)
        {
            Console.WriteLine("[SoundEngine] OpenAudioDevice failed; sound disabled.");
            return;
        }
        _deviceRate = have.Freq > 0 ? have.Freq : 44100;
        _deviceChannels = have.Channels > 0 ? have.Channels : 2;
        _sdl.PauseAudioDevice(_dev, 0);   // start playback
        _running = true;
        _mixThread = new Thread(MixLoop) { IsBackground = true, Name = "EVO-AudioMixer" };
        _mixThread.Start();
    }

    private const int ChunkFrames = 1024;
    private void MixLoop()
    {
        int chunkBytes = ChunkFrames * _deviceChannels * 2;     // s16
        uint targetBytes = (uint)(chunkBytes * 4);              // ~4 chunks queued (~90 ms)
        var outBuf = new byte[chunkBytes];
        var acc = new int[ChunkFrames * _deviceChannels];
        while (_running)
        {
            while (_running && _sdl.GetQueuedAudioSize(_dev) < targetBytes)
            {
                MixChunk(acc, outBuf);
                fixed (byte* p = outBuf) _sdl.QueueAudio(_dev, p, (uint)outBuf.Length);
            }
            _sdl.Delay(5);
        }
    }

    private void MixChunk(int[] acc, byte[] outBuf)
    {
        Array.Clear(acc, 0, acc.Length);
        float master = MasterVolume;
        lock (_lock)
        {
            foreach (var v in _voices)
            {
                if (!v.Active) continue;
                float g = v.Vol * master;
                for (int f = 0; f < ChunkFrames; f++)
                {
                    int srcFrame = (int)v.Pos;
                    if (srcFrame >= v.Frames)
                    {
                        if (v.Loop && v.Frames > 0) { v.Pos -= v.Frames; srcFrame = (int)v.Pos; }
                        else { v.Active = false; break; }
                    }
                    int sl, sr;
                    if (v.SrcChannels == 1)
                    {
                        sl = sr = v.Samples[srcFrame];
                    }
                    else
                    {
                        int bi = srcFrame * v.SrcChannels;
                        sl = v.Samples[bi];
                        sr = v.SrcChannels > 1 ? v.Samples[bi + 1] : sl;
                    }
                    acc[f * _deviceChannels] += (int)(sl * g);
                    if (_deviceChannels > 1) acc[f * _deviceChannels + 1] += (int)(sr * g);
                    v.Pos += v.Step;
                }
            }
            _voices.RemoveAll(v => !v.Active);
        }
        for (int i = 0; i < acc.Length; i++)
        {
            int s = acc[i];
            if (s > short.MaxValue) s = short.MaxValue; else if (s < short.MinValue) s = short.MinValue;
            outBuf[i * 2] = (byte)(s & 0xff);
            outBuf[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
    }

    private Decoded? Get(int sndId)
    {
        if (_cache.TryGetValue(sndId, out var d)) return d;
        Decoded? result = null;
        if (_data.Snds.TryGetValue(sndId, out var bytes))
        {
            var dec = MacSnd.Decode(bytes);
            if (dec is not null) result = ToDecoded(dec.Pcm16LE, dec.SampleRate, dec.Channels);
        }
        _cache[sndId] = result;
        return result;
    }

    private static Decoded ToDecoded(byte[] pcm16le, int rate, int channels)
    {
        var samples = new short[pcm16le.Length / 2];
        Buffer.BlockCopy(pcm16le, 0, samples, 0, samples.Length * 2);   // LE host
        return new Decoded { Samples = samples, Rate = rate, Channels = channels < 1 ? 1 : channels };
    }

    private Voice MakeVoice(Decoded d, float vol, bool loop)
        => new Voice
        {
            Samples = d.Samples,
            SrcChannels = d.Channels,
            Frames = d.Channels > 0 ? d.Samples.Length / d.Channels : 0,
            Pos = 0,
            Step = d.Rate / (double)_deviceRate,
            Loop = loop,
            Vol = vol,
        };

    public void PlaySfx(int sndId, float volume)
    {
        if (_dev == 0) return;
        var d = Get(sndId);
        if (d is null) return;
        lock (_lock)
        {
            // No per-sndId dedup: the faithful 16-slot mixer (FUN_10074f10) lets the SAME
            // snd overlap (rapid weapon fire, multiple same-type explosions). Killing the
            // prior voice here clipped those. Voices self-retire in MixChunk when finished.
            var v = MakeVoice(d, Math.Clamp(volume, 0f, 1f), loop: false);
            _sfxLive[sndId] = v;   // tracks the latest instance for StopSfx(sndId)
            _voices.Add(v);
        }
    }

    public void StopSfx(int sndId)
    {
        lock (_lock)
        {
            if (_sfxLive.TryGetValue(sndId, out var v)) { v.Active = false; _sfxLive.Remove(sndId); }
        }
    }

    // The Mac SysBeep trap plays the System file's alert sound — an Apple asset
    // the game doesn't ship, so the host synthesizes a simple beep (short 880 Hz
    // tone, fast attack, exponential decay — the classic "Simple Beep" character)
    // and mixes it like any voice: MasterVolume scales it, matching how the Mac
    // speaker volume (which the game sets from the prefs) scaled the real beep.
    private Decoded? _beep;
    public void PlayBeep()
    {
        if (_dev == 0) return;
        _beep ??= SynthesizeBeep();
        lock (_lock) { _voices.Add(MakeVoice(_beep, 1f, loop: false)); }
    }

    private Decoded SynthesizeBeep()
    {
        int rate = _deviceRate;
        var samples = new short[rate * 3 / 10];   // 0.3 s
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate;
            double env = Math.Min(1.0, t / 0.005) * Math.Exp(-t / 0.09);
            // Band-limited square-ish timbre: fundamental + 3rd + 5th harmonic.
            double s = Math.Sin(2 * Math.PI * 880 * t)
                     + Math.Sin(2 * Math.PI * 2640 * t) / 3
                     + Math.Sin(2 * Math.PI * 4400 * t) / 5;
            samples[i] = (short)(s / 1.54 * env * 16000);
        }
        return new Decoded { Samples = samples, Rate = rate, Channels = 1 };
    }

    public void StopAllSfx()
    {
        lock (_lock)
        {
            foreach (var v in _sfxLive.Values) v.Active = false;
            _sfxLive.Clear();
        }
    }

    // Raw interleaved-s16 one-shot (the QuickTime movie voice track). Returns an
    // opaque token StopRawPcm accepts; null when the device is down.
    public object? PlayRawPcm(short[] samples, int rate, int channels, float volume = 1f)
    {
        if (_dev == 0 || samples.Length == 0 || rate <= 0) return null;
        var d = new Decoded { Samples = samples, Rate = rate, Channels = channels < 1 ? 1 : channels };
        var v = MakeVoice(d, Math.Clamp(volume, 0f, 1f), loop: false);
        lock (_lock) { _voices.Add(v); }
        return v;
    }

    public void StopRawPcm(object? token)
    {
        if (token is Voice v) lock (_lock) { v.Active = false; }
    }

    public void StartFileMusic(int sndId)
    {
        if (_dev == 0) return;
        var d = Get(sndId);
        if (d is null) return;
        lock (_lock)
        {
            if (_fileMusicId == sndId && _fileMusic is { Active: true }) return;
            if (_fileMusic is not null) _fileMusic.Active = false;
            // Title stream plays ONCE (the Mac SndStartFilePlay has no loop flag).
            _fileMusic = MakeVoice(d, 0.4f, loop: false);
            _fileMusicId = sndId;
            _voices.Add(_fileMusic);
        }
    }

    public void StopFileMusic()
    {
        lock (_lock) { if (_fileMusic is not null) _fileMusic.Active = false; _fileMusic = null; _fileMusicId = -1; }
    }

    public void StartPairMusic(int primaryId, int secondaryId)
    {
        if (_dev == 0) return;
        long key = ((long)(uint)primaryId << 32) | (uint)secondaryId;
        var spliced = GetSpliced(key, primaryId, secondaryId);
        var d = spliced ?? Get(primaryId);
        if (d is null) return;
        lock (_lock)
        {
            if (_pairMusicId == primaryId && _pairMusic is { Active: true }) return;
            if (_pairMusic is not null) _pairMusic.Active = false;
            _pairMusic = MakeVoice(d, 0.4f, loop: true);
            _pairMusicId = primaryId;
            _voices.Add(_pairMusic);
        }
    }

    public void StopPairMusic()
    {
        lock (_lock) { if (_pairMusic is not null) _pairMusic.Active = false; _pairMusic = null; _pairMusicId = -1; }
    }

    // Splice primary+secondary into one buffer (both tracks in order, then loop)
    // when they share rate+channels — faithful to the Mac mixer playing them
    // back-to-back. Falls back to primary alone if formats differ / missing.
    private Decoded? GetSpliced(long key, int primaryId, int secondaryId)
    {
        if (_pairCache.TryGetValue(key, out var cached)) return cached;
        Decoded? result = null;
        var a = _data.Snds.TryGetValue(primaryId, out var ab) ? MacSnd.Decode(ab) : null;
        var b = _data.Snds.TryGetValue(secondaryId, out var bb) ? MacSnd.Decode(bb) : null;
        if (a is not null && b is not null && a.SampleRate == b.SampleRate && a.Channels == b.Channels)
        {
            var pcm = new byte[a.Pcm16LE.Length + b.Pcm16LE.Length];
            Buffer.BlockCopy(a.Pcm16LE, 0, pcm, 0, a.Pcm16LE.Length);
            Buffer.BlockCopy(b.Pcm16LE, 0, pcm, a.Pcm16LE.Length, b.Pcm16LE.Length);
            result = ToDecoded(pcm, a.SampleRate, a.Channels);
        }
        _pairCache[key] = result;
        return result;
    }

    public void Dispose()
    {
        _running = false;
        try { _mixThread?.Join(200); } catch { }
        if (_dev != 0) { try { _sdl.CloseAudioDevice(_dev); } catch { } _dev = 0; }
    }
}
