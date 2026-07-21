namespace OpenEV.Override.Ports.Ship.Model;

// Typed managed backing for the ship-class definition table: 0x40 (64) records of
// 0x196 bytes each (Mac slot `_DAT_1008a4fc` held the POINTER to record[0]; total
// 0x6580). Access is by index via `Store[i]` / `Core.Model.GameData.ShipClasses[i]`
// (typed ShipClassRecord); indexed by ShipRecord.ShipClass. There is no handle /
// raw-pointer layer — no subsystem passes a raw ship-class pointer around, so
// (unlike ShipTable) there is no ShipClassRec/FromPtr/indexer.
public static class ShipClassTable
{
    public const int Count = 0x40;

    // Eager 0x40-element store (no nulls); persists for the session (allocated once
    // at boot). The records' old EvoMemory byte range was removed once every
    // consumer moved to this typed store (see Misc.OriginalGameStateTotalBytes).
    public static readonly ShipClassRecord[] Store = CreateStore();
    private static ShipClassRecord[] CreateStore()
    {
        var s = new ShipClassRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new ShipClassRecord();
        return s;
    }
}
