namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// Decompressor for StuffIt compression method 13 (StuffIt 5's "faster" method),
/// ported from XADMaster's <c>XADStuffIt13Handle.m</c> (© Dag Ågren / MacPaw,
/// LGPL 2.1+, https://github.com/MacPaw/XADMaster).
///
/// A stream is LZSS over a 64 KiB window whose literals/lengths and offset slots are
/// Huffman-coded (low-bit-first bitstream). One mode byte starts the stream: the high
/// nibble selects either meta-coded dynamic trees (0) or one of five hardcoded table
/// sets (1–5); in dynamic mode bit 3 reuses the first tree as the second and the low
/// three bits size the offset tree. Two literal/length trees alternate: "first" is
/// active after a literal, "second" after a match.
/// </summary>
internal static class StuffIt13Decompressor
{
    private const int NumLiteralLengthSymbols = 321; // 256 literals + length codes + end marker (0x140)

    /// <summary>
    /// How many bytes of leading pad <see cref="DecompressDroppingLeadingPad"/> tolerates.
    /// The EV Override archive's one padded stream carries 2; the cap only bounds the
    /// retry buffer, so it is generous.
    /// </summary>
    private const int MaxLeadingPad = 64;

    /// <summary>
    /// Decompress exactly <paramref name="uncompressedLength"/> bytes, stopping at that
    /// length like XADLZSSHandle does (the end marker, if present, goes unread).
    /// </summary>
    public static byte[] Decompress(byte[] compressed, int uncompressedLength)
    {
        var output = new byte[uncompressedLength];
        if (uncompressedLength == 0) return output;
        int produced = Decode(compressed, output, untilEndMarker: false);
        if (produced < uncompressedLength)
            throw new InvalidDataException("Method-13 stream ended before producing the expected length.");
        return output;
    }

    /// <summary>
    /// Fallback for streams whose fork is preceded by encoder pad: decode until the
    /// end marker (symbol 0x140) and return the LAST <paramref name="uncompressedLength"/>
    /// bytes, or null if the stream doesn't terminate within <see cref="MaxLeadingPad"/>
    /// extra bytes. The real StuffIt 5 encoder ends every stream with the end marker, at
    /// declared-length+2 for the EV Override app's data fork (two zero bytes of pad the
    /// fork's CRC ignores by construction: leading zeroes are neutral to a 0-initialized
    /// reflected CRC). libxad, XADMaster and peeler all decode such a fork shifted; the
    /// caller only accepts this fallback when the fork CRC then verifies.
    /// </summary>
    public static byte[]? DecompressDroppingLeadingPad(byte[] compressed, int uncompressedLength)
    {
        var buffer = new byte[uncompressedLength + MaxLeadingPad];
        int produced;
        try
        {
            produced = Decode(compressed, buffer, untilEndMarker: true);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException)
        {
            return null;
        }
        if (produced < uncompressedLength) return null;
        return buffer[(produced - uncompressedLength)..produced];
    }

    /// <summary>
    /// Core decode loop. Returns the number of bytes produced. In strict mode
    /// (<paramref name="untilEndMarker"/> false) it stops when <paramref name="output"/>
    /// is full; in end-marker mode it stops at symbol 0x140 (or throws if the output
    /// buffer would overflow first).
    /// </summary>
    private static int Decode(byte[] compressed, byte[] output, bool untilEndMarker)
    {
        var reader = new BitReader(compressed);

        // The mode byte is read at stream start, byte-aligned, so an 8-bit low-bit-first
        // read is exactly CSInputNextByte.
        int val = reader.ReadBits(8);
        int code = val >> 4;

        HuffmanDecoder firstCode, secondCode, offsetCode;
        if (code == 0)
        {
            var metaCode = new HuffmanDecoder();
            for (int i = 0; i < 37; i++)
                metaCode.AddCodeLowBitFirst(i, (uint)StuffIt13Tables.MetaCodes[i], StuffIt13Tables.MetaCodeLengths[i]);

            firstCode = ParseCode(NumLiteralLengthSymbols, metaCode, reader);
            secondCode = (val & 0x08) != 0 ? firstCode : ParseCode(NumLiteralLengthSymbols, metaCode, reader);
            offsetCode = ParseCode((val & 0x07) + 10, metaCode, reader);
        }
        else if (code < 6)
        {
            firstCode = HuffmanDecoder.FromLengths(StuffIt13Tables.FirstCodeLengths[code - 1]);
            secondCode = HuffmanDecoder.FromLengths(StuffIt13Tables.SecondCodeLengths[code - 1]);
            offsetCode = HuffmanDecoder.FromLengths(StuffIt13Tables.OffsetCodeLengths[code - 1]);
        }
        else
        {
            throw new InvalidDataException($"Method-13 stream has invalid mode nibble {code}.");
        }

        var currCode = firstCode;
        int pos = 0;
        while (untilEndMarker || pos < output.Length)
        {
            int symbol = currCode.DecodeSymbol(reader);

            if (symbol < 0x100)
            {
                if (pos >= output.Length)
                    throw new InvalidDataException("Method-13 stream overflows the output buffer.");
                currCode = firstCode;
                output[pos++] = (byte)symbol;
                continue;
            }

            currCode = secondCode;

            int length;
            if (symbol < 0x13e) length = symbol - 0x100 + 3;
            else if (symbol == 0x13e) length = reader.ReadBits(10) + 65;
            else if (symbol == 0x13f) length = reader.ReadBits(15) + 65;
            else if (untilEndMarker) return pos; // end marker (0x140)
            else throw new InvalidDataException("Method-13 stream ended before producing the expected length.");

            int bitLength = offsetCode.DecodeSymbol(reader);
            int offset;
            if (bitLength == 0) offset = 1;
            else if (bitLength == 1) offset = 2;
            else offset = (1 << (bitLength - 1)) + reader.ReadBits(bitLength - 1) + 1;

            // Match copy, clamped to the output size like XADLZSSHandle, which stops at
            // the declared length rather than at an end marker. References before the
            // stream start read as zero (XAD's window starts zeroed); valid streams
            // never do this, and the fork CRC would catch it.
            for (int i = 0; i < length && pos < output.Length; i++)
            {
                int src = pos - offset;
                output[pos++] = src >= 0 ? output[src] : (byte)0;
            }
            if (untilEndMarker && pos >= output.Length)
                throw new InvalidDataException("Method-13 stream overflows the output buffer.");
        }

        return pos;
    }

    /// <summary>
    /// Read a code-length table transmitted with the fixed 37-symbol meta-code and build
    /// its decoder. Symbols 0–30 set the running length to symbol+1; 31 poisons it to -1
    /// (symbol absent); 32/33 increment/decrement it. Every meta-symbol then assigns the
    /// running length to the current slot (the assign-after-switch at the loop bottom);
    /// symbols 34–36 additionally repeat-assign it first (34: one extra slot if the next
    /// bit is set; 35: 3-bit+2 extra slots; 36: 6-bit+10 extra slots). This is a faithful
    /// port of XAD's quirky loop, extra slots consumed and all.
    /// </summary>
    private static HuffmanDecoder ParseCode(int numCodes, HuffmanDecoder metaCode, BitReader reader)
    {
        int length = 0;
        // Slack matches the original's behavior of writing one slot past a repeat that
        // ends exactly at the table boundary (harmless out-of-bounds in the C original).
        var lengths = new int[numCodes + 74];

        for (int i = 0; i < numCodes; i++)
        {
            int val = metaCode.DecodeSymbol(reader);
            switch (val)
            {
                case 31: length = -1; break;
                case 32: length++; break;
                case 33: length--; break;
                case 34:
                    if (reader.ReadBit() != 0) lengths[i++] = length;
                    break;
                case 35:
                    val = reader.ReadBits(3) + 2;
                    while (val-- > 0) lengths[i++] = length;
                    break;
                case 36:
                    val = reader.ReadBits(6) + 10;
                    while (val-- > 0) lengths[i++] = length;
                    break;
                default: length = val + 1; break;
            }
            lengths[i] = length;
        }

        return HuffmanDecoder.FromLengths(lengths.AsSpan(0, numCodes));
    }
}
