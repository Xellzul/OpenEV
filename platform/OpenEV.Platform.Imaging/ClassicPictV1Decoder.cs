using System;
using System.Buffers;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

// PICT v1 decoder.
//
// EVO uses two distinct PICT v1 shapes:
//   1) 1-bit sprite masks — a single small BitMap covering the whole
//      pic frame. Used by EV's sprite asset pipeline.
//   2) Composite background art (PICT 132 = "Keys" prefs panel) —
//      MULTIPLE BitMaps stacked into one image. Each BitsRect /
//      PackBitsRect opcode has its own bounds + dstRect; the dstRect
//      tells us WHERE in the composite image to place that strip.
//
// Mask convention: QuickDraw bit==1 means black (foreground); EV's
// masks use black=transparent / white=opaque (the legacy single-bitmap
// case). For multi-bitmap composites we paint white/opaque pixels and
// leave background transparent so later strips layer on top correctly.
public static class ClassicPictV1Decoder
{
    public static Rgba8Image? Decode(ref BigEndianSpanReader reader, int width, int height, string? tag)
    {
        Rgba8Image? image = null;
        int bitmapCount = 0;

        while (reader.Remaining > 0)
        {
            int opPos = reader.Position;
            byte opcode = reader.ReadByte();
            switch (opcode)
            {
                case 0x00: break;
                case 0xFF:
                    DecodeDiagnostics.Log(tag, $"v1 EOP at 0x{opPos:X4} after {bitmapCount} bitmap(s)");
                    return image;
                case 0x01:
                    if (reader.Remaining < 2) return image;
                    int clipLen = reader.ReadUInt16() - 2;
                    if (clipLen < 0 || clipLen > reader.Remaining) return image;
                    reader.Skip(clipLen); break;
                case 0x11:
                    if (reader.Remaining < 1) return image;
                    reader.Skip(1); break;
                case 0x90: case 0x91: case 0x98: case 0x99:
                    DecodeDiagnostics.Log(tag, $"v1 bitmap 0x{opcode:X2} {width}x{height} (strip {bitmapCount})");
                    image ??= new Rgba8Image(width, height);
                    if (!DecodeBitMapInto(ref reader, image, opcode, tag)) return image;
                    bitmapCount++;
                    break;
                case 0xA0:
                    if (reader.Remaining < 2) return image;
                    reader.Skip(2); break;
                case 0xA1:
                    if (reader.Remaining < 4) return image;
                    reader.Skip(2);
                    int lcLen = reader.ReadUInt16();
                    if (lcLen > reader.Remaining) return image;
                    reader.Skip(lcLen); break;
                default:
                    DecodeDiagnostics.Log(tag, $"v1 unknown opcode 0x{opcode:X2} at 0x{opPos:X4}, returning {bitmapCount} bitmap(s)");
                    return image;
            }
        }
        return image;
    }

    /// Decode one BitsRect / PackBitsRect opcode into `image` at its
    /// dstRect. Returns false on data error (the caller should bail).
    /// Internal: QuickDrawBitmapDecoder reuses this for old-style BitMaps
    /// (rowBytes MSB clear) embedded in PICT v2 opcode streams — the record
    /// layout is identical there.
    internal static bool DecodeBitMapInto(ref BigEndianSpanReader reader, Rgba8Image image, byte opcode, string? tag)
    {
        if (reader.Remaining < 2) return false;
        short rowBytes = reader.ReadInt16();
        if (rowBytes < 0) { Log(tag, "v1: PixMap not supported in this decoder"); return false; }

        // Read header rects + mode. Mac BitMap layout:
        //   bounds   (top, left, bottom, right) — int16 ×4 = 8 bytes
        //   srcRect  — 8 bytes
        //   dstRect  — 8 bytes
        //   mode     — int16 (2 bytes)
        if (reader.Remaining < 8 + 8 + 8 + 2) return false;
        short boundsTop    = reader.ReadInt16();
        short boundsLeft   = reader.ReadInt16();
        short boundsBottom = reader.ReadInt16();
        short boundsRight  = reader.ReadInt16();
        short srcTop       = reader.ReadInt16();
        short srcLeft      = reader.ReadInt16();
        short srcBottom    = reader.ReadInt16();
        short srcRight     = reader.ReadInt16();
        short dstTop       = reader.ReadInt16();
        short dstLeft      = reader.ReadInt16();
        short dstBottom    = reader.ReadInt16();
        short dstRight     = reader.ReadInt16();
        reader.Skip(2);   // mode (srcCopy etc., ignored)

        if (opcode == 0x91 || opcode == 0x99)
        {
            // BitsRgn / PackBitsRgn — region data immediately follows.
            if (reader.Remaining < 2) return false;
            int rgnSize = reader.ReadUInt16();
            if (rgnSize < 2 || rgnSize - 2 > reader.Remaining) return false;
            reader.Skip(rgnSize - 2);
        }

        int dataRows = boundsBottom - boundsTop;
        int dataCols = boundsRight  - boundsLeft;
        if (dataRows <= 0 || dataCols <= 0) return true;

        bool packed = opcode == 0x98 || opcode == 0x99;
        // Mac stores rows uncompressed when rowBytes < 8; otherwise
        // PackBits per row with a length prefix (byte if rowBytes < 250,
        // else int16).
        bool defaultPacked = packed && rowBytes >= 8;

        byte[] rowBuffer = ArrayPool<byte>.Shared.Rent(Math.Max((int)rowBytes, 1));
        try
        {
            for (int y = 0; y < dataRows; y++)
            {
                Span<byte> rowData = rowBuffer.AsSpan(0, rowBytes);
                rowData.Clear();
                if (!defaultPacked)
                {
                    if (reader.Remaining < rowBytes) return false;
                    reader.ReadBytes(rowBytes).CopyTo(rowData);
                }
                else
                {
                    if (reader.Remaining < 1) return false;
                    int len = rowBytes >= 250
                        ? (reader.Remaining >= 2 ? reader.ReadUInt16() : 0)
                        : reader.ReadByte();
                    if (len > 0 && reader.Remaining >= len)
                        PackBitsDecompressor.Unpack(reader.ReadBytes(len), rowData);
                }

                // Composite this row into the destination image at
                // dstRect. The Mac would scale (dataCols → dstW) and
                // (dataRows → dstH) — for our purposes EVO's PICT 132
                // strips have dstRect == srcRect so we 1:1 copy.
                int dstY = dstTop + (y * (dstBottom - dstTop)) / dataRows;
                if (dstY < 0 || dstY >= image.Height) continue;
                int copyCols = Math.Min(dataCols, dstRight - dstLeft);
                for (int x = 0; x < copyCols; x++)
                {
                    int byteIdx = x >> 3;
                    if (byteIdx >= rowData.Length) break;
                    int bit = (rowData[byteIdx] >> (7 - (x & 7))) & 1;
                    int destX = dstLeft + x;
                    if (destX < 0 || destX >= image.Width) continue;
                    // QuickDraw: bit 1 = black foreground, bit 0 = white
                    // (or background colour). For composite art we paint
                    // both states with opacity so strips can stack.
                    if (bit == 1) image.SetPixel(destX, dstY, 0, 0, 0, 255);
                    else          image.SetPixel(destX, dstY, 255, 255, 255, 255);
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(rowBuffer); }
        return true;
    }

    private static void Log(string? tag, string m) =>
        Console.WriteLine(tag is null ? $"  [WARN] ClassicPictV1: {m}" : $"  [WARN] {tag}: {m}");
}
