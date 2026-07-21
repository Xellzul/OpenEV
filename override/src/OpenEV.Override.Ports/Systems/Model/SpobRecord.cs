namespace OpenEV.Override.Ports.Systems.Model;

// Typed managed C# object for ONE spob (stellar object / planet) record (formerly
// 0x48 bytes in the EvoMemory byte-dictionary, since removed — see
// Misc.OriginalGameStateTotalBytes). One instance per slot, held in
// SpobTable.Store[1500]; the SpobRec handle reads/writes these fields.
//
// Fields are 1:1 with record offsets, all named. An offset not promoted to a
// field has no storage here.
public sealed class SpobRecord
{
    // +0x00 / +0x02  world position (short coords).
    public short XPos;
    public short YPos;

    // +0x04  owning system index (resource system − 0x80; −1 = not loaded).
    public short System;

    // +0x06  owning government index (resource govt − 0x80; −1 = none).
    public short Govt;

    // +0x08  minimum coolness required to land.
    public short MinCoolness;

    // +0x0a  sprite record index (from resource 'Type' field).
    public short SpriteId;

    // +0x0c  tech level (clamped ≥ 1).
    public short TechLevel;

    // +0x0e..+0x12  special tech levels [3].
    public short[] SpecialTech = new short[3];

    // +0x14  custom picture resource id.
    public short CustomPicId;

    // +0x16  custom sound resource id.
    public short CustomSoundId;

    // +0x18  short — runtime tribute-income counter: incremented each TickSpobTributeIncome
    // tick while the planet is dominated/yielding (+TechLevel*1000 credits/tick), reset to 0
    // on tribute-collect / commodity-trade, pilot-saved alongside Tribute (+0x44).
    public short TributeAccrualTicks;

    // +0x1a  spob flag bits (4 bytes). Bit 0 = landable, bit 4 = station,
    // bit 5 = uninhabited (skip landing target), etc.
    public int Flags;

    // +0x1e  byte — 1 once a sprite node has been allocated for this spob (gate in
    // SpawnWorldSpriteNodes to avoid double-spawn; reset on world rebuild). Mirrors
    // AsteroidParticle.Spawned.
    public byte Spawned;

    // +0x1f  visible / loaded flag (1 when resource was found).
    public byte Visible;

    // +0x20  trading-enabled flag (runtime, init 0; set 1 when player docks).
    public byte TradingEnabled;

    // +0x21..+0x3f  resource name (was a 31-byte Pascal buffer; now a managed C# string).
    public string Name = "";

    // (+0x40 is 2 bytes of alignment padding after the 31-byte name buffer — never read
    // or written by the original game — so it is not modelled here.)

    // +0x42  defense dude index (resource DefenseDude − 0x80).
    public short DefenseDude;

    // +0x44  current tribute amount (runtime, computed from TributeMax on load).
    public short Tribute;

    // +0x46  max tribute (resource DefenseCount).
    public short TributeMax;
}
