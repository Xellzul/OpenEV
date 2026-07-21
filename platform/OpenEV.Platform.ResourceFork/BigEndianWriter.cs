using System.Buffers.Binary;

namespace OpenEV.Platform.ResourceFork;

/// <summary>
/// Minimal growable big-endian byte writer for building classic Mac structures.
/// <paramref name="initialCapacity"/> is only a starting hint to skip the first few growths —
/// the buffer doubles on demand, so an undersized guess just costs a reallocation, never correctness.
/// </summary>
public sealed class BigEndianWriter(int initialCapacity)
{
    private byte[] _buf = new byte[Math.Max(16, initialCapacity)];
    private int _len;

    public int Length => _len;

    private void Ensure(int extra)
    {
        if (_len + extra <= _buf.Length) return;
        int next = Math.Max(_buf.Length * 2, _len + extra);
        Array.Resize(ref _buf, next);
    }

    public void WriteByte(byte b) { Ensure(1); _buf[_len++] = b; }

    public void WriteUInt16(ushort v)
    {
        Ensure(2);
        BinaryPrimitives.WriteUInt16BigEndian(_buf.AsSpan(_len), v);
        _len += 2;
    }

    public void WriteInt16(short v) => WriteUInt16((ushort)v);

    public void WriteUInt24(uint v)
    {
        // No 24-bit BinaryPrimitives helper; emit the three high-to-low bytes directly.
        Ensure(3);
        _buf[_len++] = (byte)(v >> 16);
        _buf[_len++] = (byte)(v >> 8);
        _buf[_len++] = (byte)v;
    }

    public void WriteUInt32(uint v)
    {
        Ensure(4);
        BinaryPrimitives.WriteUInt32BigEndian(_buf.AsSpan(_len), v);
        _len += 4;
    }

    public void WriteInt32(int v) => WriteUInt32((uint)v);

    public void WriteBytes(ReadOnlySpan<byte> s) { Ensure(s.Length); s.CopyTo(_buf.AsSpan(_len)); _len += s.Length; }

    /// <summary>Append <paramref name="count"/> zero bytes.</summary>
    public void Zero(int count) { Ensure(count); Array.Clear(_buf, _len, count); _len += count; }

    /// <summary>The bytes written so far, as a view over the internal buffer (no copy).
    /// Valid until the next write; use <see cref="ToArray"/> when an owned copy is needed.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buf.AsSpan(0, _len);

    public byte[] ToArray() => _buf.AsSpan(0, _len).ToArray();
}
