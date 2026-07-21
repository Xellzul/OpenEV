namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// Low-bit-first bitstream reader, matching XADMaster's <c>CSInputBuffer</c> "LE"
/// functions that the StuffIt method-13 decoder is built on: within each byte, bit 0
/// is consumed first, and <see cref="ReadBits"/> composes multi-bit values with the
/// first-read bit as the least significant bit of the result.
/// </summary>
internal sealed class BitReader(byte[] data)
{
    private readonly byte[] _data = data;
    private int _bytePos;
    private int _bitPos; // 0..7, index of the next bit within _data[_bytePos]

    /// <summary>Read a single bit (0 or 1).</summary>
    public int ReadBit()
    {
        if (_bytePos >= _data.Length)
            throw new EndOfStreamException("Compressed bitstream is truncated.");
        int bit = (_data[_bytePos] >> _bitPos) & 1;
        if (++_bitPos == 8) { _bitPos = 0; _bytePos++; }
        return bit;
    }

    /// <summary>Read <paramref name="count"/> bits; the first bit read becomes bit 0 of the result.</summary>
    public int ReadBits(int count)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
            result |= ReadBit() << i;
        return result;
    }
}
