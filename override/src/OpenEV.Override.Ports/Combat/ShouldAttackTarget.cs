namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

// FUN_100096b4 — EV Override-11.c lines 5103-5203. AI "is my target worth attacking?":
// the target must be live and in-system; the player (slot 0) or a fleeing ship (DefendRetreat);
// this ship heavy (class Mass >= 100); and the velocity delta meaningful and heading away from us.
// Then attack if any armed weapon slot can reach (reach² >= 0.8·dist²); with none in reach, only
// if the target's class is at least as fast as ours.
public static class ShouldAttackTarget
{
    public static bool Run(ShipRec ship)
    {
        if (ship.TargetSlot == -1)
            return false;
        var target = ShipTable.Ships[ship.TargetSlot];
        if (ship.CurrentSystem != target.CurrentSystem || target.IsActive == 0)
            return false;
        if (target.SlotIndex != 0 && target.AiState != ShipAiState.DefendRetreat)
            return false;
        if (Core.Model.GameData.ShipClasses[ship.ShipClass].Mass < 100)
            return false;

        float dVelX = target.VelX - ship.VelX;
        float dVelY = target.VelY - ship.VelY;
        double velSettleThreshold = ShipStatConstants.AiVelSettleThreshold; // 0.35
        if (EvMath.FloatAbs(dVelX) <= velSettleThreshold && EvMath.FloatAbs(dVelY) <= velSettleThreshold)
            return false;

        // Velocity-delta heading vs the target→ship bearing; within 90° the target is
        // closing on us, so don't chase.
        float velOrigin = 0f; // flt_81B88
        short deltaHeading = (short)EvMath.HeadingBetween(velOrigin, velOrigin, dVelX, dVelY);
        short bearing = (short)EvMath.HeadingBetween(target.PosX, target.PosY, ship.PosX, ship.PosY);
        int headingDelta = deltaHeading - bearing;
        int sign = headingDelta >> 31;
        int absHeadingDelta = (headingDelta ^ sign) - sign; // branchless abs
        if (absHeadingDelta < 90)
            return false;

        double distSq = EvMath.FloatAbs(EvMath.DistanceSquared(ship, target));
        double proximityScale = ShipStatConstants.AiProximityScale;   // 0.8
        double speedScale = ShipStatConstants.AiStrafeAccelScale; // 1.5

        short reachingSlot = -1;
        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (ship.WeaponSlotType[slot] < 1)
                continue;
            if (ship.WeaponSlotAmmo[slot] < 1 && ship.WeaponSlotAmmo[slot] != -1)
                continue;
            if (Core.Model.GameData.Weapons[slot].GuidanceType != 1)
                continue;
            if (!WeaponWorthFiringAtTarget.Run(slot, target))
                continue;
            if (!ShipDerivedStats.CanFireWeapon(ship, slot))
                continue;
            // 1.5 × projectile speed × lifetime; the inner product stays single precision (fmuls)
            // before the double scale, then frsp rounds the result back to single.
            float reach = (float)(speedScale * (Core.Model.GameData.Weapons[slot].ProjectileSpeed *
                                                (float)Core.Model.GameData.Weapons[slot].Lifetime));
            if (reach * reach < proximityScale * distSq)
                continue;
            reachingSlot = slot;
            break;
        }

        float shipSpeed = Core.Model.GameData.ShipClasses[ship.ShipClass].Speed;
        float targetSpeed = Core.Model.GameData.ShipClasses[target.ShipClass].Speed;
        if (reachingSlot == -1)
            return shipSpeed <= targetSpeed;
        return shipSpeed < targetSpeed;
    }
}
