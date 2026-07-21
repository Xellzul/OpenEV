using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Core.Model;

// 'ëbug' debug/config bit flags.
// Folds the former Resource.IsBugBitSet (FUN_1001e89c, decompile lines 13648-13669)
// and Resource.BugBitFlags into one managed class.
//
// IsSet reads the 'ëbug' resource (type 0x91627567, id 0x80 — a packed array of 32
// 16-bit flags) live each call. The boot path (InitPrefsPathAndBugBits) caches bits
// 0xc/0xd into the managed Stored[] mirror; those are the pilot-save guards read by
// PilotSave (bit 0xc) and SavePilotFile (bit 0xd).
//
// The Stored[] mirror was the BSS byte-per-bit array based at 0x10087079
// (Stored(bit) == byte at 0x10087079 + bit). It is now a divorced managed array.
// In the Mac binary the
// writer reached bits 0xc/0xd GameToc-relative (GameToc-0x15db/-0x15da) while the
// readers reached them absolutely — both now route through Stored/SetStored.
public static class BugBits
{
    private const short ResourceId = 0x80;   // MacResType.Bug ('ëbug') id
    private const int BitCount = 32;     // valid indices 0..31

    // Divorced managed mirror of the former 0x10087079 byte-per-bit cache.
    private static readonly byte[] _stored = new byte[BitCount];

    public static byte Stored(BugBit bit) => _stored[(int)bit];
    // The only writer (InitPrefsPathAndBugBits) stores IsSet's own 0/1 return here.
    public static void SetStored(BugBit bit, bool set) => _stored[(int)bit] = (byte)(set ? 1 : 0);
    public static bool IsStoredSet(BugBit bit) => _stored[(int)bit] != 0;   // cached-mirror read (vs. IsSet's live resource read)

    public static bool IsSet(BugBit bit)
    {
        short bitIndex = (short)bit;
        if (bitIndex < 0 || bitIndex >= BitCount)
            return false;

        // 'ëbug' resource: a packed short[32]. Read the bit's flag word through the
        // Toolbox resource accessor (the resource data lives in managed memory).
        int handle = MacToolbox.GetResource(MacResType.Bug, ResourceId);
        if (handle == 0)
            return false;

        bool set = MacToolbox.ReadResourceShort(handle, bitIndex * 2) != 0;
        MacToolbox.ReleaseResource(handle);
        return set;
    }
}
