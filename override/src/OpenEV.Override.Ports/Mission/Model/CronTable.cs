namespace OpenEV.Override.Ports.Mission.Model;

// The "cron" (timed news / commodity-price event, 'öops') table — 128 records,
// formerly 0x50 bytes each in the heap behind PTR slot 0x1008a52c (alloc
// toc+0x1ecc). Filled by LoadCronResources, advanced daily by
// TickCronTable, read by the news ticker / outfitter / trade dialogs; fields
// +0x02/+0x0c are pilot-save state. Records are typed managed now.
public static class CronTable
{
    public const int Count = 128;
    public const int PtrSlot = 0x1008a52c;   // alloc site, kept for reference

    public static readonly CronRecord[] Store = CreateStore();
    private static CronRecord[] CreateStore()
    {
        var s = new CronRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new CronRecord();
        return s;
    }
}

// One cron event (offsets = the old 0x50-byte record layout; 'öops' resource
// offsets noted).
public sealed class CronRecord
{
    public short LocationSelector;   // res+0x00 — system/spob selector (reset 0x8001 = -32767; -1 = any visible spob; -2 = remote news)
    public short ChosenSpob;   // runtime — chosen spob (reset -1; SAVED in the pilot file)
    public short Commodity;   // res+0x02 — commodity index (also indexes the STR# 0xfa1 name table)
    public short PriceDelta;   // res+0x04 — price delta
    public short DurationDays;   // res+0x06 — duration (days; < 0 = until control-bit clears)
    public short DailyOdds;   // res+0x08 — daily activation odds vs rng(100) (reset -1)
    public short StateCountdown;   // runtime — state/countdown (0 = idle, set from DailyOdds(+0x08) on fire; reset -1; SAVED)
    public short ControlBit;   // res+0x0a — control-bit link, clamped to [0,0x1ff] else -1 by the loader
    public string Name = ""; // +0x10 — resource NAME (was a Pascal string, 0x3f cap)
}
