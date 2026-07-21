using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Ship.Model;

// Typed managed C# object for ONE ship class record (0x196 bytes). One instance
// per slot, held in ShipClassTable.Store[64] and reached via ShipClassTable.Store[i]
// / Core.Model.GameData.ShipClasses[i]; these fields replace the old EvoMemory bytes
// now that every consumer reads the typed record instead.
public sealed class ShipClassRecord
{
    // +0x00 to +0x28: 21 individual short fields (42 bytes)
    public short Holds;
    public short Maneuver;
    public short BaseFuel;
    public short FreeMass;
    public short BaseArmor;
    public short ShieldRecharge;
    public short MaxGun;
    public short MaxTur;
    public short TechLevel;
    public short DeathDelay;
    public ShipAiType InherentAI;   // +0x14 fallback/default AiBehaviorType for this class (see ShipAiType)
    public short MissionBit;

    public const int DefaultItemSlots = 4;   // built-in outfit slots per class (ASM bound 4)
    // +0x18..+0x1e  built-in outfit ids (res 0x4e..0x54, -0x80-biased).
    public short[] DefaultItems = new short[DefaultItemSlots];
    // +0x20..+0x26  the quantity of each built-in outfit (res 0x56..0x5c).
    public short[] DefaultItemsCount = new short[DefaultItemSlots];
    public short FuelRegen;

    public float Accel;
    public float Speed;
    public float SpriteScale;
    public int Cost;
    // +0x3a int (shield — written as int from short, read as float/int/uint)
    public int Shield;

    // +0x3e resource name (was a 64-byte Pascal buffer; now a managed C# string).
    public string Name = "";

    // Resource-side count of default-weapon-slot readouts (type + 2 ammo counts per
    // slot, res 0x12..0x28) the loader stages before scattering them by outfit id into
    // DefaultWeaponType/DefaultWeaponAmmo below. Distinct from DefaultItemSlots (a
    // different resource sub-range, built-in outfit items) — both are 4 but not the
    // same concept.
    public const int WeaponSlotDefaultCount = 4;

    public const int WeaponSlotCount = 64;   // weapon-loadout slots (ASM bound 0x40)

    // +0x7e weapon slot types (WeaponSlotCount shorts = 128 bytes)
    public short[] DefaultWeaponType = new short[WeaponSlotCount];

    // +0xfe weapon slot ammo (WeaponSlotCount shorts = 128 bytes)
    public short[] DefaultWeaponAmmo = new short[WeaponSlotCount];

    // +0x17e to +0x194: tail short fields (12 shorts = 24 bytes)
    public ShipFlags Flags;
    public short ShotXOffset;
    public short TurretYDisp0;
    public short TurretYDisp1;
    public short TurretYDisp2;
    public short TurretYDisp3;
    public short Mass;
    public short Length;
    public short Crew;
    public short InherentGovt;
    public short SkillLevel;
    public short NegativeHoldsFlag;
}
