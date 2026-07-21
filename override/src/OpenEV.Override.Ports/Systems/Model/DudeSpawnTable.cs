namespace OpenEV.Override.Ports.Systems.Model;

// The 'düde' NPC spawn-definition table — 128 records, formerly 0x20 bytes each
// in the heap behind PTR slot 0x1008a51c (toc+0x1ebc, alloc 0x1000). The decompile
// spells the slot `iRam1008a51c` and derefs it: `*(short*)(*0x1008a51c + i*0x20 +
// field)`. Records are typed managed now, filled by LoadDudeResources; there is no
// raw heap backing left.
public static class DudeSpawnTable
{
    public const int Count = 128;

    public static readonly DudeSpawnRecord[] Store = CreateStore();
    private static DudeSpawnRecord[] CreateStore()
    {
        var s = new DudeSpawnRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new DudeSpawnRecord();
        return s;
    }
}

// One dude (NPC spawn) definition (offsets = the old record layout; resource offsets noted).
public sealed class DudeSpawnRecord
{
    public const int RollSlotCount = 4;

    public ShipAiType AiType;      // +0x00  res+0 — AI type for the spawned ship; < WimpyTrader → use the ship class's InherentAI
    public short Govt;            // +0x02  res+0x12 − 0x80 (govt index; -1 = none)
    public short[] ShipClass = new short[RollSlotCount];  // +0x04  res+2..8    − 0x80 (ship class per roll slot; -1 = empty)
    public short[] Weight = new short[RollSlotCount];  // +0x0c  res+0x0a..0x10 (spawn weight per roll slot — PickWeightedSlot)
    public short[] MissionBit = new short[RollSlotCount];  // +0x14  res+0x18..0x1e ControlBits gate per roll slot
                                               //        (< 512 = bit index (ControlBits.Count), 1000..1511 = alias; -1 = always)
    public short Flags;           // +0x1c  res+0x14 — bit 0x40 = refuses barter/bribe (InitTradeSession)
    public short BarPattern;      // +0x1e  res+0x16 — 1000..7127: /1000 = bar-type mask, %1000 + 7500 = STR# id (BuildBarDescription)
}
