namespace OpenEV.Override.Ports.Core.Model;

// Typed managed record for ONE entry of the 512 x 0x1c0 PERS ("person" / named-captain)
// table — the `përs` resource (NOT a mission table; the former "MissionRecord" name was a
// MISNOMER). Formerly the heap behind PTR slot 0x1008a524, indexed by
// ship.PersIndex; slots 510/511 are the special reinforcement/escort entries the
// SpawnReinforcement absolutes hit. Populated by LoadPersResources; per-pilot
// availability/accepted flags reset by InitializeNewPilotWorld and round-tripped by Save/LoadPilotFile.
//
// Layout (from the loader + all reader sites):
//   +0x00..+0x14  eleven shorts (LinkSyst/Govt/AppearGate/AiCourage/Coward/ShipType/
//                 CommQuote/HailQuote/LinkMission/AvailabilityBit/Flags; +0x12/+0x14 are
//                 also read as their HIGH BYTE — AvailabilityBitHighByte/FlagsHighByte)
//   +0x16         short[64] WeaponType   (+ weaponSlot*2)
//   +0x116        short[64] WeaponAmmo
//   +0x196        int Credits, +0x19a float ShieldMultiplier
//   +0x19e        byte AvailableFlag, +0x19f byte AcceptedFlag
//   +0x1a0        byte[0x20] Pascal ship-name
public sealed class PersRecord
{
    public short LinkSyst;
    public short Govt;
    // +0x04 this pers's AiType (ShipAiType — raw-copied onto the spawned ship's AiBehaviorType,
    // SpawnPers.cs:117); a value > 0 is ALSO what makes this pers eligible for a random spawn
    // (SpawnPers.cs:40), hence the field's established "AppearGate" name.
    public ShipAiType AppearGate;
    public short AiCourage;
    public short Coward;    // TickDefenderAi surrender-threshold scale
    public short ShipType;
    public short CommQuote;
    public short HailQuote;    // pers hail snd/line id (SpeakPersHailLine)
    public short LinkMission;
    public short AvailabilityBit;
    public short Flags;   // flags word (+0x14); see PersFlags — most AI fire-gate bits are unnamed

    // Big-endian sub-byte reads of the +0x12/+0x14 shorts (decompile ReadByte forms).
    public byte AvailabilityBitHighByte => (byte)((ushort)AvailabilityBit >> 8);
    public byte FlagsHighByte => (byte)((ushort)Flags >> 8);

    public short[] WeaponType = new short[64];   // +0x16
    public short[] WeaponAmmo = new short[64];   // +0x116

    public int Credits;
    public float ShieldMultiplier;

    public byte AvailableFlag;   // +0x19e
    public byte AcceptedFlag;    // +0x19f

    // +0x1a0 Pascal name (length byte first), 0x20 bytes.
    public byte[] Name = new byte[0x20];
}
