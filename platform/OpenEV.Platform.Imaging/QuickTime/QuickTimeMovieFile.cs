using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OpenEV.Platform.Imaging.QuickTime;

// Parsed view of a flattened (single-fork) QuickTime movie file: every video
// track's sample tables resolved to flat, time-ordered sample lists (with the
// edit-list start delay applied), plus the movie duration. Compressed movie
// resources ('cmov' + zlib 'cmvd') are decompressed transparently. Only what the
// host movie player needs — no partial edits, no audio decode (audio track
// fourccs are surfaced so the player can log what it skipped).
public sealed class QuickTimeMovieFile
{
    public double DurationMs;
    public readonly List<QtVideoTrack> VideoTracks = new();
    public readonly List<QtAudioTrack> AudioTracks = new();
    public readonly List<string> SkippedTracks = new();   // sample-desc fourccs of non-video tracks

    public sealed class QtVideoTrack
    {
        public string FourCC = "";
        public int Width, Height;
        public readonly List<QtVideoSample> Samples = new();
    }

    // A sound track's compressed packets + the codec parameters a decoder needs.
    // Extradata is the sound-description 'wave' extension atom's content (or the raw
    // description tail when there is no 'wave') — what QDMC-style decoders parse.
    public sealed class QtAudioTrack
    {
        public string FourCC = "";
        public int Channels = 1;
        public int SampleRate;
        public byte[] Extradata = Array.Empty<byte>();
        public double OffsetMs;
        public readonly List<QtVideoSample> Packets = new();   // same (Offset, Size, StartMs) shape
    }

    public sealed class QtVideoSample
    {
        public int Offset;      // absolute offset in the ORIGINAL file (stco is file-absolute)
        public int Size;
        public double StartMs;  // media time + track edit-list delay → ms
    }

    public static QuickTimeMovieFile? TryParse(byte[] file)
    {
        int moov = FindAtom(file, 0, file.Length, "moov");
        if (moov < 0) return null;

        // Compressed movie resource: moov { cmov { dcom('zlib'), cmvd(size + deflate) } }.
        byte[] d = file;
        int moovEnd = moov + AtomSize(file, moov);
        int cmov = FindAtom(file, moov + 8, moovEnd, "cmov");
        if (cmov >= 0)
        {
            byte[]? unpacked = DecompressMoov(file, cmov);
            if (unpacked is null) return null;
            d = unpacked;
            moov = FindAtom(d, 0, d.Length, "moov");
            if (moov < 0) return null;
            moovEnd = moov + AtomSize(d, moov);
        }

        var m = new QuickTimeMovieFile();
        uint movieScale = 600;
        int mvhd = FindAtom(d, moov + 8, moovEnd, "mvhd");
        if (mvhd >= 0)
        {
            movieScale = U32(d, mvhd + 8 + 12);
            uint duration = U32(d, mvhd + 8 + 16);
            if (movieScale > 0) m.DurationMs = duration * 1000.0 / movieScale;
        }

        for (int trak = FindAtom(d, moov + 8, moovEnd, "trak"); trak >= 0;
             trak = FindAtom(d, trak + AtomSize(d, trak), moovEnd, "trak"))
        {
            int trakEnd = trak + AtomSize(d, trak);
            int mdia = FindAtom(d, trak + 8, trakEnd, "mdia");
            if (mdia < 0) continue;
            int mdiaEnd = mdia + AtomSize(d, mdia);

            int hdlr = FindAtom(d, mdia + 8, mdiaEnd, "hdlr");
            string subtype = hdlr >= 0 ? FourCC(d, hdlr + 8 + 8) : "";
            int mdhd = FindAtom(d, mdia + 8, mdiaEnd, "mdhd");
            uint mediaScale = mdhd >= 0 ? U32(d, mdhd + 8 + 12) : 0;

            int minf = FindAtom(d, mdia + 8, mdiaEnd, "minf");
            int stbl = minf >= 0 ? FindAtom(d, minf + 8, minf + AtomSize(d, minf), "stbl") : -1;
            if (stbl < 0) continue;
            int stblEnd = stbl + AtomSize(d, stbl);

            int stsd = FindAtom(d, stbl + 8, stblEnd, "stsd");
            if (stsd < 0) continue;
            string fourcc = U32(d, stsd + 8 + 4) > 0 ? FourCC(d, stsd + 8 + 8 + 4) : "";

            // Edit list: leading empty edits (mediaTime == -1) delay the track start.
            double trackOffsetMs = 0;
            {
                int edts0 = FindAtom(d, trak + 8, trakEnd, "edts");
                int elst0 = edts0 >= 0 ? FindAtom(d, edts0 + 8, edts0 + AtomSize(d, edts0), "elst") : -1;
                if (elst0 >= 0 && movieScale > 0)
                {
                    int n = (int)U32(d, elst0 + 8 + 4);
                    for (int e = 0; e < n; e++)
                    {
                        uint dur = U32(d, elst0 + 8 + 8 + e * 12);
                        int mediaTime = (int)U32(d, elst0 + 8 + 12 + e * 12);
                        if (mediaTime != -1) break;
                        trackOffsetMs += dur * 1000.0 / movieScale;
                    }
                }
            }

            if (subtype == "soun" && mediaScale > 0 && fourcc.Length > 0)
            {
                var at = ParseAudioTrack(d, stsd, stbl, stblEnd, mediaScale, trackOffsetMs, fourcc);
                if (at is not null) { m.AudioTracks.Add(at); continue; }
            }

            if (subtype != "vide" || mediaScale == 0)
            {
                if (fourcc.Length > 0) m.SkippedTracks.Add(fourcc);
                continue;
            }

            // Video sample description entry: w/h at +32/+34
            // (size4 fourcc4 reserved6 dataRef2 ver2 rev2 vendor4 tq4 sq4 w2 h2 …).
            int entry = stsd + 8 + 8;
            var track = new QtVideoTrack
            {
                FourCC = fourcc,
                Width = (short)U16(d, entry + 32),
                Height = (short)U16(d, entry + 34),
            };

            if (ReadSampleTables(d, stbl, stblEnd, mediaScale, trackOffsetMs, track.Samples))
                m.VideoTracks.Add(track);
        }
        if (m.DurationMs <= 0 && m.VideoTracks.Count == 0) return null;
        return m;
    }

    private static byte[]? DecompressMoov(byte[] file, int cmov)
    {
        int cmovEnd = cmov + AtomSize(file, cmov);
        int dcom = FindAtom(file, cmov + 8, cmovEnd, "dcom");
        int cmvd = FindAtom(file, cmov + 8, cmovEnd, "cmvd");
        if (dcom < 0 || cmvd < 0 || FourCC(file, dcom + 8) != "zlib") return null;
        int rawSize = (int)U32(file, cmvd + 8);
        if (rawSize <= 0 || rawSize > 1 << 24) return null;
        try
        {
            using var src = new MemoryStream(file, cmvd + 12, AtomSize(file, cmvd) - 12);
            using var z = new ZLibStream(src, CompressionMode.Decompress);
            var outBuf = new byte[rawSize];
            int got = 0;
            while (got < rawSize)
            {
                int r = z.Read(outBuf, got, rawSize - got);
                if (r <= 0) break;
                got += r;
            }
            return got == rawSize ? outBuf : null;
        }
        catch { return null; }
    }

    // Parse a sound track: codec params from the SoundDescription (v0/v1) + its
    // 'wave' extension (decoder extradata), packets from the shared sample tables.
    private static QtAudioTrack? ParseAudioTrack(byte[] d, int stsd, int stbl, int stblEnd,
        uint mediaScale, double offsetMs, string fourcc)
    {
        int entry = stsd + 8 + 8;
        int entrySize = (int)U32(d, entry);
        if (entrySize < 36) return null;
        var track = new QtAudioTrack
        {
            FourCC = fourcc,
            OffsetMs = offsetMs,
            Channels = Math.Max(1, U16(d, entry + 16 + 8)),
            SampleRate = U16(d, entry + 16 + 16),           // 16.16 fixed → integer part
        };
        // v1 sound descriptions add 4 longs (samplesPerPacket / bytesPerPacket /
        // bytesPerFrame / bytesPerSample) before the extension atoms.
        int version = U16(d, entry + 16);
        int samplesPerPacket = version >= 1 ? (int)U32(d, entry + 36) : 0;
        int bytesPerFrame = version >= 1 ? (int)U32(d, entry + 44) : 0;
        int ext = entry + (version >= 1 ? 52 : 36);
        int entryEnd = entry + entrySize;
        int wave = ext < entryEnd ? FindAtom(d, ext, entryEnd, "wave") : -1;
        if (wave >= 0)
        {
            int len = AtomSize(d, wave) - 8;
            if (len > 0 && wave + 8 + len <= d.Length)
            {
                track.Extradata = new byte[len];
                Array.Copy(d, wave + 8, track.Extradata, 0, len);
            }
        }
        else if (ext < entryEnd && entryEnd <= d.Length)
        {
            track.Extradata = new byte[entryEnd - ext];
            Array.Copy(d, ext, track.Extradata, 0, entryEnd - ext);
        }

        // v1 compressed audio: the sample tables count PCM SAMPLES, not packets —
        // stsz is uniform "1" and useless. Real framing: each chunk holds
        // (stsc samples / samplesPerPacket) packets of bytesPerFrame bytes
        // (verified against the E3 files: consecutive stco deltas match exactly).
        if (version >= 1 && samplesPerPacket > 0 && bytesPerFrame > 0)
        {
            int stsc = FindAtom(d, stbl + 8, stblEnd, "stsc");
            int stco = FindAtom(d, stbl + 8, stblEnd, "stco");
            if (stsc < 0 || stco < 0) return null;
            int stscN = (int)U32(d, stsc + 8 + 4);
            int stcoN = (int)U32(d, stco + 8 + 4);
            double rate = track.SampleRate > 0 ? track.SampleRate : 8000;
            long pcmPos = 0;
            for (int chunk = 1, e = 0; chunk <= stcoN; chunk++)
            {
                while (e + 1 < stscN && (int)U32(d, stsc + 8 + 8 + (e + 1) * 12) <= chunk) e++;
                int chunkSamples = (int)U32(d, stsc + 8 + 12 + e * 12);
                int packets = chunkSamples / samplesPerPacket;
                int off = (int)U32(d, stco + 8 + 8 + (chunk - 1) * 4);
                for (int k = 0; k < packets; k++)
                {
                    track.Packets.Add(new QtVideoSample
                    {
                        Offset = off + k * bytesPerFrame,
                        Size = bytesPerFrame,
                        StartMs = offsetMs + pcmPos * 1000.0 / rate,
                    });
                    pcmPos += samplesPerPacket;
                }
            }
            return track.Packets.Count > 0 ? track : null;
        }

        return ReadSampleTables(d, stbl, stblEnd, mediaScale, offsetMs, track.Packets) ? track : null;
    }

    private static bool ReadSampleTables(byte[] d, int stbl, int stblEnd, uint mediaScale,
        double offsetMs, List<QtVideoSample> samples)
    {
        int stts = FindAtom(d, stbl + 8, stblEnd, "stts");
        int stsc = FindAtom(d, stbl + 8, stblEnd, "stsc");
        int stsz = FindAtom(d, stbl + 8, stblEnd, "stsz");
        int stco = FindAtom(d, stbl + 8, stblEnd, "stco");
        if (stts < 0 || stsc < 0 || stsz < 0 || stco < 0) return false;

        uint uniform = U32(d, stsz + 8 + 4);
        int sampleCount = (int)U32(d, stsz + 8 + 8);
        if (sampleCount <= 0 || sampleCount > 100000) return false;
        var sizes = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            sizes[i] = uniform != 0 ? (int)uniform : (int)U32(d, stsz + 8 + 12 + i * 4);

        var startUnits = new long[sampleCount];
        {
            int n = (int)U32(d, stts + 8 + 4), s = 0; long t = 0;
            for (int e = 0; e < n && s < sampleCount; e++)
            {
                uint cnt = U32(d, stts + 8 + 8 + e * 8);
                uint dur = U32(d, stts + 8 + 12 + e * 8);
                for (uint k = 0; k < cnt && s < sampleCount; k++) { startUnits[s++] = t; t += dur; }
            }
            while (s < sampleCount) startUnits[s++] = t;
        }

        int stscN = (int)U32(d, stsc + 8 + 4);
        int stcoN = (int)U32(d, stco + 8 + 4);
        int sample = 0;
        for (int chunk = 1, e = 0; chunk <= stcoN && sample < sampleCount; chunk++)
        {
            while (e + 1 < stscN && (int)U32(d, stsc + 8 + 8 + (e + 1) * 12) <= chunk) e++;
            int perChunk = (int)U32(d, stsc + 8 + 12 + e * 12);
            int off = (int)U32(d, stco + 8 + 8 + (chunk - 1) * 4);
            for (int k = 0; k < perChunk && sample < sampleCount; k++, sample++)
            {
                samples.Add(new QtVideoSample
                {
                    Offset = off,
                    Size = sizes[sample],
                    StartMs = offsetMs + startUnits[sample] * 1000.0 / mediaScale,
                });
                off += sizes[sample];
            }
        }
        return samples.Count > 0;
    }

    private static int AtomSize(byte[] d, int off)
    {
        long size = U32(d, off);
        if (size == 0) return d.Length - off;
        return (int)Math.Max(size, 8);
    }

    private static int FindAtom(byte[] d, int start, int end, string type)
    {
        int off = start;
        while (off + 8 <= end && off + 8 <= d.Length)
        {
            int size = AtomSize(d, off);
            if (FourCC(d, off + 4) == type) return off;
            off += size;
        }
        return -1;
    }

    private static uint U32(byte[] d, int o) =>
        o + 4 <= d.Length ? (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]) : 0u;
    private static int U16(byte[] d, int o) => o + 2 <= d.Length ? (d[o] << 8) | d[o + 1] : 0;
    private static string FourCC(byte[] d, int o) =>
        o + 4 <= d.Length
            ? new string(new[] { (char)d[o], (char)d[o + 1], (char)d[o + 2], (char)d[o + 3] })
            : "";
}
