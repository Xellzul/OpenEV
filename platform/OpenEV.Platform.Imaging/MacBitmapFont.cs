using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenEV.Platform.Imaging;

// One classic-Mac bitmap font strike (a single pixel size), parsed from a BDF file —
// the form `fondu` emits from a Mac NFNT/FONT resource. This reproduces a 1-bit Mac
// screen font (e.g. Geneva 9) pixel-for-pixel, which the anti-aliased outline path
// (SoftwareFont over a TTF) cannot. SoftwareFont can carry one of these per size and
// prefer it over the TTF when the requested size matches (see SoftwareFont.AddStrike).
//
// Glyphs are keyed by the Unicode char their BDF ENCODING (a Mac Roman byte) maps to,
// so lookups take the already-decoded C# string chars directly.
public sealed class MacBitmapFont
{
    public readonly struct Glyph
    {
        public readonly Rgba8Image? Bitmap;   // premultiplied (set px = 255,255,255,255; clear = 0); null when blank
        public readonly int Advance, OffX, OffY, W, H;
        public Glyph(Rgba8Image? bmp, int advance, int offX, int offY, int w, int h)
        { Bitmap = bmp; Advance = advance; OffX = offX; OffY = offY; W = w; H = h; }
    }

    // CP 10000 = Mac Roman: maps each glyph's BDF ENCODING byte to the Unicode char the styled
    // text was decoded to (so lookups take decoded string chars directly). The provider is
    // registered by OpenEV.Platform.EvoData's module init; if it is somehow unavailable, fall back to
    // identity (correct for the ASCII range, which covers the register's body/field text).
    private static readonly Encoding? MacRoman = TryGetMacRoman();
    private static Encoding? TryGetMacRoman()
    {
        try { return Encoding.GetEncoding(10000); } catch { return null; }
    }

    private readonly Dictionary<char, Glyph> _glyphs = new();

    public int PixelSize { get; private set; }
    public int Ascent { get; private set; }
    public int Descent { get; private set; }
    public int Height => Ascent + Descent;

    public bool TryGetGlyph(char ch, out Glyph g) => _glyphs.TryGetValue(ch, out g);

    // One already-rasterized glyph for FromGlyphs: OffX/OffY follow the Glyph convention
    // (OffY = bitmap top relative to baseline, negative above). Bitmap is premultiplied-white
    // (as AddGlyph builds), or null for a blank glyph (space).
    public readonly record struct GlyphSpec(char Ch, int Advance, int OffX, int OffY, int W, int H, Rgba8Image? Bitmap);

    // Build a strike from glyphs a non-BDF source already rasterized (e.g. a runtime hinter),
    // reusing the same keying + Glyph layout the BDF path produces.
    public static MacBitmapFont FromGlyphs(int ascent, int descent, int pixelSize, IEnumerable<GlyphSpec> glyphs)
    {
        var font = new MacBitmapFont { Ascent = ascent, Descent = descent, PixelSize = pixelSize };
        foreach (var s in glyphs)
            font._glyphs[s.Ch] = new Glyph(s.Bitmap, s.Advance, s.OffX, s.OffY, s.W, s.H);
        return font;
    }

    public static MacBitmapFont FromBdfFile(string path) => FromBdf(File.ReadAllLines(path));
    public static MacBitmapFont FromBdfBytes(byte[] bytes) =>
        FromBdf(Encoding.Latin1.GetString(bytes).Split('\n'));

    private static MacBitmapFont FromBdf(string[] lines)
    {
        var font = new MacBitmapFont();
        int enc = -1, dwidth = 0, bw = 0, bh = 0, bx = 0, by = 0;
        List<long>? rows = null;   // long: a BDF row is ceil(w/8)*8 bits; >16px-wide strikes overflow int
        bool inBitmap = false;

        foreach (var raw in lines)
        {
            string line = raw.TrimEnd('\r');
            if (inBitmap)
            {
                if (line.StartsWith("ENDCHAR", StringComparison.Ordinal))
                {
                    inBitmap = false;
                    font.AddGlyph(enc, dwidth, bw, bh, bx, by, rows!);
                    rows = null;
                }
                else if (line.Length > 0)
                {
                    rows!.Add(long.Parse(line.Trim(), NumberStyles.HexNumber));
                }
                continue;
            }

            var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            switch (p[0])
            {
                case "FONT_ASCENT":  font.Ascent    = int.Parse(p[1]); break;
                case "FONT_DESCENT": font.Descent   = int.Parse(p[1]); break;
                case "PIXEL_SIZE":   font.PixelSize = int.Parse(p[1]); break;
                case "STARTCHAR":    enc = -1; dwidth = 0; bw = bh = bx = by = 0; break;
                case "ENCODING":     enc = int.Parse(p[1]); break;
                case "DWIDTH":       dwidth = int.Parse(p[1]); break;
                case "BBX":          bw = int.Parse(p[1]); bh = int.Parse(p[2]); bx = int.Parse(p[3]); by = int.Parse(p[4]); break;
                case "BITMAP":       inBitmap = true; rows = new List<long>(Math.Max(0, bh)); break;
            }
        }
        if (font.PixelSize == 0) font.PixelSize = font.Height;
        return font;
    }

    private void AddGlyph(int enc, int dwidth, int w, int h, int xoff, int yoff, List<long> rows)
    {
        if (enc is < 0 or > 255) return;                 // Mac Roman byte only
        char ch = MacRoman is not null ? MacRoman.GetString(new[] { (byte)enc })[0] : (char)enc;

        Rgba8Image? bmp = null;
        if (w > 0 && h > 0 && rows.Count >= h)
        {
            int rowBytes = (w + 7) / 8;
            bmp = new Rgba8Image(w, h);
            byte[] px = bmp.Pixels;
            for (int r = 0; r < h; r++)
            {
                long val = rows[r];
                for (int c = 0; c < w; c++)
                {
                    if (((val >> (rowBytes * 8 - 1 - c)) & 1L) == 0) continue;
                    int o = (r * w + c) * 4;
                    px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 255;   // premultiplied white = full coverage
                }
            }
        }
        // Bitmap top relative to the baseline (negative = above), matching SoftwareFont's
        // OffY convention (drawn at baseY + OffY). BDF (xoff,yoff) is the lower-left corner.
        _glyphs[ch] = new Glyph(bmp, dwidth, xoff, -(yoff + h), w, h);
    }
}
