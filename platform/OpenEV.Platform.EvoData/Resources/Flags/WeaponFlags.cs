namespace OpenEV.Platform.EvoData.Resources.Flags;

// wëap weapon-definition Flags word (resource +0x1c, in-memory record +0x1a). Bit names
// transcribed from the editor's consumer-cited WeapFlagBits table (each is tied to a real
// port consumer, not a raw TMPL guess). Bits 0x0200/0x0400/0x0800 are the smoke-trail TYPE
// selector (two-bit field), not single flags — omitted here; TickProjectile derives the
// streak type from them directly.
[System.Flags]
public enum WeaponFlags : ushort
{
    None                          = 0,
    SpinGraphicContinuously       = 0x0001,
    FiresOnSecondaryTrigger       = 0x0002,
    CyclingStartsOnFirstFrame     = 0x0004,
    DontFireAtEvasiveTargets      = 0x0008,
    FireSoundLoopedSingleInstance = 0x0010,
    ActsAsMissileDecoy            = 0x0020,
    FiresFromAllMatchingSlots     = 0x0040,
    IgnoreCarrierGunXOffset       = 0x0080,
    BlastSafeForPlayer            = 0x0100,   // owner exempt from this weapon's blast-radius damage (only observable for the player)
    TurretBlindFront              = 0x1000,
    TurretBlindSides              = 0x2000,
    TurretBlindRear               = 0x4000,
    AreaBlastDetonation           = 0x8000
}
