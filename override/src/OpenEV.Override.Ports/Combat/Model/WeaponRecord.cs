namespace OpenEV.Override.Ports.Combat.Model;

// Typed managed record for ONE "wëap" weapon definition (formerly 0x28 bytes at
// _DAT_1008a510 + index*0x28). The 64 records live in WeaponTable.Store.
// Populated by LoadSpobAndStellarResources. Field widths are
// taken from the loader writes: sixteen int16 at +0x00..+0x1e, then two float32 at
// +0x20 and +0x24 (which together span the full 40-byte record). A few readers read
// the +0x1a/+0x1c flag shorts as a 32-bit big-endian span (high short << 16 | low
// short) for bit tests — those sites reconstruct that explicitly.
public sealed class WeaponRecord
{
    public short Lifetime;   // +0x00  projectile lifetime/TTL (res +0x02 → projectile countdown timer). Reload is separate at +0x24.
    public short MassDamage;   // +0x02  hull/shield damage component (res +0x04; v1 "MassDamage")
    public short EnergyDamage;   // +0x04  scaled damage component (res +0x06; v1 "EnergyDamage")
    public short GuidanceType;   // +0x06  guidance / type code (-1/0/1/3/4/6/7/8/99) — most-read
    public short AmmoLink;   // +0x08  ammo / link
    public short SpriteIndex;   // +0x0a  graphic index (WeaponDefTable)
    public short Inaccuracy;   // +0x0c  inaccuracy
    public short FireSound;   // +0x0e  fire-sound index (WeaponSoundTable)
    public short ImpactDamage;  // +0x10  special/impact effect (res +0x14; negative → status flag on hit; v1 "Impact")
    public short ExplosionType;  // +0x12  explosion kind
    public short ShotOffset;  // +0x14  shot/targeting offset
    public short Submunitions;  // +0x16  submunition count
    public short AnimationRate;  // +0x18  projectile sprite-cycle period in frames (res +0x22; v1 mislabels "Decay")
    public short Flags;  // +0x1a  weapon flags bitfield (often read as ushort / 32-bit span)
    public short SeekerFlags;  // +0x1c  seeker / guidance flags
    public short TrailSmokeSet;  // +0x1e  trail/smoke set (clamped < 8)
    public float ProjectileSpeed;  // +0x20  projectile speed
    public float ReloadTime;  // +0x24  reload / cooldown

    // Variable-offset short read (the weapon-class priority list: PickBest/Forward read
    // +2 + i*2). Maps a byte offset to the corresponding int16 field.
    public short ShortAt(int off) => off switch
    {
        0x0 => Lifetime,
        0x2 => MassDamage,
        0x4 => EnergyDamage,
        0x6 => GuidanceType,
        0x8 => AmmoLink,
        0xa => SpriteIndex,
        0xc => Inaccuracy,
        0xe => FireSound,
        0x10 => ImpactDamage,
        0x12 => ExplosionType,
        0x14 => ShotOffset,
        0x16 => Submunitions,
        0x18 => AnimationRate,
        0x1a => Flags,
        0x1c => SeekerFlags,
        0x1e => TrailSmokeSet,
        _ => throw new System.ArgumentOutOfRangeException(nameof(off)),
    };
}
