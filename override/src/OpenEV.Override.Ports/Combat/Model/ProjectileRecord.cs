namespace OpenEV.Override.Ports.Combat.Model;

// Typed managed C# object for ONE projectile slot (formerly 0x26 bytes in the
// EvoMemory heap at *_DAT_1008a53c + slot*0x26; 128 slots, alloc 0x1300 at
// toc+0x1edc). EvoMemory itself was removed once every reader moved to these
// typed fields — no raw byte backing exists anymore.
//
// Field map derived from the decompile census (FUN_100233bc guidance tick,
// FUN_10023ef8 spawn, intercept/death/threat consumers):
public sealed class ProjectileRecord
{
    // 0xfffe in the decompile. Marks the slot dead; spawners treat any slot
    // <= this as free.
    public const short Killed = -2;

    public float PosX;          // +0x00  world X
    public float PosY;          // +0x04  world Y
    public float VelX;          // +0x08  velocity X
    public float VelY;          // +0x0c  velocity Y
    public short WeaponType;    // +0x10  weapon-table index
    public short TargetSlot;    // +0x12  target index (ship / asteroid / projectile, per Mode)
    public short OwnerSlot;     // +0x14  firing ship slot (-1 once consumed; seeker flag 0x8000 retargets jammed shots at it)
    public short SystemId;      // +0x16  system the shot lives in (vs target ship's CurrentSystem)
    public short Mode;          // +0x18  guidance mode: 0 ship-seek, 1 asteroid-lock, 2 missile-intercept, 999 jammed-spin
    public short LifeRemaining; // +0x1a  ticks left; <=0 = free slot, -2 (0xfffe) = killed
    public short Heading;       // +0x1c  degrees 0..359
    public short DamageFalloffTimer;     // +0x1e
    public short DamageFalloffSteps;     // +0x20
    public short AnimFrame;     // +0x22
    public byte FromGuardingEscort;     // +0x24
}
