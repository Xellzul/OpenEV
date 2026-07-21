namespace OpenEV.Platform.EvoData.Resources.Flags;

// shïp resource +0x4a / ship-class record +0x17e flags word. All twelve bits are decompile-confirmed:
// FUN_10015e70 (ship-class loader, lines 11128-11154) tests 1/2/4 for the sprite-scale class and 0x80
// for ShotXOffset; FUN_10027830 line 18712 tests 8 (UseFuelRegen); FUN_10056a2c line 35686 tests 0x100
// (ShowArmorPercentOnTarget); FUN_10059c58/FUN_10063984/FUN_10064b58/FUN_1006c110 test 0x10
// (DisabledAt10PctArmor) at lines 36960/41748/41776/42176/44580; FUN_100610c0 lines 40610-40611 tests
// 0x20/0x40 (AfterburnerAdvancedRating/AfterburnerAlways); FUN_100611a4 lines 40653-40667 tests
// 0x1000/0x2000/0x4000 (TurretBlindFront/Sides/Rear) — no other bit is read anywhere in the port or
// the decompile.
[System.Flags]
public enum ShipFlags : ushort
{
    None = 0,
    JumpSpeed75 = 0x0001,
    JumpSpeed125 = 0x0002,
    JumpSpeed150 = 0x0004,
    UseFuelRegen = 0x0008,
    DisabledAt10PctArmor = 0x0010,
    AfterburnerAdvancedRating = 0x0020,
    AfterburnerAlways = 0x0040,
    UseShotXOffset = 0x0080,
    ShowArmorPercentOnTarget = 0x0100,
    TurretBlindFront = 0x1000,
    TurretBlindSides = 0x2000,
    TurretBlindRear = 0x4000
}
