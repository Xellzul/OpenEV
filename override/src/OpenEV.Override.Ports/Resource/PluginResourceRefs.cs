namespace OpenEV.Override.Ports.Resource;

// The six open plugin resource-file refNums (was a fixed-BSS short[6] at 0x100870d0).
// OpenPluginResourceFiles fills them from OpenResFile; the resource manager reads them
// to know which file backs each plugin.
//
// Slot ↔ plugin-file order is the GetIndString 1..6 permutation:
//   file1→slot2, file2→slot3, file3→slot0, file4→slot1, file5→slot4, file6→slot5.
//
// Migrated to a managed array — the old BSS cell (and the resource manager's
// un-greppable GameToc-relative loads of it) is now dead.
public static class PluginResourceRefs
{
    public const int Count = 6;
    private static readonly short[] _refs = new short[Count];

    public static short Ref(int slot) => _refs[slot];
    public static void SetRef(int slot, int refNum) => _refs[slot] = (short)refNum;
}
