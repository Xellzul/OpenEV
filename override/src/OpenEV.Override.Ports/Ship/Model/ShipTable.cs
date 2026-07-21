namespace OpenEV.Override.Ports.Ship.Model;

// Semantic accessor for the in-memory "ship" table. In the decompile `_DAT_1008a4f8`
// holds a POINTER to record[0], each record 0xa82 bytes; record[0] is the player/active
// ship. Same heap-pointer-table contract as SystTable: deref, THEN add the field offset —
// reading `*_DAT_1008a4f8` as a raw float (dropping the deref) is a transcription bug the
// baked-in deref here prevents.
public static class ShipTable
{
    public const int Stride = 0xa82;

    // Synthetic record base in the 0x30 FREE band. Records live in Store[]; this value
    // only feeds *Rec/At index arithmetic. Must NOT be 0x60 — that band is
    // MacPixMap.HandleBase.
    public const int Base = 0x3090_0000;

    // record[0] = player, [1..35] = NPCs; the per-frame loops iterate 1..35.
    public const int Count = 36;

    // Typed backing for the ship records. ShipRec maps its Ptr -> Store[index] and
    // reads/writes these fields. Eager (no nulls) and session-lived: the table is
    // allocated once at boot.
    public static readonly ShipRecord[] Store = CreateStore();
    private static ShipRecord[] CreateStore()
    {
        var s = new ShipRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new ShipRecord();
        return s;
    }

    // `ShipTable` is a static class and can't carry an indexer itself, so `Ships[i]`
    // lives on this stateless holder.
    public static readonly ShipArray Ships = default;

    public static ShipRec Player => new ShipRec(Base);

    // Wrap a raw pointer a ported helper already holds. It may be a record base or a
    // sub-address into the middle of a record — the handle just stores it.
    public static ShipRec FromPtr(int ptr) => new ShipRec(ptr);

    // record[0] +0x00 / +0x04 — `*_DAT_1008a4f8` and `_DAT_1008a4f8[1]`.
    public static float PosX => Store[0].PosX;
    public static float PosY => Store[0].PosY;

    // +0x60 int.
    public static int Credits(int index) => Store[index].Credits;
    public static void SetCredits(int index, int value) => Store[index].Credits = value;
}

// Stateless indexer holder exposed as `ShipTable.Ships`.
public readonly struct ShipArray
{
    public ShipRec this[int index] => new ShipRec(ShipTable.Base + index * ShipTable.Stride);
}
