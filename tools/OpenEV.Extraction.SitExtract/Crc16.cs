namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// CRC-16/ARC: reflected, polynomial 0x8005 (table form 0xA001), initial value 0, no
/// final XOR. This — not the CCITT variant some StuffIt format notes claim — is what
/// StuffIt 5 actually uses, verified two ways: The Unarchiver checks fork data with
/// <c>XADCRCHandle IBMCRC16HandleWithHandle:…conditioned:NO</c> (table a001, init 0),
/// and all 39 entry headers of the EV Override 1.0.2 archive match it over the header
/// bytes with the CRC field cleared. Used here for both header and fork verification.
/// </summary>
internal static class Crc16
{
    private static readonly ushort[] Table = BuildTable();

    private static ushort[] BuildTable()
    {
        var table = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            int crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1;
            table[i] = (ushort)crc;
        }
        return table;
    }

    /// <summary>Continue a CRC over more bytes (start with <c>crc = 0</c>).</summary>
    public static ushort Update(ushort crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
            crc = (ushort)((crc >> 8) ^ Table[(crc ^ b) & 0xFF]);
        return crc;
    }

    public static ushort Compute(ReadOnlySpan<byte> bytes) => Update(0, bytes);
}
