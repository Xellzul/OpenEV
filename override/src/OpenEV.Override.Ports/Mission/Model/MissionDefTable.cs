namespace OpenEV.Override.Ports.Mission.Model;

// One resolved mission-definition record (formerly 0x1c bytes in the BSS table
// behind the PEF-relocated ptr cell 0x10081140 → 0x101013e8). Filled by
// ResolveSingleMissionSpawn from the 'mïsn' resource when a mission is offered;
// read by Accept/RunSingleMissionDialog/RunMissionBbsDialog/LoadMissionResource/
// SubstituteMissionDescTags.
public sealed class MissionDefRecord
{
    public short TargetSpob;     // +0x00 resolved destination spob (-1 = none)
    public short TargetSystem;   // +0x02 its owning system
    public short CargoType;      // +0x04 (resource +0x10; 1000 = random 0..5)
    public short CargoQty;       // +0x06 (MissionAvailTable +0x0e)
    public short ReturnSpob;     // +0x08 resolved return spob (resource +0xe; -1 -> TargetSpob)
    public short ReturnSystem;   // +0x0a its owning system
    public short ControlBitLink; // +0x0c (resource +0x5e; -1 on pre-1.0.2 short resources)
    public short DeadlineYear;   // +0x0e
    public short DeadlineMonth;  // +0x10
    public short DeadlineDay;    // +0x12
    // +0x14..+0x1a pad / unidentified (no reader in the decompile).
}

// Typed managed mission-definition table. The original BSS range
// [0x101013e8, 0x10104be8) (512 × 0x1c), the int resource-handle cell right
// after it (0x10104be8) and both PEF-relocated ptr cells (0x10081140 /
// 0x10081150) are now retired.
public static class MissionDefTable
{
    public const int Count = 512;

    public static readonly MissionDefRecord[] Store = CreateStore();
    private static MissionDefRecord[] CreateStore()
    {
        var s = new MissionDefRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MissionDefRecord();
        return s;
    }

    // The loaded 'mïsn'/spob resource handle (was the BSS int behind ptr cell
    // 0x10081150, written through the cell by Load/ResolveSingleMissionSpawn).
    public static int ResourceHandle;
}
