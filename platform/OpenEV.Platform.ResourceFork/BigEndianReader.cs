namespace OpenEV.Platform.ResourceFork;

/// <summary>Big-endian reads of classic-Mac on-disk integers from a payload, with optional bounds checks.
/// The read sibling of <see cref="BigEndianWriter"/>; consolidates the per-file ReadS16/BE16 helpers.</summary>
public static class BigEndianReader
{
    /// <summary>A big-endian signed 16-bit value at <paramref name="offset"/> (sign-extended). The caller
    /// guarantees the two bytes are in range.</summary>
    public static int ReadS16(byte[] p, int offset) => (short)((p[offset] << 8) | p[offset + 1]);

    /// <summary>A big-endian unsigned 16-bit value at <paramref name="offset"/> (0..65535). The caller
    /// guarantees the two bytes are in range.</summary>

    /// <summary>A big-endian signed 16-bit value, or null when the payload is too short to hold it.</summary>
    public static int? ReadS16OrNull(byte[] p, int offset) =>
        offset + 2 <= p.Length ? ReadS16(p, offset) : null;
}
