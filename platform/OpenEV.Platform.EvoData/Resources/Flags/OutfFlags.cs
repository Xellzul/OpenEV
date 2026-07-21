namespace OpenEV.Platform.EvoData.Resources.Flags;

// oütf resource +0xc / outfit record +0x0e flags word. All five bits are decompile-confirmed
// (FUN_10015e70 ~line 10930 loads Persistent into the record's separate persistent-flag byte;
// FUN_1005b388 lines 37626-37637 test FixedGun/Turret; FUN_1003a2d0 lines 23202/23294 test
// CannotSell/RemoveAfterPurchase) — no other bit is read anywhere in the port or the decompile.
[System.Flags]
public enum OutfFlags : ushort
{
    None = 0,
    FixedGun = 0x0001,
    Turret = 0x0002,
    Persistent = 0x0004,
    CannotSell = 0x0008,
    RemoveAfterPurchase = 0x0010
}
