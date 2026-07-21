using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_100608a4 (EV Override-11.c lines 40374-40397). Most callers add the result to
// proj.Heading; WeaponWorthFiringAtShield instead compares it against the maneuver threshold.
public static class WeaponTurnRate
{
    public static int Run(short weaponIndex)
    {
        var weapon = Core.Model.GameData.Weapons[weaponIndex];
        if ((WeaponGuidanceType)weapon.GuidanceType != WeaponGuidanceType.HomingWeapon)
            return 0;

        var seeker = (WeapSeekerFlags)weapon.SeekerFlags;
        int turnRate = 1;
        if ((seeker & WeapSeekerFlags.TurnSpeed30) != 0) turnRate = 3;
        if ((seeker & WeapSeekerFlags.TurnSpeed60) != 0) turnRate += 4;
        return turnRate;
    }
}
