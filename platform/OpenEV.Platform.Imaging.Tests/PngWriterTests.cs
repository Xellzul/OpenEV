using System.IO;
using System.IO.Compression;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

public class PngWriterTests
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Decode our PNG back to raw RGBA using the BCL ZLibStream — proves the
    // zlib stream and chunk framing are spec-valid, not just self-consistent.
    private static byte[] DecodeToRgba(byte[] png, out int width, out int height)
    {
        Assert.True(png.Length > 8);
        for (int i = 0; i < 8; i++) Assert.Equal(Signature[i], png[i]);

        int pos = 8;
        width = height = 0;
        using var idat = new MemoryStream();
        while (pos < png.Length)
        {
            int len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            string type = $"{(char)png[pos + 4]}{(char)png[pos + 5]}{(char)png[pos + 6]}{(char)png[pos + 7]}";
            int dataOff = pos + 8;
            if (type == "IHDR")
            {
                width = (png[dataOff] << 24) | (png[dataOff + 1] << 16) | (png[dataOff + 2] << 8) | png[dataOff + 3];
                height = (png[dataOff + 4] << 24) | (png[dataOff + 5] << 16) | (png[dataOff + 6] << 8) | png[dataOff + 7];
                Assert.Equal(8, png[dataOff + 8]);  // bit depth
                Assert.Equal(6, png[dataOff + 9]);  // RGBA
            }
            else if (type == "IDAT")
            {
                idat.Write(png, dataOff, len);
            }
            else if (type == "IEND")
            {
                break;
            }
            pos = dataOff + len + 4; // + CRC
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var rawBytes = raw.ToArray();

        // Strip the per-row filter byte (must be 0 = none).
        int rowLen = width * 4;
        var rgba = new byte[width * height * 4];
        int ro = 0;
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(0, rawBytes[y * (rowLen + 1)]);
            Buffer.BlockCopy(rawBytes, y * (rowLen + 1) + 1, rgba, ro, rowLen);
            ro += rowLen;
        }
        return rgba;
    }

    [Fact]
    public void Write_SmallImage_RoundTripsThroughBclZlib()
    {
        var img = new Rgba8Image(3, 2);
        img.SetPixel(0, 0, 255, 0, 0, 255);
        img.SetPixel(1, 0, 0, 255, 0, 200);
        img.SetPixel(2, 0, 0, 0, 255, 100);
        img.SetPixel(0, 1, 1, 2, 3, 4);
        img.SetPixel(1, 1, 250, 240, 230, 220);
        img.SetPixel(2, 1, 9, 8, 7, 6);

        using var ms = new MemoryStream();
        PngWriter.Write(img, ms);

        var decoded = DecodeToRgba(ms.ToArray(), out int w, out int h);
        Assert.Equal(3, w);
        Assert.Equal(2, h);
        Assert.Equal(img.Pixels, decoded);
    }

    [Fact]
    public void Write_LargeImage_SpansMultipleStoredBlocks()
    {
        // 200×200×4 + 200 filter bytes = 160200 raw bytes > 65535 → forces
        // multiple stored DEFLATE blocks; verify they reassemble correctly.
        var img = new Rgba8Image(200, 200);
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 200; x++)
                img.SetPixel(x, y, (byte)x, (byte)y, (byte)(x ^ y), 255);

        using var ms = new MemoryStream();
        PngWriter.Write(img, ms);

        var decoded = DecodeToRgba(ms.ToArray(), out int w, out int h);
        Assert.Equal(200, w);
        Assert.Equal(200, h);
        Assert.Equal(img.Pixels, decoded);
    }
}
