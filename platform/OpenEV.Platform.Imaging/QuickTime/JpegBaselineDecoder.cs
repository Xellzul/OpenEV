using System;

namespace OpenEV.Platform.Imaging.QuickTime;

// Baseline sequential JPEG (ITU T.81) — what QuickTime Photo-JPEG ('jpeg') samples
// contain. Supports 8/16-bit quant tables, 1- or 3-component scans, all common
// chroma samplings (4:4:4 / 4:2:2 / 4:2:0), and restart intervals. No progressive,
// no arithmetic coding — Photo-JPEG never uses either. Output is opaque RGBA.
public static class JpegBaselineDecoder
{
    public static Rgba8Image? Decode(ReadOnlySpan<byte> d)
    {
        var quant = new ushort[4][];
        var huffDc = new HuffTable?[4];
        var huffAc = new HuffTable?[4];
        Component[]? comps = null;
        int width = 0, height = 0, restartInterval = 0;

        int i = 0;
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return null;
        i = 2;
        while (i + 4 <= d.Length)
        {
            if (d[i] != 0xFF) { i++; continue; }
            byte marker = d[i + 1];
            i += 2;
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;
            if (marker == 0xD9) break;
            int len = (d[i] << 8) | d[i + 1];
            int seg = i + 2, segEnd = i + len;
            if (segEnd > d.Length) return null;

            switch (marker)
            {
                case 0xDB:   // DQT
                    while (seg < segEnd)
                    {
                        int pq = d[seg] >> 4, tq = d[seg] & 15; seg++;
                        var q = new ushort[64];
                        for (int k = 0; k < 64; k++)
                        {
                            q[ZigZag[k]] = pq == 0 ? d[seg] : (ushort)((d[seg] << 8) | d[seg + 1]);
                            seg += pq == 0 ? 1 : 2;
                        }
                        quant[tq] = q;
                    }
                    break;

                case 0xC0: case 0xC1:   // SOF0/1 (baseline / extended sequential)
                {
                    height = (d[seg + 1] << 8) | d[seg + 2];
                    width = (d[seg + 3] << 8) | d[seg + 4];
                    int nc = d[seg + 5];
                    comps = new Component[nc];
                    for (int c = 0; c < nc; c++)
                    {
                        int o = seg + 6 + c * 3;
                        comps[c] = new Component
                        {
                            Id = d[o], H = d[o + 1] >> 4, V = d[o + 1] & 15, Tq = d[o + 2],
                        };
                    }
                    break;
                }

                case 0xC4:   // DHT
                    while (seg < segEnd)
                    {
                        int tc = d[seg] >> 4, th = d[seg] & 15; seg++;
                        var counts = new int[17];
                        int total = 0;
                        for (int k = 1; k <= 16; k++) { counts[k] = d[seg + k - 1]; total += counts[k]; }
                        seg += 16;
                        var vals = new byte[total];
                        for (int k = 0; k < total; k++) vals[k] = d[seg + k];
                        seg += total;
                        var t = new HuffTable(counts, vals);
                        if (tc == 0) huffDc[th] = t; else huffAc[th] = t;
                    }
                    break;

                case 0xDD:   // DRI
                    restartInterval = (d[seg] << 8) | d[seg + 1];
                    break;

                case 0xDA:   // SOS — decode the scan and finish
                {
                    if (comps is null || width <= 0 || height <= 0) return null;
                    int ns = d[seg];
                    for (int c = 0; c < ns; c++)
                    {
                        int id = d[seg + 1 + c * 2], tables = d[seg + 2 + c * 2];
                        foreach (var comp in comps)
                            if (comp.Id == id) { comp.Dc = huffDc[tables >> 4]; comp.Ac = huffAc[tables & 15]; }
                    }
                    return DecodeScan(d, segEnd, comps, quant, width, height, restartInterval);
                }
            }
            i += len;
        }
        return null;
    }

    private sealed class Component
    {
        public int Id, H, V, Tq;
        public HuffTable? Dc, Ac;
        public int Pred;
        public byte[] Plane = Array.Empty<byte>();
        public int PlaneW, PlaneH;
    }

    private static Rgba8Image? DecodeScan(ReadOnlySpan<byte> d, int start, Component[] comps,
        ushort[][] quant, int width, int height, int restartInterval)
    {
        int hMax = 1, vMax = 1;
        foreach (var c in comps) { hMax = Math.Max(hMax, c.H); vMax = Math.Max(vMax, c.V); }
        int mcusX = (width + hMax * 8 - 1) / (hMax * 8);
        int mcusY = (height + vMax * 8 - 1) / (vMax * 8);
        foreach (var c in comps)
        {
            c.PlaneW = mcusX * c.H * 8;
            c.PlaneH = mcusY * c.V * 8;
            c.Plane = new byte[c.PlaneW * c.PlaneH];
            if (quant[c.Tq] is null || c.Dc is null || (comps.Length > 0 && c.Ac is null)) return null;
        }

        var br = new BitReader(d, start);
        Span<int> block = stackalloc int[64];
        Span<double> tmp = stackalloc double[64];
        int mcu = 0, totalMcus = mcusX * mcusY;
        while (mcu < totalMcus)
        {
            if (restartInterval > 0 && mcu > 0 && mcu % restartInterval == 0)
            {
                br.AlignToRestart();
                foreach (var c in comps) c.Pred = 0;
            }
            int mx = mcu % mcusX, my = mcu / mcusX;
            foreach (var c in comps)
            {
                for (int by = 0; by < c.V; by++)
                    for (int bx = 0; bx < c.H; bx++)
                    {
                        if (!DecodeBlock(ref br, c, quant[c.Tq], block)) return Emit(comps, width, height, hMax, vMax);
                        Idct(block, tmp);
                        int px = (mx * c.H + bx) * 8, py = (my * c.V + by) * 8;
                        for (int y = 0; y < 8; y++)
                        {
                            int row = (py + y) * c.PlaneW + px;
                            for (int x = 0; x < 8; x++)
                            {
                                int v = block[y * 8 + x] + 128;
                                c.Plane[row + x] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
                            }
                        }
                    }
            }
            mcu++;
        }
        return Emit(comps, width, height, hMax, vMax);
    }

    private static bool DecodeBlock(ref BitReader br, Component c, ushort[] q, scoped Span<int> block)
    {
        block.Clear();
        int t = c.Dc!.Decode(ref br);
        if (t < 0) return false;
        int diff = t == 0 ? 0 : br.Receive(t);
        c.Pred += Extend(diff, t);
        block[0] = c.Pred * q[0];
        int k = 1;
        while (k < 64)
        {
            int rs = c.Ac!.Decode(ref br);
            if (rs < 0) return false;
            int r = rs >> 4, s = rs & 15;
            if (s == 0)
            {
                if (r != 15) break;   // EOB
                k += 16;              // ZRL
                continue;
            }
            k += r;
            if (k > 63) break;
            int v = Extend(br.Receive(s), s);
            block[ZigZag[k]] = v * q[ZigZag[k]];
            k++;
        }
        return true;
    }

    private static int Extend(int v, int t) => t == 0 ? 0 : v < (1 << (t - 1)) ? v - (1 << t) + 1 : v;

    // Separable float IDCT with the precomputed 1-D basis; plenty for movie-sized frames.
    private static readonly double[] Basis = BuildBasis();
    private static double[] BuildBasis()
    {
        var b = new double[64];
        for (int x = 0; x < 8; x++)
            for (int u = 0; u < 8; u++)
                b[x * 8 + u] = (u == 0 ? Math.Sqrt(0.125) : 0.5) * Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
        return b;
    }

    private static void Idct(Span<int> block, Span<double> tmp)
    {
        for (int y = 0; y < 8; y++)          // rows
            for (int x = 0; x < 8; x++)
            {
                double s = 0;
                for (int u = 0; u < 8; u++) s += Basis[x * 8 + u] * block[y * 8 + u];
                tmp[y * 8 + x] = s;
            }
        for (int x = 0; x < 8; x++)          // columns
            for (int y = 0; y < 8; y++)
            {
                double s = 0;
                for (int v = 0; v < 8; v++) s += Basis[y * 8 + v] * tmp[v * 8 + x];
                block[y * 8 + x] = (int)Math.Round(s);
            }
    }

    private static Rgba8Image Emit(Component[] comps, int width, int height, int hMax, int vMax)
    {
        var img = new Rgba8Image(width, height);
        var yC = comps[0];
        var cb = comps.Length >= 3 ? comps[1] : null;
        var cr = comps.Length >= 3 ? comps[2] : null;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int lum = yC.Plane[(y * yC.V / vMax) * yC.PlaneW + (x * yC.H / hMax)];
                if (cb is null || cr is null)
                {
                    img.SetPixel(x, y, (byte)lum, (byte)lum, (byte)lum, 255);
                    continue;
                }
                int pb = cb.Plane[(y * cb.V / vMax) * cb.PlaneW + (x * cb.H / hMax)] - 128;
                int pr = cr.Plane[(y * cr.V / vMax) * cr.PlaneW + (x * cr.H / hMax)] - 128;
                int r = (int)Math.Round(lum + 1.402 * pr);
                int g = (int)Math.Round(lum - 0.344136 * pb - 0.714136 * pr);
                int b = (int)Math.Round(lum + 1.772 * pb);
                img.SetPixel(x, y,
                    (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), 255);
            }
        return img;
    }

    private sealed class HuffTable
    {
        // Canonical code assignment; decode bit-by-bit against per-length ranges.
        private readonly int[] _minCode = new int[17];
        private readonly int[] _maxCode = new int[17];
        private readonly int[] _valPtr = new int[17];
        private readonly byte[] _vals;

        public HuffTable(int[] counts, byte[] vals)
        {
            _vals = vals;
            int code = 0, k = 0;
            for (int len = 1; len <= 16; len++)
            {
                _valPtr[len] = k;
                _minCode[len] = code;
                code += counts[len]; k += counts[len];
                _maxCode[len] = counts[len] > 0 ? code - 1 : -1;
                code <<= 1;
            }
        }

        public int Decode(ref BitReader br)
        {
            int code = 0;
            for (int len = 1; len <= 16; len++)
            {
                int bit = br.ReadBit();
                if (bit < 0) return -1;
                code = (code << 1) | bit;
                if (_maxCode[len] >= 0 && code <= _maxCode[len] && code >= _minCode[len])
                    return _vals[_valPtr[len] + code - _minCode[len]];
            }
            return -1;
        }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _d;
        private int _pos;
        private int _bitBuf, _bitCount;

        public BitReader(ReadOnlySpan<byte> d, int pos) { _d = d; _pos = pos; _bitBuf = 0; _bitCount = 0; }

        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                if (_pos >= _d.Length) return -1;
                byte b = _d[_pos++];
                if (b == 0xFF)
                {
                    if (_pos < _d.Length && _d[_pos] == 0x00) _pos++;          // stuffed byte
                    else if (_pos < _d.Length && _d[_pos] >= 0xD0 && _d[_pos] <= 0xD7)
                        return -1;   // restart marker reached mid-read — AlignToRestart consumes it
                    else return -1;  // EOI or other marker
                }
                _bitBuf = b; _bitCount = 8;
            }
            _bitCount--;
            return (_bitBuf >> _bitCount) & 1;
        }

        public int Receive(int n)
        {
            int v = 0;
            for (int k = 0; k < n; k++)
            {
                int bit = ReadBit();
                if (bit < 0) return v << (n - k);
                v = (v << 1) | bit;
            }
            return v;
        }

        // Skip to just past the next RSTn marker (called at restart-interval boundaries).
        public void AlignToRestart()
        {
            _bitCount = 0;
            while (_pos + 1 < _d.Length)
            {
                if (_d[_pos] == 0xFF && _d[_pos + 1] >= 0xD0 && _d[_pos + 1] <= 0xD7)
                {
                    _pos += 2;
                    return;
                }
                _pos++;
            }
        }
    }

    private static readonly int[] ZigZag =
    {
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    };
}
