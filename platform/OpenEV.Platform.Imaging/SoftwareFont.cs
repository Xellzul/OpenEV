using System;
using System.Collections.Generic;
using FontStashSharp.Interfaces;
using FontStashSharp.Rasterizers.StbTrueTypeSharp;

namespace OpenEV.Platform.Imaging;

// Portable text renderer — the managed replacement for FontStashSharp's
// MonoGame-coupled FontSystem/DynamicSpriteFont (whose 1.3.9 IFontStashRenderer
// hard-depends on a MonoGame GraphicsDevice). We use only the renderer-agnostic
// rasterizer side: FontStashSharp.Base's IFontSource (glyph ids, metrics,
// kerning, 8-bit coverage rasterization) driven by the StbTrueTypeSharp loader,
// and do EVO's simple left-to-right layout ourselves, blitting glyph cells with
// the same Canvas as everything else.
//
// Glyphs are cached PREMULTIPLIED (rgb = a = coverage). Canvas.BlitGlyph then
// multiplies by the text colour (→ premultiplied tinted glyph) and composites
// with the premultiplied-over blend, reproducing exactly what the old
// SpriteBatch + premultiplied glyph atlas produced — including antialiased edges.
public sealed class SoftwareFont
{
    private readonly IFontSource? _src;
    private readonly Dictionary<long, Glyph> _cache = new();
    /// Threshold outline rasterization to 1-bit (each pixel fully on or off), like the
    /// classic Mac TrueType scaler — Mac OS drew all of EVO's text unsmoothed. Bitmap
    /// strikes are inherently 1-bit; this extends the same look to TTF glyphs. Set it
    /// before the first draw (cached glyphs are not re-rasterized).
    public bool Monochrome { get; set; }
    /// Interpret draw sizes as Mac POINT sizes: the classic Mac scaler maps point size
    /// straight to pixels-per-em (72 dpi), while FontStashSharp's stb source scales so
    /// hhea (ascent − descent) = size — glyphs come out smaller by that ratio (Times
    /// New Roman ≈ 11%: the About-EVÉ credits roll at TextSize(24) rendered a ~21.7px
    /// em vs the Mac's 24px). When set, sizes are converted at the rasterizer boundary
    /// by the font's own head/hhea tables so `size` = pixels-per-em, like the Mac.
    /// Set before the first draw (cached glyphs are keyed by the Mac size). Off by
    /// default: faces verified against Mac captures at FSS scaling (and every bitmap
    /// strike, which this never affects) keep their pixels.
    public bool MacPointSizes { get; set; }
    // hhea (ascent − descent) / head unitsPerEm — the MacPointSizes conversion factor.
    private readonly float _hheaPerEm = 1f;
    // Optional 1-bit Mac bitmap strikes keyed by pixel size. When the requested draw size
    // matches a strike, it is rendered pixel-for-pixel in preference to the TTF outline —
    // this is how the register app reproduces Mac Geneva 9 exactly. Empty for plain TTF use.
    private readonly Dictionary<int, MacBitmapFont> _strikes = new();
    // The game runs text LAYOUT (StringWidth/TETextBox word-wrap) synchronously on whichever
    // thread needs the pixel measurement right away, while the matching glyph DRAW is queued
    // and later runs on the host's draw-drain thread — so two threads can call into this same
    // SoftwareFont concurrently. _src (FontStashSharp's StbTrueTypeSharpSource, including its
    // internal glyph-kerning-advance cache) and _cache are not thread-safe, so unsynchronized
    // concurrent access corrupted the kerning cache and crashed with IndexOutOfRangeException
    // (title thread + host draw thread both mid-measure/mid-draw). Serialize all _src/_cache
    // touches through this lock — a port-substrate fix, not a decompile behavior change.
    private readonly object _lock = new();

    public SoftwareFont(byte[] ttf)
    {
        _src = new StbTrueTypeSharpLoader(new StbTrueTypeSharpSettings()).Load(ttf);
        _hheaPerEm = ReadHheaPerEm(ttf);
    }

    // (hhea ascent − descent) / head unitsPerEm from the raw sfnt tables (first font of
    // a 'ttcf' collection, matching what the stb loader opens). 1 when unreadable.
    private static float ReadHheaPerEm(byte[] b)
    {
        try
        {
            ushort U16(int o) => (ushort)((b[o] << 8) | b[o + 1]);
            int U32(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
            int font = U32(0) == 0x74746366 ? U32(12) : 0;   // 'ttcf' → first font's dir
            int headOff = 0, hheaOff = 0;
            for (int i = 0; i < U16(font + 4); i++)
            {
                int rec = font + 12 + i * 16;
                switch (U32(rec))
                {
                    case 0x68656164: headOff = U32(rec + 8); break;   // 'head'
                    case 0x68686561: hheaOff = U32(rec + 8); break;   // 'hhea'
                }
            }
            int upm = U16(headOff + 18);
            if (headOff == 0 || hheaOff == 0 || upm == 0) return 1f;
            return ((short)U16(hheaOff + 4) - (short)U16(hheaOff + 6)) / (float)upm;
        }
        catch { return 1f; }
    }

    // The size handed to the rasterizer: Mac point size → FSS's hhea-height size.
    private float FssSize(int size) => MacPointSizes ? size * _hheaPerEm : size;

    private SoftwareFont() { }   // strike-only (no TTF fallback)

    /// A SoftwareFont with no TTF backing — only the given bitmap strike.
    public static SoftwareFont FromStrike(MacBitmapFont strike)
    {
        var f = new SoftwareFont();
        f.AddStrike(strike);
        return f;
    }

    /// Attach a Mac bitmap strike; draws at its pixel size use it instead of the TTF.
    public void AddStrike(MacBitmapFont strike) => _strikes[strike.PixelSize] = strike;

    /// The strike's line pitch at `size` (the Mac font height), or null if no strike matches.
    /// Lets TETextBox use the true Mac line spacing for bitmap text without altering TTF spacing.
    public int? StrikeLineHeight(int size) =>
        _strikes.TryGetValue(size, out var s) ? s.Height : null;

    private readonly struct Glyph
    {
        public readonly Rgba8Image? Bitmap; // null for whitespace / empty glyphs
        public readonly int Advance, OffX, OffY, W, H;
        public Glyph(Rgba8Image? bmp, int adv, int ox, int oy, int w, int h)
        { Bitmap = bmp; Advance = adv; OffX = ox; OffY = oy; W = w; H = h; }
    }

    private Glyph GetGlyph(int codepoint, int size)
    {
        long key = ((long)size << 32) | (uint)codepoint;
        if (_cache.TryGetValue(key, out var g)) return g;
        g = Build(codepoint, size);
        _cache[key] = g;
        return g;
    }

    private Glyph Build(int codepoint, int size)
    {
        if (_src is null) return new Glyph(null, 0, 0, 0, 0, 0);   // strike-only font: no TTF outlines
        var gidN = _src.GetGlyphId(codepoint);
        if (gidN is null) return new Glyph(null, 0, 0, 0, 0, 0);
        int gid = gidN.Value;
        _src.GetGlyphMetrics(gid, FssSize(size), out int advance, out int x0, out int y0, out int x1, out int y1);
        int w = x1 - x0, h = y1 - y0;
        if (w <= 0 || h <= 0) return new Glyph(null, advance, x0, y0, 0, 0);

        var bmp = new Rgba8Image(w, h);
        byte[] px = bmp.Pixels;
        if (Monochrome)
        {
            RasterizeMono(gid, size, x0, y0, w, h, px);
            return new Glyph(bmp, advance, x0, y0, w, h);
        }
        var cov = new byte[w * h];
        _src.RasterizeGlyphBitmap(gid, FssSize(size), cov, 0, w, h, w);
        for (int i = 0; i < cov.Length; i++)
        {
            byte a = cov[i];          // 8-bit coverage
            int o = i * 4;
            px[o] = a; px[o + 1] = a; px[o + 2] = a; px[o + 3] = a;  // premultiplied white
        }
        return new Glyph(bmp, advance, x0, y0, w, h);
    }

    // 1-bit scan conversion in the classic Mac TrueType scaler's style: sample the
    // outline (rasterized at 4×) at each output pixel's center, then a dropout-control
    // pass per center scanline — row and column — so an inside-run too thin or too
    // ill-phased to cover any pixel center still lights its nearest pixel. Plain
    // thresholding of the 1× antialiased raster can't do this: a 1px stem straddling
    // two columns is either dropped by a high threshold or doubled by a low one.
    private void RasterizeMono(int gid, int size, int x0, int y0, int w, int h, byte[] px)
    {
        _src!.GetGlyphMetrics(gid, FssSize(size) * 4, out _, out int qx0, out int qy0, out int qx1, out int qy1);
        int qw = qx1 - qx0, qh = qy1 - qy0;
        if (qw <= 0 || qh <= 0) return;
        var cov4 = new byte[qw * qh];
        _src.RasterizeGlyphBitmap(gid, FssSize(size) * 4, cov4, 0, qw, qh, qw);

        var on = new bool[w * h];
        // Output pixel (i, j) has its center at 4× subpixel (offU + 4i, offV + 4j).
        int offU = 4 * x0 + 2 - qx0, offV = 4 * y0 + 2 - qy0;
        bool Inside(int u, int v) => u >= 0 && u < qw && v >= 0 && v < qh && cov4[v * qw + u] >= 128;

        // Row pass: walk each output row's center scanline for inside-runs.
        for (int j = 0; j < h; j++)
        {
            int v = offV + 4 * j;
            if (v < 0 || v >= qh) continue;
            for (int u = 0; u < qw; )
            {
                if (!Inside(u, v)) { u++; continue; }
                int runStart = u;
                while (u < qw && Inside(u, v)) u++;
                MarkRun(on, w, h, runStart, u - 1, offU, j, true);
            }
        }
        // Column pass: same along each output column's center line (catches thin
        // horizontal bars sitting between row centers).
        for (int i = 0; i < w; i++)
        {
            int u = offU + 4 * i;
            if (u < 0 || u >= qw) continue;
            for (int v = 0; v < qh; )
            {
                if (!Inside(u, v)) { v++; continue; }
                int runStart = v;
                while (v < qh && Inside(u, v)) v++;
                MarkRun(on, w, h, runStart, v - 1, offV, i, false);
            }
        }

        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                if (!on[j * w + i]) continue;
                int o = (j * w + i) * 4;
                px[o] = 255; px[o + 1] = 255; px[o + 2] = 255; px[o + 3] = 255;
            }
    }

    // Light every output pixel whose center falls inside the run [a, b] (4× subpixel
    // coords along the scan axis); if none does, light the pixel nearest the run's
    // middle (the dropout-control rule). Runs shorter than 3 subpixels (< ¾px) with no
    // center are treated as stubs — corner/curve slivers TrueType's smart dropout mode
    // also skips; filling them fattened every corner. `fixedIdx` is the cross-axis index.
    private static void MarkRun(bool[] on, int w, int h, int a, int b, int off, int fixedIdx, bool isRow)
    {
        int n = isRow ? w : h;
        // centers along the axis sit at off + 4k
        int kFirst = (int)Math.Ceiling((a - off) / 4.0);
        int kLast = (int)Math.Floor((b - off) / 4.0);
        if (kFirst < 0) kFirst = 0;
        if (kLast >= n) kLast = n - 1;
        bool any = false;
        for (int k = kFirst; k <= kLast; k++) { Set(on, w, k, fixedIdx, isRow); any = true; }
        if (any) return;
        if (b - a + 1 < 3) return;   // stub — don't fill
        int kNear = (int)Math.Round(((a + b) / 2.0 - off) / 4.0);
        if (kNear < 0) kNear = 0;
        if (kNear >= n) kNear = n - 1;
        Set(on, w, kNear, fixedIdx, isRow);
    }

    private static void Set(bool[] on, int w, int k, int fixedIdx, bool isRow)
        => on[isRow ? fixedIdx * w + k : k * w + fixedIdx] = true;

    /// Distance from one baseline to the next at `size` (px).
    public int LineHeight(int size)
    {
        if (_strikes.TryGetValue(size, out var strike)) return strike.Height;
        if (_src is null) return size;
        lock (_lock)
        {
            _src.GetMetricsForSize(FssSize(size), out _, out _, out int lineHeight);
            return lineHeight;
        }
    }

    /// Pixels above the baseline at `size`.
    public int Ascent(int size)
    {
        if (_strikes.TryGetValue(size, out var strike)) return strike.Ascent;
        if (_src is null) return size;
        lock (_lock)
        {
            _src.GetMetricsForSize(FssSize(size), out int ascent, out _, out _);
            return ascent;
        }
    }

    /// Pixel width of `text` at `size` (sum of advances + kerning).
    public int MeasureWidth(string text, int size)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (_strikes.TryGetValue(size, out var strike))
        {
            int w = 0;
            foreach (char ch in text)
                if (strike.TryGetGlyph(ch, out var bg)) w += bg.Advance;
            return w;
        }
        if (_src is null) return 0;
        lock (_lock)
        {
            int x = 0, prev = 0;
            foreach (char ch in text)
            {
                int gid = _src.GetGlyphId(ch) ?? 0;
                if (prev != 0 && gid != 0) x += _src.GetGlyphKernAdvance(prev, gid, FssSize(size));
                x += GetGlyph(ch, size).Advance;
                prev = gid;
            }
            return x;
        }
    }

    /// Draw `text` with (x, y) as the TOP-LEFT of the text box (FontStashSharp's
    /// convention; the baseline sits at y + Ascent), tinted by `color`, at `size`.
    public void DrawText(Canvas canvas, string text, int x, int y, RgbaColor color, int size)
    {
        if (canvas is null || string.IsNullOrEmpty(text)) return;
        if (_strikes.TryGetValue(size, out var strike)) { DrawTextStrike(canvas, strike, text, x, y, color); return; }
        if (_src is null) return;
        lock (_lock)
        {
            _src.GetMetricsForSize(FssSize(size), out int ascent, out _, out _);
            int penX = x, baseY = y + ascent, prev = 0;
            foreach (char ch in text)
            {
                int gid = _src.GetGlyphId(ch) ?? 0;
                if (prev != 0 && gid != 0) penX += _src.GetGlyphKernAdvance(prev, gid, FssSize(size));
                var g = GetGlyph(ch, size);
                if (g.Bitmap is not null)
                {
                    canvas.BlitGlyph(g.Bitmap,
                        new RectI(penX + g.OffX, baseY + g.OffY, g.W, g.H),
                        new RectI(0, 0, g.W, g.H), color);
                }
                penX += g.Advance;
                prev = gid;
            }
        }
    }

    // Bitmap-strike draw: blit each 1-bit glyph at its native pixel grid (no anti-aliasing),
    // baseline at y + the strike's ascent. Same (x,y)=top-left convention as the TTF path.
    private static void DrawTextStrike(Canvas canvas, MacBitmapFont strike, string text, int x, int y, RgbaColor color)
    {
        int penX = x, baseY = y + strike.Ascent;
        foreach (char ch in text)
        {
            if (!strike.TryGetGlyph(ch, out var g)) continue;
            if (g.Bitmap is not null)
                canvas.BlitGlyph(g.Bitmap,
                    new RectI(penX + g.OffX, baseY + g.OffY, g.W, g.H),
                    new RectI(0, 0, g.W, g.H), color);
            penX += g.Advance;
        }
    }
}
