using System;
using System.IO;

namespace OpenEV.Platform.Imaging;

// Minimal, dependency-free PNG encoder for Rgba8Image. Replaces MonoGame's
// RenderTarget2D.SaveAsPng (the only thing that wrote PNGs). Uses stored (uncompressed) DEFLATE blocks: trivially
// spec-compliant zlib, and screenshots are CI artifacts where size is irrelevant.
// 8-bit RGBA (PNG colour type 6), no interlace, filter 0 (none) per row.
public static class PngWriter
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static void Write(Rgba8Image img, Stream stream)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        stream.Write(Signature, 0, Signature.Length);

        // IHDR
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)img.Width);
        WriteBE(ihdr, 4, (uint)img.Height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // colour type: truecolour + alpha
        ihdr[10] = 0;  // compression: deflate
        ihdr[11] = 0;  // filter: adaptive (we use 0 per row)
        ihdr[12] = 0;  // interlace: none
        WriteChunk(stream, "IHDR", ihdr);

        // Raw image data: each row prefixed with a filter byte (0 = none).
        int rowLen = img.Width * 4;
        var raw = new byte[img.Height * (rowLen + 1)];
        int ro = 0;
        for (int y = 0; y < img.Height; y++)
        {
            raw[ro++] = 0; // filter: none
            Buffer.BlockCopy(img.Pixels, y * rowLen, raw, ro, rowLen);
            ro += rowLen;
        }

        WriteChunk(stream, "IDAT", ZlibStored(raw));
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    public static void WriteFile(Rgba8Image img, string path)
    {
        using var fs = File.Create(path);
        Write(img, fs);
    }

    // zlib stream wrapping `data` in stored (BTYPE=00) DEFLATE blocks.
    private static byte[] ZlibStored(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // CMF: deflate, 32K window
        ms.WriteByte(0x01); // FLG: no preset dict, check bits

        int offset = 0;
        while (offset < data.Length)
        {
            int len = Math.Min(0xFFFF, data.Length - offset);
            bool final = offset + len >= data.Length;
            ms.WriteByte((byte)(final ? 1 : 0));     // BFINAL in bit0, BTYPE=00
            ms.WriteByte((byte)(len & 0xFF));        // LEN (little-endian)
            ms.WriteByte((byte)((len >> 8) & 0xFF));
            int nlen = (~len) & 0xFFFF;
            ms.WriteByte((byte)(nlen & 0xFF));       // NLEN = ones-complement of LEN
            ms.WriteByte((byte)((nlen >> 8) & 0xFF));
            ms.Write(data, offset, len);
            offset += len;
        }
        // Handle the empty-data edge (still need a final block).
        if (data.Length == 0)
        {
            ms.WriteByte(1); ms.WriteByte(0); ms.WriteByte(0);
            ms.WriteByte(0xFF); ms.WriteByte(0xFF);
        }

        WriteBEStream(ms, Adler32(data));
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        WriteBE(lenBuf, 0, (uint)data.Length);
        stream.Write(lenBuf);

        var typeBytes = new byte[4];
        for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];
        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcBuf = stackalloc byte[4];
        WriteBE(crcBuf, 0, crc);
        stream.Write(crcBuf);
    }

    private static void WriteBE(Span<byte> buf, int off, uint v)
    {
        buf[off]     = (byte)(v >> 24);
        buf[off + 1] = (byte)(v >> 16);
        buf[off + 2] = (byte)(v >> 8);
        buf[off + 3] = (byte)v;
    }

    private static void WriteBEStream(Stream s, uint v)
    {
        s.WriteByte((byte)(v >> 24));
        s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (byte x in data)
        {
            a = (a + x) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static uint[]? _crcTable;

    private static uint Crc32(byte[] type, byte[] data)
    {
        var table = _crcTable ??= BuildCrcTable();
        uint crc = 0xFFFFFFFF;
        foreach (byte x in type) crc = table[(crc ^ x) & 0xFF] ^ (crc >> 8);
        foreach (byte x in data) crc = table[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
