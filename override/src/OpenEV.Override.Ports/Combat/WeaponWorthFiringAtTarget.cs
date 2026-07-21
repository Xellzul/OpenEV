using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_100613c0 (EV Override-11.c lines 40703-40729) — AI weapon-pick gate: is this
// weapon worth firing at a target this maneuverable? A weapon flagged DontFireAtEvasiveTargets
// skips targets with EffectiveManeuver >= 4 outright; otherwise fire unless the weapon's turn
// rate is too slow (< 3) to track a target this agile (maneuver > 3).
public static class WeaponWorthFiringAtTarget
{
    public static bool Run(short weaponSlot, ShipRec target)
    {
        short maneuver = (short)ShipDerivedStats.EffectiveManeuver(target);
        if (((WeaponFlags)Core.Model.GameData.Weapons[weaponSlot].Flags & WeaponFlags.DontFireAtEvasiveTargets) == 0
            || maneuver < 4)
        {
            short turnRate = (short)WeaponTurnRate.Run(weaponSlot);
            return !(turnRate < 3 && 3 < maneuver);
        }
        return false;
    }
}
