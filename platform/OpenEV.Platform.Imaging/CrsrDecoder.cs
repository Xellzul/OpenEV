using System;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

// 'crsr' (Color Cursor). Fixed 16x16 header (crsrType, offsets to the colour PixMap/pixel data,
// the classic 1-bit crsrMask + crsrBits, and the hotspot), followed by the colour PixMap record +
// ColorTable + pixel data the header's offsets point at (same PixMap/ColorTable shape as 'cicn',
// see CicnDecoder). Unlike cicn, the on-disk offsets are absolute from the resource start (they
// were real Handle/Ptr fields flattened to file offsets), so the sections are read by seeking
// rather than assumed to be sequential.
public static class CrsrDecoder
{
    public static Rgba8Image? Decode(byte[] data, out int hotX, out int hotY, string? tag = null)
    {
        hotX = hotY = 0;
        if (data.Length < 0x60) { Log(tag, $"too small ({data.Length})"); return null; }
        var r = new BigEndianSpanReader(data);
        r.Skip(2);                       // crsrType
        int crsrMapOffset = r.ReadInt32();
        int crsrDataOffset = r.ReadInt32();
        r.Skip(4);                       // crsrXData
        r.Skip(2);                       // crsrXValid
        r.Skip(4);                       // crsrXHandle
        ReadOnlySpan<byte> maskBits = r.ReadBytes(32);    // crsrMask: 16x16 1bpp, 1 = opaque
        ReadOnlySpan<byte> imageBits = r.ReadBytes(32);   // crsrBits: 16x16 1bpp, 1 = black
        short hotV = r.ReadInt16(), hotH = r.ReadInt16();
        hotX = hotH; hotY = hotV;

        if (crsrMapOffset > 0 && crsrDataOffset > 0)
        {
            var colorImage = TryDecodeColorImage(data, crsrMapOffset, crsrDataOffset, tag);
            if (colorImage is not null) return colorImage;
        }

        // Monochrome fallback (also the path for a plain, non-colour crsrType): paint crsrBits
        // black/white, masked by crsrMask — the same convention every 'crsr' carries so 8-bit/
        // colour-less Macs still get a usable cursor.
        var image = new Rgba8Image(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int byteIdx = y * 2 + x / 8;
                int bit = 7 - (x % 8);
                bool black = ((imageBits[byteIdx] >> bit) & 1) != 0;
                bool opaque = ((maskBits[byteIdx] >> bit) & 1) != 0;
                byte v = black ? (byte)0 : (byte)255;
                image.SetPixel(x, y, v, v, v, opaque ? (byte)255 : (byte)0);
            }
        }
        return image;
    }

    private static Rgba8Image? TryDecodeColorImage(byte[] data, int mapOffset, int dataOffset, string? tag)
    {
        if (mapOffset < 0 || mapOffset + 50 > data.Length) { Log(tag, "pixMap offset out of range"); return null; }
        var pm = new BigEndianSpanReader(data);
        pm.Seek(mapOffset);
        pm.Skip(4);                              // baseAddr
        short rowBytes = (short)(pm.ReadInt16() & 0x7FFF);
        short top = pm.ReadInt16(), left = pm.ReadInt16(), bottom = pm.ReadInt16(), right = pm.ReadInt16();
        int width = right - left, height = bottom - top;
        pm.Skip(2);                              // pmVersion
        pm.Skip(2);                              // packType
        pm.Skip(4);                              // packSize
        pm.Skip(8);                              // hRes + vRes
        pm.Skip(2);                              // pixelType
        short pixelSize = pm.ReadInt16();
        pm.Skip(2);                              // cmpCount
        pm.Skip(2);                              // cmpSize
        pm.Skip(4);                              // planeBytes
        int pmTableOffset = pm.ReadInt32();

        if (width <= 0 || height <= 0 || width > 64 || height > 64 || rowBytes <= 0)
        { Log(tag, $"invalid pixMap dims {width}x{height}"); return null; }
        int pixelsPerByte = pixelSize == 0 ? 0 : 8 / pixelSize;
        if (pixelsPerByte == 0) { Log(tag, $"unsupported pixelSize {pixelSize}"); return null; }
        byte indexMask = (byte)((1 << pixelSize) - 1);

        int pixelBytes = rowBytes * height;
        if (dataOffset < 0 || dataOffset + pixelBytes > data.Length) { Log(tag, "truncated pixel data"); return null; }
        ReadOnlySpan<byte> pixelData = data.AsSpan(dataOffset, pixelBytes);

        if (pmTableOffset <= 0 || pmTableOffset + 8 > data.Length) { Log(tag, "missing ColorTable"); return null; }
        var ctReader = new BigEndianSpanReader(data.AsSpan(pmTableOffset));
        var colorTable = ColorTableDecoder.Read(ref ctReader);

        // Unlike 'cicn', 'crsr' carries no separate mask sized for the colour PixMap — crsrMask
        // above is fixed at 16x16 for the classic monochrome fallback only. The colour pixel data
        // is its own mask: ColorTable index 0 (empirically confirmed white here, and a standard
        // Mac icon/cursor convention) marks a transparent pixel; every other index is opaque.
        var image = new Rgba8Image(width, height);
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * rowBytes;
            for (int x = 0; x < width; x++)
            {
                int byteIdx = rowStart + x / pixelsPerByte;
                if (byteIdx >= pixelData.Length) break;
                int bitOff = (pixelsPerByte - 1 - (x % pixelsPerByte)) * pixelSize;
                byte idx = (byte)((pixelData[byteIdx] >> bitOff) & indexMask);
                var color = colorTable.Get(idx);
                image.SetPixel(x, y, color.R, color.G, color.B, idx != 0 ? (byte)255 : (byte)0);
            }
        }
        return image;
    }

    private static void Log(string? tag, string m) =>
        Console.WriteLine(tag is null ? $"  [WARN] Crsr: {m}" : $"  [WARN] {tag}: {m}");
}
