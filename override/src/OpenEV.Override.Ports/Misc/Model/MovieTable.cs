namespace OpenEV.Override.Ports.Misc.Model;

// The in-memory QuickTime-movie descriptor table — 128 records, formerly 0x104
// bytes each in the heap behind PTR slot 0x1008a54c (`_DAT_1008a54c`, alloc
// toc+0x1eec). Filled by LoadCargoResources (MISNOMER — the 'dëqt' resources are
// movie descriptors, not cargo) from GetIndResource by INDEX; scanned by
// PlayMovieById. Records are typed managed now.
public static class MovieTable
{
    public const int PtrSlot = 0x1008a54c;   // _DAT_1008a54c (alloc site, kept for reference)
    public const int Count = 128;

    public static readonly MovieRecord[] Store = CreateStore();
    private static MovieRecord[] CreateStore()
    {
        var s = new MovieRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MovieRecord();
        return s;
    }
}

// One movie descriptor (offsets = the old 0x104-byte record layout).
public sealed class MovieRecord
{
    public short Flags;      // +0x00  res+0 — bit 0x1 = auto-play, bit 0x2 = one-shot/already played
    public short MovieId;    // +0x02  resource id callers match on (-1 = empty; loader reset)
    public string Name = ""; // +0x04  resource NAME (was a Pascal string, 0x3f cap) — the
                             //        QuickTime movie FILENAME PlayQuickTimeMovie opens
}
