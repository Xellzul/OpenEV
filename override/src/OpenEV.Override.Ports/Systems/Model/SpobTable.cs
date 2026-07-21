namespace OpenEV.Override.Ports.Systems.Model;

// Semantic accessor for the in-memory "spob" (stellar object / planet) table.
// 0x1008a500 (`_DAT_1008a500`) holds a POINTER to record[0]; each record is
// 0x48 bytes. Same heap-pointer-table contract as SystTable (deref, then
// + index*stride + field) — the correct deref is baked in here so the
// dropped-deref transcription bug can't recur.
public static class SpobTable
{
    public const int PtrSlot = 0x1008a500;
    public const int Stride = 0x48;

    public const int Base = 0x3040_0000;   // synthetic record-base in the 0x30 FREE band; records live in Store[], this is address arithmetic only — NOT 0x60, that is MacPixMap.HandleBase

    // Count of records allocated (NewPtr(0x1a5e0) = 1500 × 0x48). Not all are
    // populated; only those with Visible=1 hold live data.
    public const int Count = 1500;

    // Typed managed backing for the spob records. The bytes used to live in the
    // EvoMemory byte-dictionary at [Base, Base + Count*Stride); EvoMemory itself
    // was later removed (see Misc.OriginalGameStateTotalBytes) once every consumer
    // moved to these typed fields. SpobRec maps its Ptr -> Store[index] and
    // reads/writes them. Eager 1500-element store (no nulls); persists for the
    // session.
    public static readonly SpobRecord[] Store = CreateStore();
    private static SpobRecord[] CreateStore()
    {
        var s = new SpobRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new SpobRecord();
        return s;
    }

    // Wrap a raw pointer a ported helper already holds.
    public static SpobRec FromPtr(int ptr) => new SpobRec(ptr);

    // +0x1a int — spob flag bits (e.g. bit 0 landable, bit 5 uninhabited).
    public static int Flags(int index) => Store[index].Flags;

    // +0x06 short — owning government index (−1 = none).
    public static short Govt(int index) => Store[index].Govt;
}
