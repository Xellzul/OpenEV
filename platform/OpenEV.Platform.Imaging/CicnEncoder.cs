using System;
using System.IO;

namespace OpenEV.Platform.Imaging;

// Encodes an Rgba8Image as a classic Mac 'cicn' (Color Icon) resource — the inverse of CicnDecoder.
//
// Editor-only tooling: the original game READS cicn resources, it never writes them, so (like
// PictEncoder) this has no decompiled-source equivalent. Correctness is defined by round trip — the
// bytes produced here must decode back through CicnDecoder to the same image.
//
// Layout (mirrors CicnDecoder / Inside Macintosh):
//   PixMap(50) iconMask BitMap(14) iconBMap BitMap(14) iconData handle(4)
//   mask data (1bpp) · iconBMap data (1bpp) · ColorTable · pixel data (raw 8-bit indices, NOT packed)
// Encoded 8-bit indexed and quantised to the game master palette (clut 1001), so the in-game 8-bit
// remap is an identity — the same choice PictEncoder.Encode8 makes for sprites.
public static class CicnEncoder
{
    /// <summary>8-bit indexed 'cicn'. <paramref name="palette"/> (flat RGB triples, ≤256 colours, e.g.
    /// the game's clut 1001) is embedded as the colour table and pixels are mapped to it; without it a
    /// per-image palette is built by median cut. Alpha &lt; 128 becomes a transparent (mask-off) pixel.</summary>
    public static byte[] Encode8(Rgba8Image img, byte[]? palette = null)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        int w = img.Width, h = img.Height;
        if (w <= 0 || h <= 0) throw new ArgumentException("empty image");
        // CicnDecoder caps dimensions at 512; refuse anything larger rather than write an unreadable icon.
        if (w > 512 || h > 512) throw new ArgumentException("cicn dimensions must be ≤ 512");

        var pal = PaletteQuantizer.BuildIndexMap(img, palette, out byte[] index);
        var px = img.Pixels;

        int pmRowBytes = w + (w & 1);         // 8-bit: one byte per pixel, padded to even
        int bitRowBytes = ((w + 15) / 16) * 2; // 1-bit bitmaps: word-aligned (mask + iconBMap)

        using var ms = new MemoryStream();

        // ── PixMap (50 bytes) ──
        U32(ms, 0);                              // baseAddr
        U16(ms, (ushort)(pmRowBytes | 0x8000));  // rowBytes + PixMap flag
        Rect(ms, 0, 0, h, w);                     // bounds
        U16(ms, 0);                               // pmVersion
        U16(ms, 0);                               // packType = 0 (unpacked)
        U32(ms, 0);                               // packSize
        U32(ms, 0x00480000); U32(ms, 0x00480000); // hRes / vRes = 72 dpi
        U16(ms, 0);                               // pixelType = indexed
        U16(ms, 8);                               // pixelSize
        U16(ms, 1);                               // cmpCount
        U16(ms, 8);                               // cmpSize
        U32(ms, 0); U32(ms, 0); U32(ms, 0);       // planeBytes / pmTable / pmReserved

        // ── iconMask BitMap (14) + iconBMap BitMap (14): both share the PixMap bounds ──
        U32(ms, 0); U16(ms, (ushort)bitRowBytes); Rect(ms, 0, 0, h, w); // mask
        U32(ms, 0); U16(ms, (ushort)bitRowBytes); Rect(ms, 0, 0, h, w); // 1-bit icon

        U32(ms, 0);                               // iconData handle (NULL)

        // ── mask data: 1bpp, MSB-first, bit = 1 → opaque ──
        var bitRow = new byte[bitRowBytes];
        for (int y = 0; y < h; y++)
        {
            Array.Clear(bitRow, 0, bitRowBytes);
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] >= 128) bitRow[x >> 3] |= (byte)(0x80 >> (x & 7));
            ms.Write(bitRow, 0, bitRowBytes);
        }
        // ── iconBMap data: best-effort 1-bit B&W companion (bit = 1 → dark, within the mask). The decoder
        //    skips this, but a real cicn carries a 1-bit fallback icon, so emit a sensible one. ──
        for (int y = 0; y < h; y++)
        {
            Array.Clear(bitRow, 0, bitRowBytes);
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 4;
                if (px[o + 3] < 128) continue;       // transparent → leave white
                int lum = (px[o] * 30 + px[o + 1] * 59 + px[o + 2] * 11) / 100;
                if (lum < 128) bitRow[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
            ms.Write(bitRow, 0, bitRowBytes);
        }

        // ── ColorTable: raw palette × 257 (CicnDecoder re-applies display gamma, like PictEncoder.Encode8) ──
        U32(ms, 0x00010000);                      // ctSeed (non-zero)
        U16(ms, 0);                               // ctFlags
        U16(ms, (ushort)(pal.Length - 1));        // ctSize = colours - 1
        for (int i = 0; i < pal.Length; i++)
        {
            U16(ms, (ushort)i);
            U16(ms, (ushort)(pal[i].R * 257)); U16(ms, (ushort)(pal[i].G * 257)); U16(ms, (ushort)(pal[i].B * 257));
        }

        // ── pixel data: raw 8-bit indices, pmRowBytes per row ──
        var pixRow = new byte[pmRowBytes];
        for (int y = 0; y < h; y++)
        {
            Array.Clear(pixRow, 0, pmRowBytes);
            Array.Copy(index, y * w, pixRow, 0, w);
            ms.Write(pixRow, 0, pmRowBytes);
        }

        return ms.ToArray();
    }

    private static void U16(Stream s, ushort v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void U32(Stream s, uint v)
    { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void Rect(Stream s, int top, int left, int bottom, int right)
    { U16(s, (ushort)(short)top); U16(s, (ushort)(short)left); U16(s, (ushort)(short)bottom); U16(s, (ushort)(short)right); }
}
