using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Mission.Model;

// Typed managed C# object for ONE active-mission runtime-STATE record (formerly 0x12
// bytes in the EvoMemory byte-dictionary at _DAT_1008a544 + index*0x12, since removed —
// see Misc.OriginalGameStateTotalBytes). One instance per active-mission slot, held in
// MissionStateTable.Store[8].
//
// Only the first 0x0a of the 0x12-byte stride is ever read or written anywhere in
// the decompile (offsets 0x0a-0x11 are unused stride padding, not a missed field);
// every byte the original game actually touches has a named field below.
public sealed class MissionStateRecord
{
    // +0x00  runtime active/encountered flag (gates govt processing; init 0).
    public byte IsActive;

    // Per active-mission runtime flags (init 0); ApplyMissionCompletion fires when ArrivedAtTarget && ObjectiveComplete.
    // +0x01 set when the player reaches the mission's destination/return stellar
    // (AcceptMission/FUN_1004a570; CheckMissionEncounter/FUN_1004e648).
    public byte ArrivedAtTarget;
    // +0x02 mission ship/cargo objective satisfied — recomputed each tick per the mission's ShipGoal (UpdateMissionStatusFlags).
    public byte ObjectiveComplete;
    // +0x03 the mission has failed/aborted — set directly by UpdateMissionStatusFlags
    // (time-limit expiry, impossible goal) and CheckContrabandScan (contraband scan
    // tripped); MarkMissionFailed (the shared fail-bit/abort finalizer) also sets it,
    // from its callers in UpdateMissionStatusFlags (time-limit path), CheckContrabandScan,
    // ApplyShipDamage, and UpdateShipSlotTick — all the same fail flag.
    public byte Failed;

    // +0x04 / +0x06 / +0x08  the record's DEADLINE date (Year/Month/Day): compared to
    // Core.Model.GameDate.Current and FormatDateLongFull'd as a "deadline" in
    // SubstituteMissionDescTags; restored from the pilot save.
    public short DeadlineYear;
    public short DeadlineMonth;
    public short DeadlineDay;

    // ── pilot-file serialization (the save block-copies the 0x12-byte record) ──
    // Straight into/out of the managed pilot save block at the record's offset.
    public void WriteTo(PilotBlock block, int off)
    {
        block.SetByte(off + 0x00, IsActive);
        block.SetByte(off + 0x01, ArrivedAtTarget);
        block.SetByte(off + 0x02, ObjectiveComplete);
        block.SetByte(off + 0x03, Failed);
        block.SetShort(off + 0x04, DeadlineYear);
        block.SetShort(off + 0x06, DeadlineMonth);
        block.SetShort(off + 0x08, DeadlineDay);
    }
    public void ReadFrom(PilotBlock block, int off)
    {
        IsActive = block.ByteAt(off + 0x00);
        ArrivedAtTarget = block.ByteAt(off + 0x01);
        ObjectiveComplete = block.ByteAt(off + 0x02);
        Failed = block.ByteAt(off + 0x03);
        DeadlineYear = block.ShortAt(off + 0x04);
        DeadlineMonth = block.ShortAt(off + 0x06);
        DeadlineDay = block.ShortAt(off + 0x08);
    }
}
