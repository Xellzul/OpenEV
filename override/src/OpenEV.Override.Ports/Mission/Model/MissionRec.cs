using System;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Mission.Model;

// Typed HANDLE over one active-mission DETAIL record. The record data now lives in a
// managed MissionRecord object (MissionTable.Store[index]) — there is no raw byte-memory
// backing anymore (the EvoMemory byte range that used to hold it was retired once every
// consumer moved to typed fields; see OriginalGameStateTotalBytes).
// Same contract as SpobRec:
//   * `Ptr` / implicit `operator int` return the record's synthetic address —
//     address arithmetic only, for callers that still identify a mission by address.
//   * `Index` maps the Ptr to Store[index]; named properties read/write the
//     typed fields; the generic byte accessors THROW (no byte backing anymore).
//
// Use as `Mission.Model.MissionTable.Missions[i].TargetSpob` / `.FromPtr(ptr)`.
public readonly struct MissionRec
{
    public readonly int Ptr;
    public MissionRec(int ptr) { Ptr = ptr; }

    public bool IsNull => Ptr == 0;

    // Slot index relative to record[0]. (Ptr - Base) / Stride.
    public int Index => (Ptr - MissionTable.Base) / MissionTable.Stride;

    public static implicit operator int(MissionRec g) => g.Ptr;

    private MissionRecord Rec
    {
        get
        {
            int i = Index;
            if ((uint)i >= (uint)MissionTable.Count)
                throw new NotSupportedException(
                    $"MissionRec.Ptr 0x{Ptr:x8} maps to mission index {i} (out of [0,{MissionTable.Count})) — "
                    + "likely a sub-address or stale pointer. The mission record is now a typed object; "
                    + "use a record-aligned handle and a named field.");
            return MissionTable.Store[i];
        }
    }

    // ---- named fields → typed MissionRecord -------------------------------------
    public short TargetSpob { get => Rec.TargetSpob; set => Rec.TargetSpob = value; }
    public short ReturnSpob { get => Rec.ReturnSpob; set => Rec.ReturnSpob = value; }
    public short SpawnCount { get => Rec.SpawnCount; set => Rec.SpawnCount = value; }
    public short ShipToBoardOrScan { get => Rec.ShipToBoardOrScan; set => Rec.ShipToBoardOrScan = value; }
    public MissionGoalKind MissionGoalType { get => Rec.MissionGoalType; set => Rec.MissionGoalType = value; }
    public short ShipBehavior { get => Rec.ShipBehavior; set => Rec.ShipBehavior = value; }
    public short DestSystem { get => Rec.DestSystem; set => Rec.DestSystem = value; }
    public short CargoStringIndex { get => Rec.CargoStringIndex; set => Rec.CargoStringIndex = value; }
    public short CargoMass { get => Rec.CargoMass; set => Rec.CargoMass = value; }
    public short ScanPersIndex { get => Rec.ScanPersIndex; set => Rec.ScanPersIndex = value; }
    public short DestroyedShipCount { get => Rec.DestroyedShipCount; set => Rec.DestroyedShipCount = value; }
    public short BoardedShipCount { get => Rec.BoardedShipCount; set => Rec.BoardedShipCount = value; }
    public short DisabledShipCount { get => Rec.DisabledShipCount; set => Rec.DisabledShipCount = value; }
    public short MissionShipsSpawnedCount { get => Rec.MissionShipsSpawnedCount; set => Rec.MissionShipsSpawnedCount = value; }
    public short DepartedShipCount { get => Rec.DepartedShipCount; set => Rec.DepartedShipCount = value; }
    public short GoalThreshold { get => Rec.GoalThreshold; set => Rec.GoalThreshold = value; }
    public byte CargoPickedUp { get => Rec.CargoPickedUp; set => Rec.CargoPickedUp = value; }
    public short TimeLimit { get => Rec.TimeLimit; set => Rec.TimeLimit = value; }
    public short MissionShipSpawnCountdown { get => Rec.MissionShipSpawnCountdown; set => Rec.MissionShipSpawnCountdown = value; }
    public MisnFlags Flags { get => Rec.Flags; set => Rec.Flags = value; }
    public short AuxShipCount { get => Rec.AuxShipCount; set => Rec.AuxShipCount = value; }
    public short SpawnDudeId { get => Rec.SpawnDudeId; set => Rec.SpawnDudeId = value; }
    public short LiveSpawnCount { get => Rec.LiveSpawnCount; set => Rec.LiveSpawnCount = value; }
    public short SpawnCountdown { get => Rec.SpawnCountdown; set => Rec.SpawnCountdown = value; }
    public short RemainingSpawnCount { get => Rec.RemainingSpawnCount; set => Rec.RemainingSpawnCount = value; }

    // ---- generic byte access — REMOVED (no byte backing) ------------------------
    private static NotSupportedException NoBytes(int off) =>
        new NotSupportedException(
            $"MissionRec generic byte access at +0x{off:x} is gone — the mission record is a typed "
            + "object now. Add a named field to MissionRecord/MissionRec for this offset and use it.");

    public byte ByteAt(int off) => throw NoBytes(off);
    public short ShortAt(int off) => throw NoBytes(off);
    public ushort UShortAt(int off) => throw NoBytes(off);
    public int IntAt(int off) => throw NoBytes(off);
    public uint UIntAt(int off) => throw NoBytes(off);
    public float FloatAt(int off) => throw NoBytes(off);

    public void SetByteAt(int off, byte v) => throw NoBytes(off);
    public void SetShortAt(int off, short v) => throw NoBytes(off);
    public void SetIntAt(int off, int v) => throw NoBytes(off);
    public void SetFloatAt(int off, float v) => throw NoBytes(off);
}
