namespace OpenEV.Override.Ports.Mission.Model;

// Semantic accessor for the active-mission DETAIL table — 8 accepted-mission slots.
// 0x1008a540 (`_DAT_1008a540`) held a POINTER to record[0]; each record is 0x186 bytes;
// 8 records (alloc 0xc30 = 8 × 0x186, see OriginalGameStateTotalBytes toc+0x1ee0).
// Records live in Store; MissionRec maps its Ptr -> Store[index]. Not to be confused
// with the real government table, Systems.Model.GovtTable.
public static class MissionTable
{
    public const int PtrSlot = 0x1008a540;
    public const int Stride = 0x186;
    public const int Count = 8;

    // Synthetic record base in the 0x30 FREE band. Records live in Store[]; this value
    // only feeds *Rec/At index arithmetic. Must NOT be 0x60 — that band is
    // MacPixMap.HandleBase.
    public const int Base = 0x3080_0000;

    // Typed managed backing — eager 8-element store (no nulls), persists for the session.
    public static readonly MissionRecord[] Store = CreateStore();
    private static MissionRecord[] CreateStore()
    {
        var s = new MissionRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MissionRecord();
        return s;
    }

    // `MissionTable` is a static class and can't carry an indexer itself, so
    // `Missions[i]` lives on this stateless holder.
    public static readonly MissionArray Missions = default;
}

// Stateless indexer holder exposed as `MissionTable.Missions`.
public readonly struct MissionArray
{
    public MissionRec this[int index] => new MissionRec(MissionTable.Base + index * MissionTable.Stride);
}
