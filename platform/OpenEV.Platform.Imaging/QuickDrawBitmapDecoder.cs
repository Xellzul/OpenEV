using System;
using System.Buffers;
using System.Buffers.Binary;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

public static class QuickDrawBitmapDecoder
{
    public static Rgba8Image? Decode(ref BigEndianSpanReader reader, int width, int height, ushort opcode, string? tag = null)
    {
        if (opcode == 0x009A || opcode == 0x009B)
        {
            if (reader.Remaining < 4) { Log(tag, $"0x{opcode:X4}: missing baseAddr"); return null; }
            reader.Skip(4);
        }
        if (reader.Remaining < 2) { Log(tag, $"0x{opcode:X4}: missing rowBytes"); return null; }
        short rowBytesRaw = reader.ReadInt16();
        bool isPixMap = (rowBytesRaw & 0x8000) != 0;
        short rowBytes = (short)(rowBytesRaw & 0x7FFF);

        if (reader.Remaining < 42) { Log(tag, $"0x{opcode:X4}: PixMap header truncated"); return null; }

        reader.Skip(8);
        short pmVersion = reader.ReadInt16();
        short packType = reader.ReadInt16();
        int packSize = reader.ReadInt32();
        reader.Skip(8);
        short pixelType = reader.ReadInt16();
        short pixelSize = reader.ReadInt16();
        short cmpCount = reader.ReadInt16();
        short cmpSize = reader.ReadInt16();
        reader.Skip(12);

        ColorTable? colorTable = null;
        if (opcode == 0x0098 || (isPixMap && pixelSize <= 8))
        {
            if (reader.Remaining < 8) { Log(tag, $"0x{opcode:X4}: missing ColorTable"); return null; }
            colorTable = ColorTableDecoder.Read(ref reader);
        }

        DecodeDiagnostics.Log(tag,
            $"op=0x{opcode:X4} {width}x{height} rowBytes={rowBytes} isPixMap={isPixMap} packType={packType} pixelSize={pixelSize} cmpCount={cmpCount} cmpSize={cmpSize}");

        if (reader.Remaining < 18) { Log(tag, $"0x{opcode:X4}: missing srcRect/dstRect/mode"); return null; }
        reader.Skip(8); reader.Skip(8);
        short mode = reader.ReadInt16();

        if (opcode == 0x0091 || opcode == 0x0099 || opcode == 0x009B)
        {
            if (reader.Remaining < 2) { Log(tag, $"0x{opcode:X4}: missing maskRgn size"); return null; }
            int rgnSize = reader.ReadUInt16();
            int rgnPayload = rgnSize - 2;
            if (rgnPayload < 0 || rgnPayload > reader.Remaining)
            { Log(tag, $"0x{opcode:X4}: maskRgn size out of range"); return null; }
            reader.Skip(rgnPayload);
        }

        if (width <= 0 || height <= 0 || rowBytes < 0)
        { Log(tag, $"0x{opcode:X4}: invalid dims"); return null; }

        var image = new Rgba8Image(width, height);
        byte[] rowBuffer = ArrayPool<byte>.Shared.Rent(rowBytes);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowData = rowBuffer.AsSpan(0, rowBytes);
                rowData.Clear();

                bool defaultPacked = packType == 0 && rowBytes >= 8;
                bool rawData = packType == 1 || (packType == 0 && rowBytes < 8) || packType == 2;

                if (rawData)
                {
                    if (reader.Remaining >= rowBytes) reader.ReadBytes(rowBytes).CopyTo(rowData);
                    else break;
                }
                else
                {
                    if (reader.Remaining < 1) break;
                    int len = rowBytes >= 250
                        ? (reader.Remaining >= 2 ? reader.ReadUInt16() : 0)
                        : reader.ReadByte();
                    if (len > 0 && reader.Remaining >= len)
                    {
                        var packed = reader.ReadBytes(len);
                        if (packType == 3 || pixelSize == 16) PackBitsDecompressor.UnpackWords(packed, rowData);
                        else PackBitsDecompressor.Unpack(packed, rowData);
                    }
                }

                if (pixelSize == 32) Read32(rowData, image, y, width, cmpCount);
                else if (pixelSize == 16) Read16(rowData, image, y, width);
                else if (pixelSize <= 8) ReadIndexed(rowData, image, y, width, pixelSize, colorTable);
            }
        }
        catch (Exception ex) { Log(tag, $"row exception: {ex.GetType().Name}: {ex.Message}"); }
        finally { ArrayPool<byte>.Shared.Return(rowBuffer); }
        return image;
    }

    private static void Read32(ReadOnlySpan<byte> input, Rgba8Image image, int y, int width, int cmpCount)
    {
        int components = cmpCount > 0 ? cmpCount : (input.Length / Math.Max(width, 1));
        if (components < 3) return;
        if (components == 3)
        {
            if (input.Length < width * 3) return;
            for (int x = 0; x < width; x++)
                image.SetPixel(x, y,
                    Gamma.Correct(input[x]),
                    Gamma.Correct(input[x + width]),
                    Gamma.Correct(input[x + width * 2]),
                    255);
        }
        else
        {
            if (input.Length < width * 4) return;
            for (int x = 0; x < width; x++)
            {
                byte a = (cmpCount == 4) ? input[x] : (byte)255;
                image.SetPixel(x, y,
                    Gamma.Correct(input[x + width]),
                    Gamma.Correct(input[x + width * 2]),
                    Gamma.Correct(input[x + width * 3]),
                    a);
            }
        }
    }

    private static void Read16(ReadOnlySpan<byte> input, Rgba8Image image, int y, int width)
    {
        if (input.Length < width * 2) return;
        for (int x = 0; x < width; x++)
        {
            ushort pixel = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(x * 2, 2));
            byte r = (byte)((pixel >> 10) & 0x1F);
            byte g = (byte)((pixel >> 5) & 0x1F);
            byte b = (byte)(pixel & 0x1F);
            r = (byte)((r << 3) | (r >> 2));
            g = (byte)((g << 3) | (g >> 2));
            b = (byte)((b << 3) | (b >> 2));
            image.SetPixel(x, y, Gamma.Correct(r), Gamma.Correct(g), Gamma.Correct(b), 255);
        }
    }

    private static void ReadIndexed(ReadOnlySpan<byte> input, Rgba8Image image, int y, int width, int pixelSize, ColorTable? colorTable)
    {
        int pixelsPerByte = 8 / pixelSize;
        if (pixelsPerByte == 0) return;
        byte mask = (byte)((1 << pixelSize) - 1);
        for (int x = 0; x < width; x++)
        {
            int byteIdx = x / pixelsPerByte;
            if (byteIdx >= input.Length) break;
            int bitOffset = (pixelsPerByte - 1 - (x % pixelsPerByte)) * pixelSize;
            byte index = (byte)((input[byteIdx] >> bitOffset) & mask);
            if (colorTable != null)
            {
                var c = colorTable.Get(index);
                image.SetPixel(x, y, c.R, c.G, c.B, c.A);
            }
            else
            {
                byte g = Gamma.Correct(index);
                image.SetPixel(x, y, g, g, g, 255);
            }
        }
    }

    private static void Log(string? tag, string m) =>
        Console.WriteLine(tag is null ? $"  [WARN] QuickDrawBitmap: {m}" : $"  [WARN] {tag}: {m}");
}
