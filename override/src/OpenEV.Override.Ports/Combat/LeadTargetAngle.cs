using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_1005b208 (EV Override-11.c lines 37585-37611) — aim angle from shooter to target.
// For ballistic weapon kinds (unguided projectiles and the unguided/quadrant turrets) the
// aim point is led by the relative velocity × (distance / weapon speed); beams, homing
// shots, bombs and rockets just fire at the raw bearing.
public static class LeadTargetAngle
{
    public static int Run(ShipRec shooter, ShipRec target, short weaponIndex)
    {
        int aimAngle = EvMath.HeadingBetween(shooter.PosX, shooter.PosY, target.PosX, target.PosY);
        if (weaponIndex == -1)
            return aimAngle;

        var kind = (WeaponGuidanceType)Core.Model.GameData.Weapons[weaponIndex].GuidanceType;
        if (kind is not (WeaponGuidanceType.UnguidedProjectile or WeaponGuidanceType.TurretedUnguided
            or WeaponGuidanceType.FrontQuadrantTurret or WeaponGuidanceType.RearQuadrantTurret))
            return aimAngle;

        float weaponSpeed = Core.Model.GameData.Weapons[weaponIndex].ProjectileSpeed;
        double distance = MacToolbox.sqrt(
            (double)((target.PosX - shooter.PosX) * (target.PosX - shooter.PosX) +
                     (target.PosY - shooter.PosY) * (target.PosY - shooter.PosY)));
        float leadFactor = (float)distance / weaponSpeed;
        return EvMath.HeadingBetween(shooter.PosX, shooter.PosY,
            leadFactor * (target.VelX - shooter.VelX) + target.PosX,
            leadFactor * (target.VelY - shooter.VelY) + target.PosY);
    }
}
