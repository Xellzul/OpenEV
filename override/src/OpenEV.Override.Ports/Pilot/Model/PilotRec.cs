namespace OpenEV.Override.Ports.Pilot.Model;

// Typed facade over the MAIN pilot save record — now a MANAGED PilotBlock
// (byte[0x26ee], big-endian), no longer an EvoMemory-backed Mac Handle. Get it
// via `Pilot.Model.PilotData.Record`; the AUX block's facade is `PilotData.Aux`
// (PilotAuxRec below).
//
// Layout transcribed from the serializer SavePilotFile.cs (FUN_1001a868) — that
// is the authoritative field map.
//
// Named `PilotRec` (not `Pilot`) — `Pilot` is the namespace, and the host already
// has a `Pilot` class elsewhere.
public readonly struct PilotRec
{
    public readonly PilotBlock Block;
    public PilotRec(PilotBlock block) { Block = block; }

    // ---- MAIN record scalar header (offsets from SavePilotFile) -------------------
    /// +0x00 short — the spob the pilot is docked at when saved (save callers pass
    /// the home/landed spob index; the loader spawns the ship at
    /// Core.Model.GameData.Spobs[this].System / XPos / YPos and stores it in ship +0x52).
    public short DockedSpobIndex { get => Block.ShortAt(0x00); set => Block.SetShort(0x00, value); }
    /// +0x02 short — player ship class (saved from ShipTable.Base +0x36).
    public short ShipClass { get => Block.ShortAt(0x02); set => Block.SetShort(0x02, value); }
    /// +0x10 short — shield, saved as (short)(int) of the int-valued +0x68 float
    /// (a real new-pilot save = 180, the shuttle shield max). The current-format
    /// loader ignores this and recomputes from EffectiveShieldMax; the legacy
    /// importer restores it. See SavePilotFile.cs for the rationale.
    public short Shield { get => Block.ShortAt(0x10); set => Block.SetShort(0x10, value); }
    /// +0x12 short — fuel (saved from ShipTable.Base +0x18, float→int truncated).
    public short Fuel { get => Block.ShortAt(0x12); set => Block.SetShort(0x12, value); }
    /// +0x14/+0x16/+0x18 short — save date (month/day/year, Core.Model.GameDate).
    public short DateMonth { get => Block.ShortAt(0x14); set => Block.SetShort(0x14, value); }
    public short DateDay { get => Block.ShortAt(0x16); set => Block.SetShort(0x16, value); }
    public short DateYear { get => Block.ShortAt(0x18); set => Block.SetShort(0x18, value); }
    /// +0x11ba int — credits (saved from ShipTable.Base +0x60).
    public int Credits { get => Block.IntAt(0x11ba); set => Block.SetInt(0x11ba, value); }
    /// +0x26ea int — player day counter (last 4 bytes of the record).
    public int DayCounter { get => Block.IntAt(0x26ea); set => Block.SetInt(0x26ea, value); }

    // ---- MAIN record array regions (base offset, element stride, count) -----------
    // Source in [brackets] is what SavePilotFile copies in/out of each region.
    /// short[6] @ +0x04  [player ship +0x3a array].
    public const int ShipSlotCount = 6;
    public short ShipSlot(int i) => Block.ShortAt(0x04 + i * 2);
    public void SetShipSlot(int i, short v) => Block.SetShort(0x04 + i * 2, v);

    /// short[1000] @ +0x1a  [per-system state, syst record +0x40].
    public const int SystStateCount = 1000;
    public short SystState(int i) => Block.ShortAt(0x1a + i * 2);
    public void SetSystState(int i, short v) => Block.SetShort(0x1a + i * 2, v);

    /// short[128] @ +0x7ea  [Outfit.Model.OwnedOutfitGrid.Store — outfits the player owns].
    public const int OwnedOutfitCount = 128;
    public short OwnedOutfit(int i) => Block.ShortAt(0x7ea + i * 2);
    public void SetOwnedOutfit(int i, short v) => Block.SetShort(0x7ea + i * 2, v);

    /// short[1000] @ +0x8ea  [kill count by system].
    public const int KillsBySystCount = 1000;
    public short KillsBySyst(int i) => Block.ShortAt(0x8ea + i * 2);
    public void SetKillsBySyst(int i, short v) => Block.SetShort(0x8ea + i * 2, v);

    /// short[64] @ +0x10ba  [player ship weapon slot types].
    public const int WeaponTypeCount = 64;
    public short WeaponType(int i) => Block.ShortAt(0x10ba + i * 2);
    public void SetWeaponType(int i, short v) => Block.SetShort(0x10ba + i * 2, v);

    /// short[64] @ +0x113a  [player ship weapon slot ammo].
    public const int WeaponAmmoCount = 64;
    public short WeaponAmmo(int i) => Block.ShortAt(0x113a + i * 2);
    public void SetWeaponAmmo(int i, short v) => Block.SetShort(0x113a + i * 2, v);

    /// byte[512] @ +0x1e7e  [control bits, Core.Model.ControlBits].
    public const int ControlBitCount = 512;
    public byte ControlBit(int i) => Block.ByteAt(0x1e7e + i);
    public void SetControlBit(int i, byte v) => Block.SetByte(0x1e7e + i, v);

    /// byte[1500] @ +0x207e  [per-spob trading-enabled bit, spob record +0x20].
    public const int SpobScannedCount = 1500;
    public byte SpobScanned(int i) => Block.ByteAt(0x207e + i);
    public void SetSpobScanned(int i, byte v) => Block.SetByte(0x207e + i, v);

    /// short[36] @ +0x265a  [saved ship classes in state 6 (escort; +1000 = carried)].
    public const int EscortClassCount = 36;
    public short EscortClass(int i) => Block.ShortAt(0x265a + i * 2);
    public void SetEscortClass(int i, short v) => Block.SetShort(0x265a + i * 2, v);

    /// short[36] @ +0x26a2  [saved ship classes in state 5 (carried/captured)].
    public const int CarriedClassCount = 36;
    public short CarriedClass(int i) => Block.ShortAt(0x26a2 + i * 2);
    public void SetCarriedClass(int i, short v) => Block.SetShort(0x26a2 + i * 2, v);

    // Mission blocks — MissionStateRecord/MissionRecord serialize themselves straight
    // into the block via their (PilotBlock, offset) WriteTo/ReadFrom overloads.
    /// mission-state records [8] × 0x12 @ +0x11be  [Mission.Model.MissionStateTable].
    public const int MissionStatesCount = 8;
    public int MissionStateOffset(int i) => 0x11be + i * 0x12;
    /// mission records [8] × 0x186 @ +0x124e  [Mission.Model.MissionTable].
    public const int MissionRecordsCount = 8;
    public int MissionRecordOffset(int i) => 0x124e + i * 0x186;
}

// Typed facade over the AUX/galaxy block (PilotData.AuxBlock, 0x22fe bytes).
// Layout transcribed from SavePilotFile; get it via `Pilot.Model.PilotData.Aux`.
public readonly struct PilotAuxRec
{
    public readonly PilotBlock Block;
    public PilotAuxRec(PilotBlock block) { Block = block; }

    public const int SpobCount = 1500, MissionCount = 512, CronCount = 128, JunkCount = 128;

    /// +0x00 short — format marker, written 0x6b (0x69 = legacy importer format).
    public short Magic { get => Block.ShortAt(0x00); set => Block.SetShort(0x00, value); }
    /// +0x02 short — Core.Model.WorldState.StrictPlay (saved as 0/1).
    public short WorldFlag { get => Block.ShortAt(0x02); set => Block.SetShort(0x02, value); }
    /// +0x1ff4 byte — format marker, written 1.
    public byte Marker1ff4 { get => Block.ByteAt(0x1ff4); set => Block.SetByte(0x1ff4, value); }

    /// short[1500] @ +0x04  [spob Tribute].
    public short SpobTribute(int i) => Block.ShortAt(0x04 + i * 2);
    public void SetSpobTribute(int i, short v) => Block.SetShort(0x04 + i * 2, v);

    /// short[512] @ +0xbbc  [mission available flag, 0/1].
    public short MissionAvailable(int i) => Block.ShortAt(0xbbc + i * 2);
    public void SetMissionAvailable(int i, short v) => Block.SetShort(0xbbc + i * 2, v);

    /// short[512] @ +0xfbc  [mission accepted flag, 0/1].
    public short MissionAccepted(int i) => Block.ShortAt(0xfbc + i * 2);
    public void SetMissionAccepted(int i, short v) => Block.SetShort(0xfbc + i * 2, v);

    /// short[1500] @ +0x143c  [spob +0x18].
    public short SpobField18(int i) => Block.ShortAt(0x143c + i * 2);
    public void SetSpobField18(int i, short v) => Block.SetShort(0x143c + i * 2, v);

    /// short[128] @ +0x1ff6  [cron +0x0c].
    public short CronField0c(int i) => Block.ShortAt(0x1ff6 + i * 2);
    public void SetCronField0c(int i, short v) => Block.SetShort(0x1ff6 + i * 2, v);

    /// short[128] @ +0x20f6  [cron +0x02].
    public short CronField02(int i) => Block.ShortAt(0x20f6 + i * 2);
    public void SetCronField02(int i, short v) => Block.SetShort(0x20f6 + i * 2, v);

    /// short[128] @ +0x21f6  [Core.Model.JunkTable PlayerQty].
    public short JunkPlayerQty(int i) => Block.ShortAt(0x21f6 + i * 2);
    public void SetJunkPlayerQty(int i, short v) => Block.SetShort(0x21f6 + i * 2, v);

    /// short[2] @ +0x22f6  [Core.Model.WorldState.StarDrift — the starfield drift pair;
    /// "GalaxyState" kept as the save-layout name].
    public short GalaxyState(int i) => Block.ShortAt(0x22f6 + i * 2);
    public void SetGalaxyState(int i, short v) => Block.SetShort(0x22f6 + i * 2, v);

    /// short[2] @ +0x22fa  [Core.Model.WorldState.StarJitter].
    public short StarJitter(int i) => Block.ShortAt(0x22fa + i * 2);
    public void SetStarJitter(int i, short v) => Block.SetShort(0x22fa + i * 2, v);
}
