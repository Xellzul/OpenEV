using System;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

// 'cicn' (Color Icon). PixMap + iconMask BitMap + iconBMap BitMap + NULL handle + data blocks + ColorTable.
public static class CicnDecoder
{
    public static Rgba8Image? Decode(byte[] data, string? tag = null)
    {
        if (data.Length < 82) { Log(tag, $"too small ({data.Length})"); return null; }
        var r = new BigEndianSpanReader(data);
        r.Skip(4);
        short pmRowBytesRaw = r.ReadInt16();
        short pmRowBytes = (short)(pmRowBytesRaw & 0x7FFF);
        short pmTop = r.ReadInt16(), pmLeft = r.ReadInt16();
        short pmBottom = r.ReadInt16(), pmRight = r.ReadInt16();
        int width = pmRight - pmLeft;
        int height = pmBottom - pmTop;
        r.Skip(2); short packType = r.ReadInt16(); r.Skip(4); r.Skip(8); r.Skip(2);
        short pixelSize = r.ReadInt16();
        r.Skip(2); r.Skip(2); r.Skip(4); r.Skip(4); r.Skip(4);

        r.Skip(4);
        short maskRowBytes = r.ReadInt16();
        short maskTop = r.ReadInt16(), maskLeft = r.ReadInt16();
        short maskBottom = r.ReadInt16(), maskRight = r.ReadInt16();
        int maskHeight = maskBottom - maskTop;

        r.Skip(4);
        short bmpRowBytes = r.ReadInt16();
        short bmpTop = r.ReadInt16(), bmpLeft = r.ReadInt16();
        short bmpBottom = r.ReadInt16(), bmpRight = r.ReadInt16();
        int bmpHeight = bmpBottom - bmpTop;

        r.Skip(4);

        if (width <= 0 || height <= 0 || width > 512 || height > 512 || pmRowBytes <= 0)
        { Log(tag, $"invalid cicn dims"); return null; }

        int maskBytes = Math.Max(0, maskRowBytes * maskHeight);
        int bmpBytes = Math.Max(0, bmpRowBytes * bmpHeight);
        int pixelBytes = pmRowBytes * height;

        if (r.Remaining < maskBytes + bmpBytes) { Log(tag, "truncated before mask/bmap"); return null; }
        ReadOnlySpan<byte> maskData = r.ReadBytes(maskBytes);
        r.Skip(bmpBytes);

        if (r.Remaining < 8) { Log(tag, "missing ColorTable"); return null; }
        var colorTable = ColorTableDecoder.Read(ref r);

        if (r.Remaining < pixelBytes) { Log(tag, "truncated before pixels"); return null; }
        ReadOnlySpan<byte> pixelData = r.ReadBytes(pixelBytes);

        var image = new Rgba8Image(width, height);
        int pixelsPerByte = pixelSize == 0 ? 0 : 8 / pixelSize;
        if (pixelsPerByte == 0) { Log(tag, $"unsupported pixelSize {pixelSize}"); return null; }
        byte indexMask = (byte)((1 << pixelSize) - 1);

        for (int y = 0; y < height; y++)
        {
            int pixRowStart = y * pmRowBytes;
            int maskRowStart = y * maskRowBytes;
            for (int x = 0; x < width; x++)
            {
                int pixByteIdx = pixRowStart + x / pixelsPerByte;
                if (pixByteIdx >= pixelData.Length) break;
                int bitOff = (pixelsPerByte - 1 - (x % pixelsPerByte)) * pixelSize;
                byte idx = (byte)((pixelData[pixByteIdx] >> bitOff) & indexMask);
                var color = colorTable.Get(idx);

                byte alpha = 255;
                if (maskRowBytes > 0 && maskHeight > 0 && y < maskHeight)
                {
                    int maskByteIdx = maskRowStart + (x >> 3);
                    if (maskByteIdx < maskData.Length)
                    {
                        int maskBit = (maskData[maskByteIdx] >> (7 - (x & 7))) & 1;
                        alpha = maskBit == 1 ? (byte)255 : (byte)0;
                    }
                }
                image.SetPixel(x, y, color.R, color.G, color.B, alpha);
            }
        }
        return image;
    }

    private static void Log(string? tag, string m) =>
        Console.WriteLine(tag is null ? $"  [WARN] Cicn: {m}" : $"  [WARN] {tag}: {m}");
}
