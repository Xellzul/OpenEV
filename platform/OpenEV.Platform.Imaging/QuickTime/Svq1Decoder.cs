using System;

namespace OpenEV.Platform.Imaging.QuickTime;

// Sorenson Video 1 ('SVQ1') — hierarchical vector-quantization codec, YUV 4:1:0.
// Ported from FFmpeg's LGPL decoder (libavcodec/svq1dec.c; codebooks/VLC tables
// generated into Svq1Tables.cs from svq1_cb.h / svq1_vlc.h / h263data.c). The
// uint32 lane arithmetic of the original is unrolled to per-pixel int math —
// identical results, no packed saturation tricks.
internal sealed class Svq1Decoder
{
    private const int BlockSkip = 0, BlockInter = 1, BlockInter4V = 2, BlockIntra = 3;

    private int _width, _height;             // actual (unaligned) frame dims
    private byte[][]? _cur, _prev;           // 3 planes, pitch-padded
    private readonly int[] _planeW = new int[3], _planeH = new int[3], _pitch = new int[3];
    private int _lastTempref = 0xFF;
    private Rgba8Image? _rgb;

    public Svq1Decoder(int width, int height)
    {
        _width = (width + 3) & ~3;
        _height = (height + 3) & ~3;
    }

    public Rgba8Image? DecodeFrame(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return null;
        byte[] buf = data.ToArray();

        var br = new BitReader(buf);
        int frameCode = br.Bits(22);
        if ((frameCode & ~0x70) != 0 || (frameCode & 0x60) == 0) return null;

        // De-obfuscate header words 1..4: swap each group's 16-bit halves and XOR
        // with group 7-i (byte-order-independent form of the reference uint32 op).
        if (frameCode != 0x20)
        {
            if (buf.Length < 36) return null;
            for (int i = 0; i < 4; i++)
            {
                int a = 4 + i * 4, b = 4 + (7 - i) * 4;
                byte m0 = buf[a], m1 = buf[a + 1], m2 = buf[a + 2], m3 = buf[a + 3];
                buf[a] = (byte)(m2 ^ buf[b]);
                buf[a + 1] = (byte)(m3 ^ buf[b + 1]);
                buf[a + 2] = (byte)(m0 ^ buf[b + 2]);
                buf[a + 3] = (byte)(m1 ^ buf[b + 3]);
            }
            br = new BitReader(buf);
            br.Skip(22);
        }

        // ── frame header ──
        int tempref = br.Bits(8);
        bool buggy = tempref == 0 && _lastTempref == 0;   // no container extradata (QuickTime path)
        _lastTempref = tempref;

        int frameType = br.Bits(2);          // 0 = I, 1 = P, 2 = P non-ref
        if (frameType == 3) return null;
        bool intraFrame = frameType == 0;
        bool nonref = frameType == 2;

        if (intraFrame)
        {
            if (frameCode == 0x50 || frameCode == 0x60)
                br.Skip(16);                  // packet checksum (diagnostics only)
            if ((frameCode ^ 0x10) >= 0x50)
                ParseEmbeddedString(ref br);  // consume — keeps the bit position honest
            br.Skip(5);
            int sizeCode = br.Bits(3);
            int w, h;
            if (sizeCode == 7)
            {
                w = br.Bits(12); h = br.Bits(12);
                if (w == 0 || h == 0) return null;
            }
            else
            {
                w = Svq1Tables.FrameSizeTable[sizeCode, 0];
                h = Svq1Tables.FrameSizeTable[sizeCode, 1];
            }
            if (w != _width || h != _height) { _width = w; _height = h; _cur = _prev = null; }
        }

        if (br.Bit() != 0)
        {
            br.Skip(2);
            if (br.Bits(2) != 0) return null;
        }
        if (br.Bit() != 0)
        {
            br.Skip(8);
            while (br.Bit() != 0) { br.Skip(8); if (br.Overrun) return null; }
        }
        if (br.Overrun) return null;

        EnsurePlanes();
        if (!intraFrame && _prev is null) return null;   // P frame with no reference

        var cur = _cur!;
        for (int p = 0; p < 3; p++)
        {
            int width = p == 0 ? Align16(_width) : Align16(_width / 4);
            int height = p == 0 ? Align16(_height) : Align16(_height / 4);
            int pitch = _pitch[p];

            if (intraFrame)
            {
                for (int y = 0; y < height; y += 16)
                    for (int x = 0; x < width; x += 16)
                        if (!DecodeBlockIntra(ref br, cur[p], y * pitch + x, pitch))
                            return null;
            }
            else
            {
                var motion = new int[(width / 8 + 4) * 2];   // interleaved x,y
                for (int y = 0; y < height; y += 16)
                {
                    for (int x = 0; x < width; x += 16)
                        if (!DecodeDeltaBlock(ref br, cur[p], _prev![p], pitch, motion, x, y, width, height, buggy))
                            return null;
                    motion[0] = motion[1] = 0;
                }
            }
        }

        if (!nonref)
        {
            _prev ??= NewPlanes();
            for (int p = 0; p < 3; p++) Array.Copy(cur[p], _prev[p], cur[p].Length);
        }
        return EmitRgb(cur);
    }

    private static int Align16(int v) => (v + 15) & ~15;

    private void EnsurePlanes()
    {
        if (_cur is not null) return;
        _cur = NewPlanes();
        _prev = null;
    }

    private byte[][] NewPlanes()
    {
        var planes = new byte[3][];
        for (int p = 0; p < 3; p++)
        {
            _planeW[p] = p == 0 ? Align16(_width) : Align16(_width / 4);
            _planeH[p] = p == 0 ? Align16(_height) : Align16(_height / 4);
            _pitch[p] = _planeW[p] + 16;                       // half-pel overread padding
            planes[p] = new byte[_pitch[p] * (_planeH[p] + 1) + 16];
        }
        return planes;
    }

    // ── intra block (16×16 quadtree) — svq1_decode_block_intra ──
    private static bool DecodeBlockIntra(ref BitReader br, byte[] plane, int off, int pitch)
    {
        Span<int> list = stackalloc int[63];
        Span<int> entry = stackalloc int[6];
        list[0] = off;
        int m = 1, n = 1, level = 5;
        for (int i = 0; i < n; i++)
        {
            for (; level > 0; i++)
            {
                if (i == m) { m = n; if (--level == 0) break; }
                if (br.Bit() == 0) break;
                list[n++] = list[i];
                list[n++] = list[i] + ((((level & 1) != 0) ? pitch : 1) << ((level >> 1) + 1));
            }
            int width = 1 << ((4 + level) / 2), height = 1 << ((3 + level) / 2);
            int dst = list[i];

            int stages = Svq1Vlc.IntraMultistage[level].Decode(ref br) - 1;
            if (br.Overrun) return false;
            if (stages == -1)
            {
                for (int y = 0; y < height; y++)
                    plane.AsSpan(dst + y * pitch, width).Clear();
                continue;
            }
            if (stages > 0 && level >= 4) return false;

            int mean = Svq1Vlc.IntraMean.Decode(ref br);
            if (mean < 0 || br.Overrun) return false;

            if (stages == 0)
            {
                for (int y = 0; y < height; y++)
                    plane.AsSpan(dst + y * pitch, width).Fill((byte)mean);
                continue;
            }

            sbyte[] codebook = Svq1Tables.IntraCodebooks[level]!;
            int bitCache = br.Bits(4 * stages);
            for (int j = 0; j < stages; j++)
                entry[j] = ((((bitCache >> (4 * (stages - j - 1))) & 0xF) + 16 * j) << (level + 3));

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int v = mean;
                    for (int j = 0; j < stages; j++) v += codebook[entry[j] + y * width + x];
                    plane[dst + y * pitch + x] = (byte)Math.Clamp(v, 0, 255);
                }
        }
        return !br.Overrun;
    }

    // ── inter residual block — svq1_decode_block_non_intra ──
    private static bool DecodeBlockNonIntra(ref BitReader br, byte[] plane, int off, int pitch, bool buggy)
    {
        Span<int> list = stackalloc int[63];
        Span<int> entry = stackalloc int[6];
        list[0] = off;
        int m = 1, n = 1, level = 5;
        for (int i = 0; i < n; i++)
        {
            for (; level > 0; i++)
            {
                if (i == m) { m = n; if (--level == 0) break; }
                if (br.Bit() == 0) break;
                list[n++] = list[i];
                list[n++] = list[i] + ((((level & 1) != 0) ? pitch : 1) << ((level >> 1) + 1));
            }
            int width = 1 << ((4 + level) / 2), height = 1 << ((3 + level) / 2);
            int dst = list[i];

            int stages = Svq1Vlc.InterMultistage[level].Decode(ref br) - 1;
            if (br.Overrun) return false;
            if (stages == -1) continue;
            if (stages > 0 && level >= 4) return false;

            int mean = Svq1Vlc.InterMean.Decode(ref br) - 256;
            if (br.Overrun) return false;
            if (buggy)
            {
                if (mean == -128) mean = 128;
                else if (mean == 128) mean = -128;
            }

            sbyte[] codebook = Svq1Tables.InterCodebooks[level]!;
            int bitCache = stages > 0 ? br.Bits(4 * stages) : 0;
            for (int j = 0; j < stages; j++)
                entry[j] = ((((bitCache >> (4 * (stages - j - 1))) & 0xF) + 16 * j) << (level + 3));

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int v = plane[dst + y * pitch + x] + mean;
                    for (int j = 0; j < stages; j++) v += codebook[entry[j] + y * width + x];
                    plane[dst + y * pitch + x] = (byte)Math.Clamp(v, 0, 255);
                }
        }
        return !br.Overrun;
    }

    // ── delta (P-frame) macroblock — svq1_decode_delta_block ──
    private static bool DecodeDeltaBlock(ref BitReader br, byte[] cur, byte[] prev, int pitch,
        int[] motion, int x, int y, int width, int height, bool buggy)
    {
        int blockType = Svq1Vlc.BlockType.Decode(ref br);
        if (blockType < 0 || br.Overrun) return false;

        if (blockType == BlockSkip || blockType == BlockIntra)
        {
            int b = (x / 8 + 2) * 2;
            motion[0] = motion[1] = 0;
            motion[b] = motion[b + 1] = 0;
            motion[b + 2] = motion[b + 3] = 0;
        }

        int off = y * pitch + x;
        switch (blockType)
        {
            case BlockSkip:
                for (int r = 0; r < 16; r++)
                    Array.Copy(prev, off + r * pitch, cur, off + r * pitch, 16);
                return true;

            case BlockInter:
                return MotionInterBlock(ref br, cur, prev, pitch, motion, x, y, width, height)
                    && DecodeBlockNonIntra(ref br, cur, off, pitch, buggy);

            case BlockInter4V:
                return MotionInter4VBlock(ref br, cur, prev, pitch, motion, x, y, width, height)
                    && DecodeBlockNonIntra(ref br, cur, off, pitch, buggy);

            default:
                return DecodeBlockIntra(ref br, cur, off, pitch);
        }
    }

    private static int MidPred(int a, int b, int c) => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    private static int SignExtend6(int v) => ((v & 63) ^ 32) - 32;

    private static bool DecodeMotionVector(ref BitReader br, ref int mvX, ref int mvY,
        int p0x, int p0y, int p1x, int p1y, int p2x, int p2y)
    {
        for (int i = 0; i < 2; i++)
        {
            int diff = Svq1Vlc.Motion.Decode(ref br);
            if (diff < 0 || br.Overrun) return false;
            if (diff != 0 && br.Bit() != 0) diff = -diff;
            if (i == 1) mvY = SignExtend6(diff + MidPred(p0y, p1y, p2y));
            else mvX = SignExtend6(diff + MidPred(p0x, p1x, p2x));
        }
        return true;
    }

    private static bool MotionInterBlock(ref BitReader br, byte[] cur, byte[] prev, int pitch,
        int[] motion, int x, int y, int width, int height)
    {
        int b2 = (x / 8 + 2) * 2, b4 = (x / 8 + 4) * 2;
        int p1x = y == 0 ? motion[0] : motion[b2], p1y = y == 0 ? motion[1] : motion[b2 + 1];
        int p2x = y == 0 ? motion[0] : motion[b4], p2y = y == 0 ? motion[1] : motion[b4 + 1];
        int mvX = 0, mvY = 0;
        if (!DecodeMotionVector(ref br, ref mvX, ref mvY, motion[0], motion[1], p1x, p1y, p2x, p2y))
            return false;

        int b3 = (x / 8 + 3) * 2;
        motion[0] = motion[b2] = motion[b3] = mvX;
        motion[1] = motion[b2 + 1] = motion[b3 + 1] = mvY;

        int cx = Math.Clamp(mvX, -2 * x, 2 * (width - x - 16));
        int cy = Math.Clamp(mvY, -2 * y, 2 * (height - y - 16));
        HalfPelCopy(cur, y * pitch + x, prev, (y + (cy >> 1)) * pitch + x + (cx >> 1), pitch, 16, cx & 1, cy & 1);
        return true;
    }

    private static bool MotionInter4VBlock(ref BitReader br, byte[] cur, byte[] prev, int pitch,
        int[] motion, int x, int y, int width, int height)
    {
        int b1 = (x / 8 + 1) * 2, b2 = (x / 8 + 2) * 2, b3 = (x / 8 + 3) * 2, b4 = (x / 8 + 4) * 2;

        // MV0 → local mv (predictors as in the plain inter block).
        int p1x = y == 0 ? motion[0] : motion[b2], p1y = y == 0 ? motion[1] : motion[b2 + 1];
        int p2x = y == 0 ? motion[0] : motion[b4], p2y = y == 0 ? motion[1] : motion[b4 + 1];
        int mv0x = 0, mv0y = 0;
        if (!DecodeMotionVector(ref br, ref mv0x, ref mv0y, motion[0], motion[1], p1x, p1y, p2x, p2y))
            return false;

        // MV1 → motion[0]; y==0 keeps all three predictors = mv0, else pmv[2] stays motion[x/8+4].
        p1x = y == 0 ? mv0x : motion[b3]; p1y = y == 0 ? mv0y : motion[b3 + 1];
        p2x = y == 0 ? mv0x : motion[b4]; p2y = y == 0 ? mv0y : motion[b4 + 1];
        int mv1x = 0, mv1y = 0;
        if (!DecodeMotionVector(ref br, ref mv1x, ref mv1y, mv0x, mv0y, p1x, p1y, p2x, p2y))
            return false;
        motion[0] = mv1x; motion[1] = mv1y;

        // MV2 → motion[x/8+2]; predictors {mv0, motion[0], motion[x/8+1]}.
        int mv2x = 0, mv2y = 0;
        if (!DecodeMotionVector(ref br, ref mv2x, ref mv2y, mv0x, mv0y, motion[0], motion[1], motion[b1], motion[b1 + 1]))
            return false;
        motion[b2] = mv2x; motion[b2 + 1] = mv2y;

        // MV3 → motion[x/8+3]; predictors {mv0, motion[0], motion[x/8+2]}.
        int mv3x = 0, mv3y = 0;
        if (!DecodeMotionVector(ref br, ref mv3x, ref mv3y, mv0x, mv0y, motion[0], motion[1], motion[b2], motion[b2 + 1]))
            return false;
        motion[b3] = mv3x; motion[b3 + 1] = mv3y;

        Span<int> mvx = stackalloc int[4] { mv0x, motion[0], motion[b2], motion[b3] };
        Span<int> mvy = stackalloc int[4] { mv0y, motion[1], motion[b2 + 1], motion[b3 + 1] };
        for (int i = 0; i < 4; i++)
        {
            int fx = Math.Clamp(mvx[i] + (i & 1) * 16, -2 * x, 2 * (width - x - 8));
            int fy = Math.Clamp(mvy[i] + (i >> 1) * 16, -2 * y, 2 * (height - y - 8));
            int dst = (y + (i >> 1) * 8) * pitch + x + (i & 1) * 8;
            int src = (y + (fy >> 1)) * pitch + x + (fx >> 1);
            HalfPelCopy(cur, dst, prev, src, pitch, 8, fx & 1, fy & 1);
        }
        return true;
    }

    private static void HalfPelCopy(byte[] cur, int dst, byte[] prev, int src, int pitch, int size, int dx, int dy)
    {
        for (int r = 0; r < size; r++)
        {
            int d = dst + r * pitch, s = src + r * pitch;
            for (int c = 0; c < size; c++)
            {
                int v;
                if (dx == 0 && dy == 0) v = prev[s + c];
                else if (dy == 0) v = (prev[s + c] + prev[s + c + 1] + 1) >> 1;
                else if (dx == 0) v = (prev[s + c] + prev[s + c + pitch] + 1) >> 1;
                else v = (prev[s + c] + prev[s + c + 1] + prev[s + c + pitch] + prev[s + c + pitch + 1] + 2) >> 2;
                cur[d + c] = (byte)v;
            }
        }
    }

    // Embedded copyright string — decode fully so the bit position stays exact.
    private static void ParseEmbeddedString(ref BitReader br)
    {
        int len = br.Bits(8);
        byte seed = StringTable[len];
        for (int i = 1; i <= len; i++)
        {
            byte c = (byte)(br.Bits(8) ^ seed);
            seed = StringTable[c ^ seed];
        }
    }

    private Rgba8Image EmitRgb(byte[][] planes)
    {
        _rgb ??= new Rgba8Image(_width, _height);
        if (_rgb.Width != _width || _rgb.Height != _height) _rgb = new Rgba8Image(_width, _height);
        int lp = _pitch[0], cp = _pitch[1];
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                int lum = planes[0][y * lp + x];
                int u = planes[1][(y / 4) * cp + x / 4] - 128;
                int v = planes[2][(y / 4) * cp + x / 4] - 128;
                double yy = 1.164 * (lum - 16);
                int r = (int)Math.Round(yy + 1.596 * v);
                int g = (int)Math.Round(yy - 0.813 * v - 0.391 * u);
                int b = (int)Math.Round(yy + 2.018 * u);
                _rgb.SetPixel(x, y,
                    (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), 255);
            }
        return _rgb;
    }

    private static readonly byte[] StringTable =
    {
        0x00, 0xD5, 0x7F, 0xAA, 0xFE, 0x2B, 0x81, 0x54, 0x29, 0xFC, 0x56, 0x83, 0xD7, 0x02, 0xA8, 0x7D,
        0x52, 0x87, 0x2D, 0xF8, 0xAC, 0x79, 0xD3, 0x06, 0x7B, 0xAE, 0x04, 0xD1, 0x85, 0x50, 0xFA, 0x2F,
        0xA4, 0x71, 0xDB, 0x0E, 0x5A, 0x8F, 0x25, 0xF0, 0x8D, 0x58, 0xF2, 0x27, 0x73, 0xA6, 0x0C, 0xD9,
        0xF6, 0x23, 0x89, 0x5C, 0x08, 0xDD, 0x77, 0xA2, 0xDF, 0x0A, 0xA0, 0x75, 0x21, 0xF4, 0x5E, 0x8B,
        0x9D, 0x48, 0xE2, 0x37, 0x63, 0xB6, 0x1C, 0xC9, 0xB4, 0x61, 0xCB, 0x1E, 0x4A, 0x9F, 0x35, 0xE0,
        0xCF, 0x1A, 0xB0, 0x65, 0x31, 0xE4, 0x4E, 0x9B, 0xE6, 0x33, 0x99, 0x4C, 0x18, 0xCD, 0x67, 0xB2,
        0x39, 0xEC, 0x46, 0x93, 0xC7, 0x12, 0xB8, 0x6D, 0x10, 0xC5, 0x6F, 0xBA, 0xEE, 0x3B, 0x91, 0x44,
        0x6B, 0xBE, 0x14, 0xC1, 0x95, 0x40, 0xEA, 0x3F, 0x42, 0x97, 0x3D, 0xE8, 0xBC, 0x69, 0xC3, 0x16,
        0xEF, 0x3A, 0x90, 0x45, 0x11, 0xC4, 0x6E, 0xBB, 0xC6, 0x13, 0xB9, 0x6C, 0x38, 0xED, 0x47, 0x92,
        0xBD, 0x68, 0xC2, 0x17, 0x43, 0x96, 0x3C, 0xE9, 0x94, 0x41, 0xEB, 0x3E, 0x6A, 0xBF, 0x15, 0xC0,
        0x4B, 0x9E, 0x34, 0xE1, 0xB5, 0x60, 0xCA, 0x1F, 0x62, 0xB7, 0x1D, 0xC8, 0x9C, 0x49, 0xE3, 0x36,
        0x19, 0xCC, 0x66, 0xB3, 0xE7, 0x32, 0x98, 0x4D, 0x30, 0xE5, 0x4F, 0x9A, 0xCE, 0x1B, 0xB1, 0x64,
        0x72, 0xA7, 0x0D, 0xD8, 0x8C, 0x59, 0xF3, 0x26, 0x5B, 0x8E, 0x24, 0xF1, 0xA5, 0x70, 0xDA, 0x0F,
        0x20, 0xF5, 0x5F, 0x8A, 0xDE, 0x0B, 0xA1, 0x74, 0x09, 0xDC, 0x76, 0xA3, 0xF7, 0x22, 0x88, 0x5D,
        0xD6, 0x03, 0xA9, 0x7C, 0x28, 0xFD, 0x57, 0x82, 0xFF, 0x2A, 0x80, 0x55, 0x01, 0xD4, 0x7E, 0xAB,
        0x84, 0x51, 0xFB, 0x2E, 0x7A, 0xAF, 0x05, 0xD0, 0xAD, 0x78, 0xD2, 0x07, 0x53, 0x86, 0x2C, 0xF9,
    };
}

// MSB-first bit reader over a byte buffer; reads past the end return 0 and latch Overrun.
internal ref struct BitReader
{
    private readonly byte[] _d;
    private int _bit;
    public bool Overrun { get; private set; }

    public BitReader(byte[] d) { _d = d; _bit = 0; Overrun = false; }

    public int Bit()
    {
        if (_bit >= _d.Length * 8) { Overrun = true; return 0; }
        int v = (_d[_bit >> 3] >> (7 - (_bit & 7))) & 1;
        _bit++;
        return v;
    }

    public int Bits(int n)
    {
        int v = 0;
        for (int i = 0; i < n; i++) v = (v << 1) | Bit();
        return v;
    }

    public void Skip(int n) => _bit += n;
}

// Prefix decoder over ffmpeg-style {code, length} symbol tables.
internal sealed class Svq1Vlc
{
    private readonly System.Collections.Generic.Dictionary<int, int> _map = new();
    private readonly int _maxLen;

    private Svq1Vlc(Func<int, (int code, int len)> get, int count)
    {
        for (int sym = 0; sym < count; sym++)
        {
            var (code, len) = get(sym);
            if (len <= 0) continue;
            _map[(len << 24) | code] = sym;
            _maxLen = Math.Max(_maxLen, len);
        }
    }

    public int Decode(ref BitReader br)
    {
        int code = 0;
        for (int len = 1; len <= _maxLen; len++)
        {
            code = (code << 1) | br.Bit();
            if (br.Overrun) return -1;
            if (_map.TryGetValue((len << 24) | code, out int sym)) return sym;
        }
        return -1;
    }

    public static readonly Svq1Vlc BlockType =
        new(s => (Svq1Tables.BlockTypeVlc[s, 0], Svq1Tables.BlockTypeVlc[s, 1]), 4);
    public static readonly Svq1Vlc Motion =
        new(s => (Svq1Tables.MvTab[s, 0], Svq1Tables.MvTab[s, 1]), 33);
    public static readonly Svq1Vlc IntraMean =
        new(s => (Svq1Tables.IntraMeanVlc[s, 0], Svq1Tables.IntraMeanVlc[s, 1]), 256);
    public static readonly Svq1Vlc InterMean =
        new(s => (Svq1Tables.InterMeanVlc[s, 0], Svq1Tables.InterMeanVlc[s, 1]), 512);
    public static readonly Svq1Vlc[] IntraMultistage = BuildMultistage(intra: true);
    public static readonly Svq1Vlc[] InterMultistage = BuildMultistage(intra: false);

    private static Svq1Vlc[] BuildMultistage(bool intra)
    {
        var result = new Svq1Vlc[6];
        for (int lvl = 0; lvl < 6; lvl++)
        {
            int l = lvl;
            result[lvl] = intra
                ? new Svq1Vlc(s => (Svq1Tables.IntraMultistageVlc[l, s, 0], Svq1Tables.IntraMultistageVlc[l, s, 1]), 8)
                : new Svq1Vlc(s => (Svq1Tables.InterMultistageVlc[l, s, 0], Svq1Tables.InterMultistageVlc[l, s, 1]), 8);
        }
        return result;
    }
}
