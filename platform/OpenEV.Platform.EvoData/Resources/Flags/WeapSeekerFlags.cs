namespace OpenEV.Platform.EvoData.Resources.Flags;

[System.Flags]
public enum WeapSeekerFlags : ushort
{
    None                          = 0,
    PassesOverAsteroids           = 0x0001,
    DecoyedByAsteroids            = 0x0002,
    DecoyedByFlares               = 0x0004,
    ConfusedBySensorInterference  = 0x0008,
    JammingType1Half              = 0x0010,
    JammingType2Half              = 0x0020,
    JammingType3Half              = 0x0040,
    JammingType4Half              = 0x0080,
    JammingType1Full              = 0x0100,
    JammingType2Full              = 0x0200,
    JammingType3Full              = 0x0400,
    JammingType4Full              = 0x0800,
    TurnSpeed30                   = 0x1000,
    TurnSpeed60                   = 0x2000,
    LosesLockIfNotAhead           = 0x4000,
    MayAttackParentIfJammed       = 0x8000,
}
