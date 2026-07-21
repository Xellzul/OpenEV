#:project ../src/OpenEV.Platform.ResourceFork

// Extract classic Mac bitmap font strikes (FONT/NFNT) from a font suitcase's
// resource fork (SheepShaver ExtFS .rsrc layout) into BDF files our
// MacBitmapFont parser reads, plus any 'sfnt' TrueType outlines.
// Run (net10 file-based app):  dotnet run tools/ExtractMacFontSuitcase.cs -- <suitcasePath> <outDir>
// The extracted Family-N.bdf strikes + the sfnt .ttf go into
// <exe dir>/Fonts/ (rename the sfnt to chicago.ttf / geneva.ttf)
// where SystemFontV2/GenevaFontV2 prefer them over the bundled free fonts.
// The strikes stay LOCAL — Apple-proprietary, never committed/bundled.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenEV.Platform.ResourceFork;

string suitcase = args[0];
string outDir = args[1];
Directory.CreateDirectory(outDir);

var fork = MacForkFile.ReadFork(suitcase);
var resources = MacResourceFork.Read(fork);
string family = Path.GetFileName(suitcase).Replace(' ', '_');

Console.WriteLine($"{suitcase}: {resources.Count} resources");
foreach (var r in resources)
    Console.WriteLine($"  {r.TypeCode} id={r.Id} name='{r.Name}' {r.Data.Length} bytes");

static ushort U16(byte[] d, int o) => (ushort)((d[o] << 8) | d[o + 1]);
static short S16(byte[] d, int o) => (short)((d[o] << 8) | d[o + 1]);

// FOND association table: which strike ids exist per (size, style).
var strikes = new List<(int size, int style, int id)>();
foreach (var r in resources)
{
    if (r.TypeCode != "FOND") continue;
    var d = r.Data;
    int off = 52;                       // FOND header up to ffVersion (IM:Text 4-90)
    int n = S16(d, off) + 1;            // numAssoc is count-1
    off += 2;
    for (int i = 0; i < n; i++, off += 6)
        strikes.Add((S16(d, off), S16(d, off + 2), S16(d, off + 4)));
}
Console.WriteLine("FOND associations: " + string.Join(", ", strikes.ConvertAll(s => $"size {s.size} style {s.style} -> id {s.id}")));

foreach (var (size, style, id) in strikes)
{
    if (size == 0) continue;            // size 0 = outline (sfnt) association
    if (style != 0) continue;           // plain only
    var res = resources.Find(r => (r.TypeCode == "NFNT" || r.TypeCode == "FONT") && r.Id == id);
    if (res is null) { Console.WriteLine($"  (strike id {id} for {size}px not in file)"); continue; }
    var d = res.Data;

    int firstChar = S16(d, 2), lastChar = S16(d, 4);
    short kernMax = S16(d, 8);
    int fRectHeight = S16(d, 14);
    int owTLoc = U16(d, 16);            // WORDS from the owTLoc field (offset 16) to the ow table
    int ascent = S16(d, 18), descent = S16(d, 20), leading = S16(d, 22);
    int rowWords = S16(d, 24);
    int bitImage = 26;
    int rowBytesSrc = rowWords * 2;
    int locTable = bitImage + rowBytesSrc * fRectHeight;
    int owTable = 16 + owTLoc * 2;
    int nChars = lastChar - firstChar + 1;   // + missing-glyph entry after these

    var sb = new StringBuilder();
    var glyphs = new StringBuilder();
    int nGlyphs = 0;
    for (int i = 0; i < nChars; i++)
    {
        int enc = firstChar + i;
        if (enc is < 0 or > 255) continue;
        ushort ow = U16(d, owTable + i * 2);
        if (ow == 0xffff) continue;          // missing char
        int advance = ow & 0xff;
        int offset = ow >> 8;                // unsigned; glyph origin = kernMax + offset
        int loc0 = U16(d, locTable + i * 2), loc1 = U16(d, locTable + (i + 1) * 2);
        int gw = loc1 - loc0;

        // Extract the glyph columns from the strike image; trim empty top/bottom rows.
        var rows = new ulong[fRectHeight];
        int minR = int.MaxValue, maxR = -1;
        for (int y = 0; y < fRectHeight; y++)
        {
            ulong v = 0;
            for (int x = 0; x < gw; x++)
            {
                int sx = loc0 + x;
                if ((d[bitImage + y * rowBytesSrc + (sx >> 3)] & (0x80 >> (sx & 7))) != 0)
                    v |= 1UL << (63 - x);
                }
            rows[y] = v;
            if (v != 0) { if (y < minR) minR = y; if (y > maxR) maxR = y; }
        }
        int bh = maxR >= 0 ? maxR - minR + 1 : 0;
        int bw = maxR >= 0 ? gw : 0;
        int bx = kernMax + offset;
        int by = maxR >= 0 ? ascent - (maxR + 1) : 0;   // BDF yoff = baseline-relative bottom (row `ascent` is 1 below baseline)

        glyphs.AppendLine($"STARTCHAR C{enc:X2}");
        glyphs.AppendLine($"ENCODING {enc}");
        glyphs.AppendLine($"SWIDTH {advance * 1000 / Math.Max(1, size)} 0");
        glyphs.AppendLine($"DWIDTH {advance} 0");
        glyphs.AppendLine($"BBX {bw} {bh} {bx} {by}");
        glyphs.AppendLine("BITMAP");
        int outRb = Math.Max(1, (bw + 7) / 8);
        for (int y = 0; y < bh; y++)
        {
            ulong v = rows[minR + y];
            // top 64-bit-aligned bits → hex string of outRb bytes (MSB-first)
            var hex = new StringBuilder(outRb * 2);
            for (int b = 0; b < outRb; b++)
                hex.Append(((byte)(v >> (56 - b * 8))).ToString("X2"));
            glyphs.AppendLine(hex.ToString());
        }
        glyphs.AppendLine("ENDCHAR");
        nGlyphs++;
    }

    sb.AppendLine("STARTFONT 2.1");
    sb.AppendLine($"COMMENT extracted from Mac font suitcase '{Path.GetFileName(suitcase)}' {res.TypeCode} id {id} ({size}px)");
    sb.AppendLine($"FONT -apple-{family}-Medium-R-Normal--{size}-{size * 10}-75-75-P-70-MacRoman-0");
    sb.AppendLine($"SIZE {size} 75 75");
    sb.AppendLine($"FONTBOUNDINGBOX {size} {ascent + descent} 0 -{descent}");
    sb.AppendLine("STARTPROPERTIES 3");
    sb.AppendLine($"FONT_ASCENT {ascent}");
    // + leading: our consumer's Height (= ascent+descent) is the LINE PITCH, and the
    // Mac pitch is ascent+descent+leading — fold leading in so multiline spacing matches.
    sb.AppendLine($"FONT_DESCENT {descent + leading}");
    sb.AppendLine($"PIXEL_SIZE {size}");
    sb.AppendLine("ENDPROPERTIES");
    sb.AppendLine($"CHARS {nGlyphs}");
    sb.Append(glyphs);
    sb.AppendLine("ENDFONT");
    string outPath = Path.Combine(outDir, $"{family}-{size}.bdf");
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"wrote {outPath} ({nGlyphs} glyphs, ascent {ascent} descent {descent} leading {leading})");
}

// TrueType outlines in the suitcase ('sfnt').
foreach (var r in resources)
    if (r.TypeCode == "sfnt")
    {
        string outPath = Path.Combine(outDir, $"{family.ToLowerInvariant()}-sfnt-{r.Id}.ttf");
        File.WriteAllBytes(outPath, r.Data);
        Console.WriteLine($"wrote {outPath} ({r.Data.Length} bytes)");
    }
