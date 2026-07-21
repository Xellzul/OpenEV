using System;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

// 'ppat' (Pixel Pattern). On-disk PixPat record:
//   +0  patType (int16)   0 = old 1-bit pattern, 1/2 = colour PixMap pattern
//   +2  patMap  (int32)   offset (from resource start) to the PixMap
//   +6  patData (int32)   offset to the pattern's pixel image
//   +10 patXData(int32), +14 patXValid(int16), +16 patXMap(int32)
//   +20 pat1Data (8 bytes) the equivalent 1-bit QuickDraw pattern
// PixMap @ patMap: rowBytes@+4 (&0x3fff), bounds@+6 (t,l,b,r), pixelSize@+32,
//   pmTable@+42 (offset to the ColorTable). Pixels @ patData (indexed).
//
// Returns the pattern tile as an opaque Rgba8Image (the radar-jam static tile,
// the armor-bar fill). Defensive: any unexpected layout / compression returns
// null so FillCRect falls back to the active fore colour (the prior behaviour).
public static class PpatDecoder
{
    public static Rgba8Image? Decode(byte[] data, string? tag = null)
    {
        try
        {
            if (data is null || data.Length < 28) return null;
            var r = new BigEndianSpanReader(data);
            short patType = r.ReadInt16();
            int patMap = r.ReadInt32();
            int patData = r.ReadInt32();

            // Old 1-bit pattern (or no PixMap) → expand the 8×8 pat1Data.
            if (patType == 0 || patMap <= 0 || patMap + 50 > data.Length)
                return Expand1Bit(data);

            r.Seek(patMap);
            r.Skip(4);                                   // baseAddr
            int rowBytes = r.ReadUInt16() & 0x3fff;
            short top = r.ReadInt16(), left = r.ReadInt16(), bottom = r.ReadInt16(), right = r.ReadInt16();
            int width = right - left, height = bottom - top;
            r.Skip(2);                                   // pmVersion
            short packType = r.ReadInt16();
            r.Skip(4);                                   // packSize
            r.Skip(8);                                   // hRes, vRes
            r.Skip(2);                                   // pixelType
            short pixelSize = r.ReadInt16();
            r.Skip(4);                                   // cmpCount, cmpSize
            r.Skip(4);                                   // planeBytes
            int pmTable = r.ReadInt32();

            if (width <= 0 || height <= 0 || width > 256 || height > 256 || rowBytes <= 0)
                return Expand1Bit(data);
            if (packType != 0) return Expand1Bit(data);  // packed pattern — uncommon; fall back
            if (pmTable <= 0 || pmTable >= data.Length) return Expand1Bit(data);

            r.Seek(pmTable);
            var ct = ColorTableDecoder.Read(ref r);

            int pixelBytes = rowBytes * height;
            if (patData <= 0 || patData + pixelBytes > data.Length) return Expand1Bit(data);
            var pix = data.AsSpan(patData, pixelBytes);

            int perByte = pixelSize == 0 ? 0 : 8 / pixelSize;
            if (perByte == 0) return Expand1Bit(data);   // 16/32-bit direct patterns — fall back
            byte idxMask = (byte)((1 << pixelSize) - 1);

            var img = new Rgba8Image(width, height);
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int bi = rowStart + x / perByte;
                    if (bi >= pix.Length) break;
                    int bo = (perByte - 1 - (x % perByte)) * pixelSize;
                    byte idx = (byte)((pix[bi] >> bo) & idxMask);
                    var c = ct.Get(idx);
                    img.SetPixel(x, y, c.R, c.G, c.B, 255);   // patterns are opaque
                }
            }
            return img;
        }
        catch { return null; }
    }

    // patType 0: pat1Data is an 8-byte, 8×8, 1-bit pattern (bit set = black).
    private static Rgba8Image Expand1Bit(byte[] d)
    {
        var img = new Rgba8Image(8, 8);
        for (int y = 0; y < 8; y++)
        {
            byte row = (20 + y) < d.Length ? d[20 + y] : (byte)0;
            for (int x = 0; x < 8; x++)
            {
                bool on = ((row >> (7 - x)) & 1) != 0;
                byte v = on ? (byte)0 : (byte)255;
                img.SetPixel(x, y, v, v, v, 255);
            }
        }
        return img;
    }
}
