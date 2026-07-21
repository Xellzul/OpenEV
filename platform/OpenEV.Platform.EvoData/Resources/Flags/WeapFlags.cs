namespace OpenEV.Platform.EvoData.Resources.Flags;

[System.Flags]
public enum WeapFlags : ushort
{
    None = 0,
    SpinGraphicContinuously       = 0x0001,
    FiredBySecondTrigger          = 0x0002,
    CyclingStartsOnFirstFrame     = 0x0004,
    GuidedDontFireAtFastShips     = 0x0008,
    SoundLooped                   = 0x0010,
    ActsAsMissileDecoy            = 0x0020,
    MultipleFireSimultaneously    = 0x0040,
    IgnoreCarrierWeaponXOffset    = 0x0080,
    BlastSafeForPlayer            = 0x0100,
    GeneratesSmallSmoke           = 0x0200,
    GeneratesBigSmoke             = 0x0400,
    SmokeTrailPersistent          = 0x0800,
    TurretBlindFront              = 0x1000,
    TurretBlindSides              = 0x2000,
    TurretBlindRear               = 0x4000,
    DetonatesAtEnd                = 0x8000,
}
