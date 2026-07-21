using System.Buffers.Binary;

namespace OpenEV.Platform.ResourceFork;

public ref struct BigEndianSpanReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public BigEndianSpanReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
    }

    public readonly int Position => _pos;
    public readonly int Length => _data.Length;
    public readonly int Remaining => _data.Length - _pos;

    public void Seek(int position) => _pos = position;

    public byte ReadByte() => _data[_pos++];

    public short ReadInt16()
    {
        short v = BinaryPrimitives.ReadInt16BigEndian(_data.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public ushort ReadUInt16()
    {
        ushort v = BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public int ReadInt32()
    {
        int v = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public uint ReadUInt32()
    {
        uint v = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _data.Slice(_pos, count);
        _pos += count;
        return slice;
    }

    public readonly ReadOnlySpan<byte> Peek(int count)
    {
        int n = Math.Min(count, Remaining);
        return _data.Slice(_pos, n);
    }

    public void Skip(int count) => _pos += count;

    public string ReadFixedString(int length)
    {
        var buf = ReadBytes(length);
        int nul = buf.IndexOf((byte)0);
        return MacRoman.GetString(nul < 0 ? buf : buf[..nul]);
    }

    // Pascal string: 1-byte length followed by MacRoman bytes.
    public string ReadPString(int fieldSize)
    {
        int start = _pos;
        byte len = ReadByte();
        if (len > fieldSize - 1) len = (byte)(fieldSize - 1);
        var str = MacRoman.GetString(ReadBytes(len));
        _pos = start + fieldSize;
        return str;
    }
}
