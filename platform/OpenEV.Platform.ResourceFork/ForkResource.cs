namespace OpenEV.Platform.ResourceFork;

/// <summary>
/// One resource inside a classic Mac resource fork. <see cref="RawType"/> is the 4 OSType
/// bytes packed big-endian (e.g. 0x73689570). The record preserves the per-resource
/// <see cref="Attributes"/> byte and on-disk ordering so a round-trip can reproduce a
/// faithful fork.
/// <para>
/// <see cref="TypeCode"/> renders the 4 OSType bytes as a Mac Roman string with their real
/// accented bytes (e.g. "shïp", "spöb"). Any game-specific reading of those codes (such as
/// EV Override's ASCII type dispatch "ship"/"spob") is not a resource-fork concern and lives
/// in the consuming layer, not here.
/// </para>
/// </summary>
public sealed record ForkResource(uint RawType, short Id, string? Name, byte[] Data, byte Attributes = 0)
{
    /// <summary>
    /// The 4 OSType bytes rendered as a Mac Roman string (e.g. "shïp", "spöb", "nëbu")
    /// with their real accented bytes. Used for display.
    /// </summary>
    public string TypeCode => MacRoman.GetString(
    [
        (byte)(RawType >> 24), (byte)(RawType >> 16), (byte)(RawType >> 8), (byte)RawType,
    ]);

    /// <summary>Pack 4 OSType bytes (big-endian) into a uint.</summary>
    public static uint PackType(byte a, byte b, byte c, byte d) =>
        ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | d;
}
