using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_100611a4 (EV Override-11.c 40640-40678) — true when the weapon (wëap Flags +0x1a) or the ship
// class (+0x17e Flags) is TurretBlind in the target's bearing arc (forward <46° / side <136° / rear):
// the turret can't bear on the target.
public static class IsTurretBlindToTarget
{
    public static bool Run(ShipRec shooter, ShipRec target, short weaponIndex)
    {
        int bearingToTarget = EvMath.HeadingBetween(shooter.PosX, shooter.PosY, target.PosX, target.PosY);
        short relativeAngle = (short)EvMath.AngleDelta(shooter.Heading, (short)bearingToTarget);
        // PickBestWeaponForTarget passes the BEARING (0..359) as the weapon index — an ORIGINAL bug that
        // read past the weapon table (garbage flags); the managed array clamps out-of-range to no bits.
        WeapFlags wpnFlags = (uint)weaponIndex < WeaponTable.Count
            ? (WeapFlags)(ushort)Core.Model.GameData.Weapons[weaponIndex].Flags
            : WeapFlags.None;
        var clsFlags = Core.Model.GameData.ShipClasses[shooter.ShipClass].Flags;

        if (relativeAngle % 180 < 46)
            return (wpnFlags & WeapFlags.TurretBlindFront) != 0 || (clsFlags & ShipFlags.TurretBlindFront) != 0;
        if (relativeAngle % 180 < 136)
            return (wpnFlags & WeapFlags.TurretBlindSides) != 0 || (clsFlags & ShipFlags.TurretBlindSides) != 0;
        return (wpnFlags & WeapFlags.TurretBlindRear) != 0 || (clsFlags & ShipFlags.TurretBlindRear) != 0;
    }
}
