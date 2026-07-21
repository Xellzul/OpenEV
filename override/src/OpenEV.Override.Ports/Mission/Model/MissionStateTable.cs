namespace OpenEV.Override.Ports.Mission.Model;

// Semantic accessor for the per-active-mission runtime STATE table — 8 slots, parallel
// to the active-mission MissionTable. 0x1008a544 (`_DAT_1008a544`) held a POINTER to
// record[0]; each record was 0x12 bytes; 8 records (alloc 0x90 = 8 × 0x12, see
// OriginalGameStateTotalBytes toc+0x1ee4). The bytes used to live in the EvoMemory
// byte-dictionary; EvoMemory itself was later removed once every consumer moved to
// these typed fields in MissionStateTable.Store.
public static class MissionStateTable
{
    public const int Count = 8;

    // Typed managed backing — eager 8-element store (no nulls), persists for the session.
    public static readonly MissionStateRecord[] Store = CreateStore();
    private static MissionStateRecord[] CreateStore()
    {
        var s = new MissionStateRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MissionStateRecord();
        return s;
    }
}
