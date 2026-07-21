namespace OpenEV.Platform.ResourceFork.Tests;

public class BigEndianSpanReaderTests
{
    [Fact]
    public void NewReader_StartsAtZero_WithFullRemaining()
    {
        var r = new BigEndianSpanReader([1, 2, 3, 4]);
        Assert.Equal(0, r.Position);
        Assert.Equal(4, r.Length);
        Assert.Equal(4, r.Remaining);
    }

    [Fact]
    public void ReadByte_ReturnsBytes_AndAdvances()
    {
        var r = new BigEndianSpanReader([0x0A, 0x0B]);
        Assert.Equal((byte)0x0A, r.ReadByte());
        Assert.Equal(1, r.Position);
        Assert.Equal((byte)0x0B, r.ReadByte());
        Assert.Equal(2, r.Position);
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void ReadInt16_IsBigEndian_AndSigned()
    {
        Assert.Equal((short)0x0102, new BigEndianSpanReader([0x01, 0x02]).ReadInt16());
        Assert.Equal((short)-1, new BigEndianSpanReader([0xFF, 0xFF]).ReadInt16());
        Assert.Equal(short.MinValue, new BigEndianSpanReader([0x80, 0x00]).ReadInt16());
    }

    [Fact]
    public void ReadUInt16_IsBigEndian_Unsigned()
    {
        Assert.Equal((ushort)0xFFFF, new BigEndianSpanReader([0xFF, 0xFF]).ReadUInt16());
        Assert.Equal((ushort)0x0100, new BigEndianSpanReader([0x01, 0x00]).ReadUInt16());
    }

    [Fact]
    public void ReadInt32_IsBigEndian_AndSigned()
    {
        Assert.Equal(0x01020304, new BigEndianSpanReader([0x01, 0x02, 0x03, 0x04]).ReadInt32());
        Assert.Equal(-1, new BigEndianSpanReader([0xFF, 0xFF, 0xFF, 0xFF]).ReadInt32());
    }

    [Fact]
    public void ReadUInt32_IsBigEndian_Unsigned()
    {
        Assert.Equal(0x01020304u, new BigEndianSpanReader([0x01, 0x02, 0x03, 0x04]).ReadUInt32());
        Assert.Equal(uint.MaxValue, new BigEndianSpanReader([0xFF, 0xFF, 0xFF, 0xFF]).ReadUInt32());
    }

    [Fact]
    public void SequentialReads_AdvancePositionAcrossTypes()
    {
        var r = new BigEndianSpanReader([0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0xAB]);
        Assert.Equal((short)0x0010, r.ReadInt16());
        Assert.Equal(0x00000020u, r.ReadUInt32());
        Assert.Equal((byte)0xAB, r.ReadByte());
        Assert.Equal(7, r.Position);
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void ReadBytes_ReturnsSlice_AndAdvances()
    {
        var r = new BigEndianSpanReader([1, 2, 3, 4, 5]);
        r.Skip(1);
        var slice = r.ReadBytes(3);
        Assert.Equal(new byte[] { 2, 3, 4 }, slice.ToArray());
        Assert.Equal(4, r.Position);
    }

    [Fact]
    public void Peek_DoesNotAdvance_AndClampsToRemaining()
    {
        var r = new BigEndianSpanReader([1, 2, 3]);
        var peek = r.Peek(10);                       // asks for more than remains
        Assert.Equal(new byte[] { 1, 2, 3 }, peek.ToArray());
        Assert.Equal(0, r.Position);                 // peek never moves the cursor
    }

    [Fact]
    public void Skip_And_Seek_MovePosition()
    {
        var r = new BigEndianSpanReader([1, 2, 3, 4]);
        r.Skip(2);
        Assert.Equal(2, r.Position);
        r.Seek(0);
        Assert.Equal((byte)1, r.ReadByte());
    }

    [Fact]
    public void ReadFixedString_StopsAtNul_AndConsumesWholeField()
    {
        var r = new BigEndianSpanReader([(byte)'a', (byte)'b', 0x00, 0x00]);
        Assert.Equal("ab", r.ReadFixedString(4));
        Assert.Equal(4, r.Position);                 // the trailing NUL padding is consumed
    }

    [Fact]
    public void ReadFixedString_DecodesMacRomanHighBytes()
    {
        // OSType "govt" with a Mac-Roman accent: o-umlaut is 0x9A in Mac Roman (U+00F6),
        // which Latin-1 would mangle into a U+009A control char.
        var r = new BigEndianSpanReader([(byte)'g', 0x9A, (byte)'v', (byte)'t']);
        Assert.Equal("gövt", r.ReadFixedString(4));
    }

    [Fact]
    public void ReadPString_ReadsLengthPrefixed_AndConsumesWholeField()
    {
        // 8-byte field: [len=2]['H']['i'][unused padding...]
        var data = new byte[8];
        data[0] = 0x02;
        data[1] = (byte)'H';
        data[2] = (byte)'i';
        var r = new BigEndianSpanReader(data);
        Assert.Equal("Hi", r.ReadPString(8));
        Assert.Equal(8, r.Position);                 // advances by fieldSize regardless of len
    }

    [Fact]
    public void ReadPString_ClampsDeclaredLengthToField()
    {
        // Declared length 9 overruns the 4-byte field; the reader clamps to fieldSize-1 = 3.
        // Byte 0x09 is a structural length prefix (deliberately too large), not text,
        // so it stays an explicit byte rather than a UTF-8 string literal (IDE0230).
        var r = new BigEndianSpanReader([0x09, (byte)'A', (byte)'B', (byte)'C']);
        Assert.Equal("ABC", r.ReadPString(4));
        Assert.Equal(4, r.Position);
    }

    [Fact]
    public void ReadPString_DecodesMacRoman()
    {
        // e-acute is 0x8E in Mac Roman (U+00E9).
        var r = new BigEndianSpanReader([0x01, 0x8E]);
        Assert.Equal("é", r.ReadPString(2));
    }
}
