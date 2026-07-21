namespace OpenEV.Platform.Toolbox;

// Big-endian byte[] accessors — the packing every Mac heap block, resource, and
// on-disk record uses (68k/PPC byte order). Two read flavors:
//   - ReadInt16/ReadUInt16/ReadInt32 index directly; an out-of-range offset throws,
//     the migration tripwire for a bad offset into a typed managed block.
//   - ReadInt16OrZero/ReadInt32OrZero return 0 past end-of-buffer, matching a faithful
//     over-read of a truncated resource the original walked off the end of (heap noise).
// Do NOT collapse the two — that distinction is behavioral.
public static class BigEndian
{
    public static short ReadInt16(byte[] b, int off)
        => (short)((b[off] << 8) | b[off + 1]);

    public static ushort ReadUInt16(byte[] b, int off)
        => (ushort)((b[off] << 8) | b[off + 1]);

    public static int ReadInt32(byte[] b, int off)
        => (b[off] << 24) | (b[off + 1] << 16) | (b[off + 2] << 8) | b[off + 3];

    public static void WriteInt16(byte[] b, int off, short v)
    {
        b[off] = (byte)(v >> 8);
        b[off + 1] = (byte)v;
    }

    public static void WriteInt32(byte[] b, int off, int v)
    {
        b[off] = (byte)(v >> 24);
        b[off + 1] = (byte)(v >> 16);
        b[off + 2] = (byte)(v >> 8);
        b[off + 3] = (byte)v;
    }

    public static short ReadInt16OrZero(byte[] b, int off)
        => (uint)(off + 1) < (uint)b.Length ? (short)((b[off] << 8) | b[off + 1]) : (short)0;

    public static int ReadInt32OrZero(byte[] b, int off)
        => (uint)(off + 3) < (uint)b.Length
            ? (b[off] << 24) | (b[off + 1] << 16) | (b[off + 2] << 8) | b[off + 3]
            : 0;
}
