using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Combat;

// FUN_100612fc — EV Override-11.c lines 40679-40702.
// Returns the firing ship class's ShotXOffset as a beam/shot spread, consumed by
// AllocateBeamSlot (via WeaponSlotTick/UpdateShipAiFrame for beam weapons) and by
// SpawnFromShip otherwise: 0 when the weapon has the IgnoreCarrierGunXOffset flag,
// when the offset is non-positive, or when the weapon slot's type (< 2) doesn't
// support it; negated when AltFireSide >= 0 (alternating left/right mount mirror).
public static class WeaponShotSpread
{
    public static int Run(ShipRec ship, short weaponSlot)
    {
        int spread = Core.Model.GameData.ShipClasses[ship.ShipClass].ShotXOffset;
        if (((WeaponFlags)Core.Model.GameData.Weapons[weaponSlot].Flags & WeaponFlags.IgnoreCarrierGunXOffset) != 0)
            spread = 0;
        if ((short)spread < 1 || ship.WeaponSlotType[weaponSlot] < 2)
            spread = 0;
        else if (-1 < ship.AltFireSide)
            spread = -spread;
        return spread;
    }
}
