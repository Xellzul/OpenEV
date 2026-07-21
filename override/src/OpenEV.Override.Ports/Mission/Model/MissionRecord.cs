using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Mission.Model;

// Typed managed C# object for ONE entry of the active-MISSION DETAIL table (8 slots,
// 0x186 bytes each, at _DAT_1008a540 + index*0x186; held in MissionTable.Store[8]).
// This is the loaded `mïsn` mission record (TargetSpob/ReturnSpob/MissionGoalType/the
// on-accept/complete/fail text ids/TimeLimit/completion bits/runtime progress
// counters), not a government record.
// Populated by Mission/LoadMissionResource; the raw address range was retired once this
// record moved to typed managed fields (see Misc/OriginalGameStateTotalBytes).
//
// The record is large (0x186 bytes, ~40 offsets); high-confidence offsets are named
// (each field's own comment states whether it's decompile-stated or usage-traced),
// the rest stay Field0xNN pending further evidence.
public sealed class MissionRecord
{
    public short TargetSpob;    // +0x00  short
    public short ReturnSpob;    // +0x04  short

    // Fleet-spawn descriptor fields (FUN_10064b58, all short unless noted).
    public short SpawnCount;    // +0x06  spawn count
    public short ShipToBoardOrScan;    // +0x08  ship/mission id to spawn
    public MissionGoalKind MissionGoalType;    // +0x0a  res+0x26 mïsn "ShipGoal" (see MissionGoalKind)
    // +0x0c  special-ship behaviour (mïsn ShipBehav, res+0x28): -1 standard AI, 0 always
    // attack player, 1 protect player, 9 hyper-in together (delayed), 10 hyper-in + attack,
    // 11 hyper-in + protect (mïsn TMPL MissionShipBehaviours, confirmed at every call site:
    // IsPlayerEngagementTarget.cs, ShipAi.cs, UpdateShipAiObjective.cs, PayEscortWages.cs,
    // FindNextShipSlot.cs/FindPrevShipSlot.cs, RunFleetSpawner.cs, SpawnMissionNpc.cs).
    // Kept `short`, not an enum: callers do genuine arithmetic on the raw packed value
    // (`% 10`, `/ 10 * 10`, "subtract 10 while > 8" reduction loops, `> 8`/`< 9` range
    // tests) across the WHOLE two-digit domain, not just these six named states.
    public short ShipBehavior;
    public short DestSystem;    // +0x0e  match key (-6 = special)

    // +0x10  short — cargo/commodity type index (−1 = none) into the STR# 0xfa1
    // commodity-name table (ResourceGlobals.NamesStr0fa1); loaded from the
    // mission resource's CargoType field, NOT a government index.
    public short CargoStringIndex;

    // +0x12  short — value summed into player carried-mass (FUN_100592d4).
    public short CargoMass;

    // +0x14  res+0x14 mïsn "PickupMode" — when the mission cargo is picked up
    // (see MissionCargoPickupMode for values + call-site citations).
    public MissionCargoPickupMode PickupMode;

    // +0x18  short — the govt's scan/captain pers index (−1 = none); matched against
    // a ship's pers link (ShipRec.Field0x5c) to pick the scanning government
    // (FUN_10000f1c interceptor scan, FUN_... contraband scan).
    public short ScanPersIndex;

    // Fields populated by the 'mïsn' resource loader (LoadMissionResource /
    // FUN_1004adf8; resource offset noted per field).
    public short DropOffMode;   // +0x16  short — res+0x16 (with the +0x14/+0x18 trio)
    // Number of completion-bit links (A/B/C/D) fired when a mission resolves
    // (ApplyMissionCompletionBits / ApplyMissionCompletion loop over these four).
    public const int CompletionBitCount = 4;
    public short CompletionBitA;   // +0x1a  short — res+0x2c
    public short CompletionBitB;   // +0x1c  short — res+0x4e
    public short CompletionBitC;   // +0x1e  short — res+0x52
    public short CompletionBitD;   // +0x20  short — res+0x54
    public short CargoType;   // +0x22  short — res+0x2e, −0x80 normalized (cargo type)
    public short CargoQty;   // +0x24  short — res+0x30 (cargo qty; zeroed if +0x22 out of range)
    public int Pay;   // +0x2a  INT   — res+0x1c (mission pay)
    public short AcceptText;   // +0x3e  short — res+0x34
    // +0x40  short — res+0x36. The active-missions browse/info dialog's description
    // text: RunMissionInfoDialog and MissionSelectDialogFilter.RebuildSelectedRowText
    // both LoadDescriptionText.Load it for the selected row. Distinct from AcceptText
    // (+0x3e), which is a one-shot movie+text shown only at accept time. Corroborated
    // by the mïsn TMPL schema: res+0x36 = "QuickBrief", "dësc shown when the player
    // asks for the briefing again (active-mission info)".
    public short MissionInfoText;
    public short LoadCargoText;   // +0x42  short — res+0x38
    public short DumpCargoText;   // +0x44  short — res+0x3a
    public short CompletionText;   // +0x46  short — res+0x3c
    public short FailText;   // +0x48  short — res+0x3e
    // +0x4a  short — res+0x58. Corroborated by the mïsn TMPL schema: res+0x58 =
    // "RefuseText", "dësc shown when a bar/ship-offered mission is refused."
    public short RefuseText;
    public short Field0x52;   // +0x52  short — res+0x44 (write-only: no TMPL field documented at
                               // this offset — a gap between CanAbort +0x42/+0x43 and AvailBitClr
                               // +0x46 — and no other port consumer; left unnamed pending evidence)
    public short MissionDefIndex;   // +0x56  short — the mission-definition index this record was loaded from
    public short AuxSpawnSystem;   // +0x5e  short — res+0x4c

    // Mission-objective progress counters (per active mission; FUN_1004ead4): reset to 0 on
    // accept/load, bumped as the player neutralises the mission's ships, goal completes once
    // the relevant counter reaches GoalThreshold (UpdateMissionStatusFlags).
    public short DestroyedShipCount;   // +0x2e  ships DESTROYED (goal type 0; any kill also FAILS a disable goal)
    public short BoardedShipCount;     // +0x30  ships BOARDED   (goal types 2/5)
    public short DisabledShipCount;    // +0x32  ships DISABLED  (goal type 1)
    // +0x34  short — count of ShipToBoardOrScan ("special") ships currently spawned for
    // this mission. Named from usage tracing (not decompile-stated): SpawnFleetShips.cs
    // gates the board/scan spawn loop on `SpawnCount - MissionShipsSpawnedCount` and bumps
    // it per ship spawned; UpdateMissionStatusFlags.cs treats it as the goal-type-4
    // progress counter (same family as DestroyedShipCount/BoardedShipCount/DisabledShipCount).
    public short MissionShipsSpawnedCount;
    // +0x36  short — count of goal-type-6 mission ships that DEPARTED (completed their
    // jump/expire wind-up and left the system; UpdateShipAiSteering substate 4).
    // UpdateMissionStatusFlags reads DestroyedShipCount + DepartedShipCount vs GoalThreshold
    // (destroy-or-drive-off goal). Name derived from usage tracing, not decompile-stated.
    public short DepartedShipCount;
    public short GoalThreshold;   // +0x38  short — goal-type progress target; init'd from
                                   // SpawnCount in LoadMissionResource (UpdateMissionStatusFlags)

    // +0x26 / +0x28  shorts (res+0x32 / res+0x56) — the two ON-FAIL control-bit links,
    // the fail-path counterpart of CompletionBitA-D (which fire on success).
    // Corroborated by the mïsn plug-in TMPL schema (editor/src/OpenEV.Editor.Schema/
    // Schemas.More.cs): res+0x32/+0x56 = "FailBitSet"/"FailBitSet2", "control bit
    // changed when the mission fails/aborts". Applied via Core.Model.ControlBits:
    // ApplyMissionFailure (FUN_1004c300) applies both AND re-arms any cron keyed on
    // the link; MarkMissionFailed (FUN_1004c908) applies them without the
    // cron step. Either way, a link in [0,0x200) sets ControlBits[link]=1; a link in
    // [1000,0x5e8) clears ControlBits[link-1000] (ControlBits' own alias-band
    // addressing, not a second array).
    public const int FailBitCount = 2;
    public short FailBitA;
    public short FailBitB;

    // +0x3a  byte — contraband-scan armed flag (CheckContrabandScan gate).
    public byte ContrabandScanArmed;

    // +0x3b  byte — abort-mission-on-scan flag, read by MarkMissionFailed (FUN_1004c908),
    // which every non-scan failure path (time limit, ship destroyed/disabled) also runs
    // through — the "OnScan" half of the name may itself be an over-specific Pass-1
    // label; unconfirmed, left as-is pending its own verification.
    public byte AbortMissionOnScan;

    // +0x3c  byte — runtime "active/known" gate (FUN_100592d4: char != 0).
    public byte CargoPickedUp;

    // +0x66  record name (was a Pascal-ish string in the record; the decompile tests
    // its length byte > 0). Populated by LoadMissionResource (random GetIndString
    // pick from the STR# at Field0x4e) and by the pilot-file load.
    public string Name = "";

    // +0x86  the active mission's DISPLAY name (Pascal in-record buffer, 0x100 max):
    // staged by AcceptMission from the 'mïsn' NameTable slot (decompile 30893
    // strncpy to record+0x86, 0x7f), read by BuildMissionsListBox for the
    // missions-list rows (decompile 30195, 0xfa). DISTINCT from Name (+0x66, the
    // govt-name token).
    public string MissionName = "";

    public short TimeLimit;  // +0x4c  short — threshold
    public short NameStrId;  // +0x4e  short — govt-name STR# resource id (>0x7f = valid)
    public short NameStrIndex;  // +0x50  short — govt-name STR# index (pilot load GetIndString)
    // +0x54  short — countdown/delay gate for the next board-or-scan spawn wave (the
    // MissionShipsSpawnedCount analogue of SpawnCountdown for the aux patrol). Named from
    // usage tracing: decrements while > 0, fires exactly at 0, then resets to -1 (dormant)
    // until LoadMissionResource/TickShipAI reroll it to rng(100)+100.
    public short MissionShipSpawnCountdown;
    public MisnFlags Flags;  // +0x58  res+0x50 mïsn "Flags"
    public short AuxShipCount;  // +0x5a  short — res+0x48 "No. of aux ships" (-1 = unlimited); the aux-ship spawn budget
    public short SpawnDudeId;  // +0x5c  short — dude id to spawn
    public short LiveSpawnCount;  // +0x60  short — live count (++ on spawn)
    public short SpawnCountdown;  // +0x62  short — frames until the next aux-ship spawn (set to rng 70-139, counts down)
    public short RemainingSpawnCount;  // +0x64  short — remaining count (-- on spawn)

    // ── pilot-file serialization (SavePilotFile / LoadPilot*) ──────────────
    // The original block-copies the whole 0x186-byte record into the save buffer.
    // Only the NAMED fields have managed storage (unnamed offsets are dead in the port —
    // nothing writes them), so serialize those at their original offsets; the rest
    // of the save image stays zero.
    public void WriteTo(PilotBlock block, int off)
    {
        block.SetShort(off + 0x00, TargetSpob);
        block.SetShort(off + 0x04, ReturnSpob);
        block.SetShort(off + 0x06, SpawnCount);
        block.SetShort(off + 0x08, ShipToBoardOrScan);
        block.SetShort(off + 0x0a, (short)MissionGoalType);
        block.SetShort(off + 0x0c, ShipBehavior);
        block.SetShort(off + 0x0e, DestSystem);
        block.SetShort(off + 0x10, CargoStringIndex);
        block.SetShort(off + 0x12, CargoMass);
        block.SetShort(off + 0x14, (short)PickupMode);
        block.SetShort(off + 0x16, DropOffMode);
        block.SetShort(off + 0x18, ScanPersIndex);
        block.SetShort(off + 0x1a, CompletionBitA);
        block.SetShort(off + 0x1c, CompletionBitB);
        block.SetShort(off + 0x1e, CompletionBitC);
        block.SetShort(off + 0x20, CompletionBitD);
        block.SetShort(off + 0x22, CargoType);
        block.SetShort(off + 0x24, CargoQty);
        block.SetShort(off + 0x26, FailBitA);
        block.SetShort(off + 0x28, FailBitB);
        block.SetInt(off + 0x2a, Pay);
        block.SetShort(off + 0x2e, DestroyedShipCount);
        block.SetShort(off + 0x30, BoardedShipCount);
        block.SetShort(off + 0x32, DisabledShipCount);
        block.SetShort(off + 0x34, MissionShipsSpawnedCount);
        block.SetShort(off + 0x36, DepartedShipCount);
        block.SetShort(off + 0x38, GoalThreshold);
        block.SetByte(off + 0x3a, ContrabandScanArmed);
        block.SetByte(off + 0x3b, AbortMissionOnScan);
        block.SetByte(off + 0x3c, CargoPickedUp);
        block.SetShort(off + 0x3e, AcceptText);
        block.SetShort(off + 0x40, MissionInfoText);
        block.SetShort(off + 0x42, LoadCargoText);
        block.SetShort(off + 0x44, DumpCargoText);
        block.SetShort(off + 0x46, CompletionText);
        block.SetShort(off + 0x48, FailText);
        block.SetShort(off + 0x4a, RefuseText);
        block.SetShort(off + 0x4c, TimeLimit);
        block.SetShort(off + 0x4e, NameStrId);
        block.SetShort(off + 0x50, NameStrIndex);
        block.SetShort(off + 0x52, Field0x52);
        block.SetShort(off + 0x54, MissionShipSpawnCountdown);
        block.SetShort(off + 0x56, MissionDefIndex);
        block.SetShort(off + 0x58, (short)Flags);
        block.SetShort(off + 0x5a, AuxShipCount);
        block.SetShort(off + 0x5c, SpawnDudeId);
        block.SetShort(off + 0x5e, AuxSpawnSystem);
        block.SetShort(off + 0x60, LiveSpawnCount);
        block.SetShort(off + 0x62, SpawnCountdown);
        block.SetShort(off + 0x64, RemainingSpawnCount);
        block.SetPascal(off + 0x66, Name, 0x1f);
        block.SetPascal(off + 0x86, MissionName, 0xfa);
    }
    public void ReadFrom(PilotBlock block, int off)
    {
        TargetSpob = block.ShortAt(off + 0x00);
        ReturnSpob = block.ShortAt(off + 0x04);
        SpawnCount = block.ShortAt(off + 0x06);
        ShipToBoardOrScan = block.ShortAt(off + 0x08);
        MissionGoalType = (MissionGoalKind)block.ShortAt(off + 0x0a);
        ShipBehavior = block.ShortAt(off + 0x0c);
        DestSystem = block.ShortAt(off + 0x0e);
        CargoStringIndex = block.ShortAt(off + 0x10);
        CargoMass = block.ShortAt(off + 0x12);
        PickupMode = (MissionCargoPickupMode)block.ShortAt(off + 0x14);
        DropOffMode = block.ShortAt(off + 0x16);
        ScanPersIndex = block.ShortAt(off + 0x18);
        CompletionBitA = block.ShortAt(off + 0x1a);
        CompletionBitB = block.ShortAt(off + 0x1c);
        CompletionBitC = block.ShortAt(off + 0x1e);
        CompletionBitD = block.ShortAt(off + 0x20);
        CargoType = block.ShortAt(off + 0x22);
        CargoQty = block.ShortAt(off + 0x24);
        FailBitA = block.ShortAt(off + 0x26);
        FailBitB = block.ShortAt(off + 0x28);
        Pay = block.IntAt(off + 0x2a);
        DestroyedShipCount = block.ShortAt(off + 0x2e);
        BoardedShipCount = block.ShortAt(off + 0x30);
        DisabledShipCount = block.ShortAt(off + 0x32);
        MissionShipsSpawnedCount = block.ShortAt(off + 0x34);
        DepartedShipCount = block.ShortAt(off + 0x36);
        GoalThreshold = block.ShortAt(off + 0x38);
        ContrabandScanArmed = block.ByteAt(off + 0x3a);
        AbortMissionOnScan = block.ByteAt(off + 0x3b);
        CargoPickedUp = block.ByteAt(off + 0x3c);
        AcceptText = block.ShortAt(off + 0x3e);
        MissionInfoText = block.ShortAt(off + 0x40);
        LoadCargoText = block.ShortAt(off + 0x42);
        DumpCargoText = block.ShortAt(off + 0x44);
        CompletionText = block.ShortAt(off + 0x46);
        FailText = block.ShortAt(off + 0x48);
        RefuseText = block.ShortAt(off + 0x4a);
        TimeLimit = block.ShortAt(off + 0x4c);
        NameStrId = block.ShortAt(off + 0x4e);
        NameStrIndex = block.ShortAt(off + 0x50);
        Field0x52 = block.ShortAt(off + 0x52);
        MissionShipSpawnCountdown = block.ShortAt(off + 0x54);
        MissionDefIndex = block.ShortAt(off + 0x56);
        Flags = (MisnFlags)block.ShortAt(off + 0x58);
        AuxShipCount = block.ShortAt(off + 0x5a);
        SpawnDudeId = block.ShortAt(off + 0x5c);
        AuxSpawnSystem = block.ShortAt(off + 0x5e);
        LiveSpawnCount = block.ShortAt(off + 0x60);
        SpawnCountdown = block.ShortAt(off + 0x62);
        RemainingSpawnCount = block.ShortAt(off + 0x64);
        Name = block.PascalAt(off + 0x66);
        MissionName = block.PascalAt(off + 0x86);
    }
}
