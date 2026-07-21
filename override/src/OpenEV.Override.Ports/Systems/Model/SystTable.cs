namespace OpenEV.Override.Ports.Systems.Model;

// Semantic accessor for the in-memory "syst" (star-system) table.
//
// The Mac global at 0x1008a508 (`_DAT_1008a508` in the decompile) held a
// POINTER to a heap-allocated array of system records, each 0x74 bytes wide.
// The decompile ALWAYS dereferences the slot first:
//     *(... )(_DAT_1008a508 + index * 0x74 + field)
//
// This wrapper is the single semantic chokepoint for that arithmetic. Base is
// a synthetic constant now (not a live heap-pointer read); the records live
// entirely in the managed Store below. The value here is in naming the offsets
// and in guarding against the recurring "dropped deref" transcription bug
// (treating 0x1008a508 itself as the array base instead of the pointer slot —
// see IsSystVisible's header for a real instance this caught).
public static class SystTable
{
    public const int PtrSlot = 0x1008a508;   // _DAT_1008a508: ptr to record[0]
    public const int Stride = 0x74;
    public const int Count = 1000;         // alloc 0x1c520 = 1000 × 0x74 (toc+0x1ea8)

    // Synthetic stand-in for the dereferenced _DAT_1008a508, in the 0x30 FREE band.
    // Records live in Store[]; this value only feeds *Rec/At index arithmetic. Must
    // NOT be 0x60 — that band is MacPixMap.HandleBase.
    public const int Base = 0x3030_0000;

    // Typed managed backing — eager 1000-element store (no nulls), persists for the session.
    public static readonly SystRecord[] Store = CreateStore();
    private static SystRecord[] CreateStore()
    {
        var s = new SystRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new SystRecord();
        return s;
    }

    // +0x48 short — EV "Visibility" control value:
    //   -1          : always visible
    //   0  .. 511   : visible only if control bit N is SET
    //   1000 .. 1511: visible only if control bit (N-1000) is CLEAR
    public static short Visibility(int index) => Store[index].Visibility;

    // +0x72 byte — per-system "defined / in-play" flag (0 = not a real system).
    public static byte ShownFlag(int index) => Store[index].ShownFlag;

    // +0x26 + slot*2 short — the system's stellar-object (spob) link slots
    // (4 slots); each holds a spob index, or -1 for an empty slot.
    public static short SpobLink(int index, int slot) => Store[index].StellarLink[slot];
}
