using System;
using System.IO;

namespace OpenEV.Platform.Imaging;

// Encodes an Rgba8Image as a classic Mac 'PICT' v2 resource.
//
// Encode8 (the one the editor's image import uses) writes an 8-bit indexed PackBitsRect — the format
// EVERY in-game ship/sprite sheet uses. For sprites to render correctly in the ORIGINAL engine three
// things must all be right (each was a separate failed attempt during the investigation, see the
// project memory):
//   1. 8-bit indexed (not 16-bit direct) — the sprite pipeline is 8-bit.
//   2. the EXTENDED v2 picture header (version word 0xFFFE, not 0xFFFFFFFF) — real QuickDraw is strict.
//   3. colours from the game's master palette (clut 1001) — the 8-bit game remaps every sprite to it,
//      so off-palette colours shift; pass that palette in and the remap becomes an identity.
//
// Encode16 keeps a 16-bit RGB555 path (now with the corrected header) for completeness.
public static class PictEncoder
{
    private static readonly byte[] InverseGamma = BuildInverseGamma();

    /// <summary>8-bit indexed PICT. If <paramref name="palette"/> (flat RGB triples, ≤256 colours,
    /// e.g. the game's clut 1001) is given, pixels are mapped to the nearest entry and that palette is
    /// embedded as the colour table; otherwise a per-image palette is built by median cut.</summary>
    public static byte[] Encode8(Rgba8Image img, byte[]? palette = null)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        int w = img.Width, h = img.Height;
        if (w <= 0 || h <= 0) throw new ArgumentException("empty image");

        // ── choose a palette and map every pixel to an index (shared with CicnEncoder) ──
        var pal = PaletteQuantizer.BuildIndexMap(img, palette, out byte[] index);

        int rowBytes = w + (w & 1);   // 8-bit: one byte per pixel, padded to even
        using var ms = new MemoryStream();
        U16(ms, 0);                                   // picSize (patched at the end)
        Rect(ms, 0, 0, h, w);                         // picFrame
        U32(ms, 0x001102FF);                          // VersionOp + version 2
        U16(ms, 0x0C00); U16(ms, 0xFFFE); U16(ms, 0); // HeaderOp + EXTENDED v2 header (0xFFFE)
        U32(ms, 0x00480000); U32(ms, 0x00480000);     // hRes / vRes = 72 dpi
        Rect(ms, 0, 0, h, w); U32(ms, 0);             // optimal source rect + reserved
        U16(ms, 0x0001); U16(ms, 0x000A); Rect(ms, 0, 0, h, w); // clip region (matches base sprites)

        U16(ms, 0x0098);                              // PackBitsRect
        U16(ms, (ushort)(rowBytes | 0x8000));         // rowBytes + PixMap flag
        Rect(ms, 0, 0, h, w);                         // bounds
        U16(ms, 0);                                   // pmVersion
        U16(ms, 0);                                   // packType = 0 (default → PackBits)
        U32(ms, 0);                                   // packSize
        U32(ms, 0x00480000); U32(ms, 0x00480000);     // hRes / vRes
        U16(ms, 0);                                   // pixelType = indexed
        U16(ms, 8);                                   // pixelSize
        U16(ms, 1);                                   // cmpCount
        U16(ms, 8);                                   // cmpSize
        U32(ms, 0); U32(ms, 0); U32(ms, 0);           // planeBytes / pmTable / pmReserved
        // ── colour table ──
        U32(ms, 0x00010000);                          // ctSeed (non-zero; 0 can be mishandled)
        U16(ms, 0);                                   // ctFlags
        U16(ms, (ushort)(pal.Length - 1));            // ctSize = colours-1
        for (int i = 0; i < pal.Length; i++)
        {
            U16(ms, (ushort)i);
            U16(ms, (ushort)(pal[i].R * 257)); U16(ms, (ushort)(pal[i].G * 257)); U16(ms, (ushort)(pal[i].B * 257));
        }
        Rect(ms, 0, 0, h, w); Rect(ms, 0, 0, h, w); U16(ms, 0); // src / dst / mode

        byte[] rowBuf = new byte[rowBytes];
        for (int y = 0; y < h; y++)
        {
            Array.Clear(rowBuf, 0, rowBytes);
            Array.Copy(index, y * w, rowBuf, 0, w);
            if (rowBytes < 8)
            {
                ms.Write(rowBuf, 0, rowBytes);   // packType-0 rows < 8 bytes are stored uncompressed
            }
            else
            {
                byte[] packed = PackBytes(rowBuf);
                if (rowBytes > 250) U16(ms, (ushort)packed.Length); else ms.WriteByte((byte)packed.Length);
                ms.Write(packed, 0, packed.Length);
            }
        }
        U16(ms, 0x00FF);                              // end of picture

        byte[] outBytes = ms.ToArray();
        outBytes[0] = (byte)(outBytes.Length >> 8); outBytes[1] = (byte)outBytes.Length; // picSize = low word of len
        return outBytes;
    }

    public static byte[] Encode16(Rgba8Image img)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        int w = img.Width, h = img.Height;
        if (w <= 0 || h <= 0) throw new ArgumentException("empty image");

        int rowBytes = w * 2;
        using var ms = new MemoryStream();
        U16(ms, 0);                           // picSize (patched at end)
        Rect(ms, 0, 0, h, w);                 // picFrame
        U32(ms, 0x001102FF);                  // VersionOp + version 2
        U16(ms, 0x0C00); U16(ms, 0xFFFE); U16(ms, 0); // HeaderOp + EXTENDED v2 header (0xFFFE)
        U32(ms, 0x00480000); U32(ms, 0x00480000);
        Rect(ms, 0, 0, h, w); U32(ms, 0);

        U16(ms, 0x009A);                      // DirectBitsRect
        U32(ms, 0x000000FF);                  // baseAddr (sentinel)
        U16(ms, (ushort)(rowBytes | 0x8000)); // rowBytes + PixMap flag
        Rect(ms, 0, 0, h, w);                 // bounds
        U16(ms, 0);                           // pmVersion
        U16(ms, 3);                           // packType = 3 (PackBits on 16-bit words)
        U32(ms, 0);
        U32(ms, 0x00480000); U32(ms, 0x00480000);
        U16(ms, 16); U16(ms, 16); U16(ms, 3); U16(ms, 5);   // RGBDirect, 16bpp, RGB555
        U32(ms, 0); U32(ms, 0); U32(ms, 0);
        Rect(ms, 0, 0, h, w); Rect(ms, 0, 0, h, w); U16(ms, 0);

        var px = img.Pixels;
        var row = new ushort[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 4;
                row[x] = (ushort)(((InverseGamma[px[o]] >> 3) << 10) | ((InverseGamma[px[o + 1]] >> 3) << 5) | (InverseGamma[px[o + 2]] >> 3));
            }
            byte[] packed = PackWords(row);
            if (rowBytes > 250) U16(ms, (ushort)packed.Length); else ms.WriteByte((byte)packed.Length);
            ms.Write(packed, 0, packed.Length);
        }
        U16(ms, 0x00FF);

        byte[] outBytes = ms.ToArray();
        outBytes[0] = (byte)(outBytes.Length >> 8); outBytes[1] = (byte)outBytes.Length;
        return outBytes;
    }

    // ── PackBits on bytes (8-bit rows) — inverse of PackBitsDecompressor.Unpack ──
    private static byte[] PackBytes(byte[] row)
    {
        using var ms = new MemoryStream(row.Length + row.Length / 64 + 4);
        int n = row.Length, i = 0;
        while (i < n)
        {
            int run = 1;
            while (i + run < n && row[i + run] == row[i] && run < 128) run++;
            if (run >= 2) { ms.WriteByte((byte)(sbyte)(-(run - 1))); ms.WriteByte(row[i]); i += run; }
            else
            {
                int start = i, lit = 0;
                while (i < n && lit < 128) { if (i + 1 < n && row[i] == row[i + 1]) break; lit++; i++; }
                ms.WriteByte((byte)(lit - 1));
                ms.Write(row, start, lit);
            }
        }
        return ms.ToArray();
    }

    // ── PackBits on 16-bit words — inverse of PackBitsDecompressor.UnpackWords ──
    private static byte[] PackWords(ushort[] words)
    {
        using var ms = new MemoryStream(words.Length * 2 + words.Length / 64 + 4);
        int n = words.Length, i = 0;
        while (i < n)
        {
            int run = 1;
            while (i + run < n && words[i + run] == words[i] && run < 128) run++;
            if (run >= 2) { ms.WriteByte((byte)(sbyte)(-(run - 1))); U16(ms, words[i]); i += run; }
            else
            {
                int start = i, lit = 0;
                while (i < n && lit < 128) { if (i + 1 < n && words[i] == words[i + 1]) break; lit++; i++; }
                ms.WriteByte((byte)(lit - 1));
                for (int k = 0; k < lit; k++) U16(ms, words[start + k]);
            }
        }
        return ms.ToArray();
    }

    private static byte[] BuildInverseGamma()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
            lut[i] = (byte)Math.Clamp(Math.Round(Math.Pow(i / 255.0, 2.2 / 1.8) * 255.0), 0, 255);
        return lut;
    }

    private static void U16(Stream s, ushort v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void U32(Stream s, uint v)
    { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void Rect(Stream s, int top, int left, int bottom, int right)
    { U16(s, (ushort)(short)top); U16(s, (ushort)(short)left); U16(s, (ushort)(short)bottom); U16(s, (ushort)(short)right); }
}
