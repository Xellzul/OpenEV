using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10005f70 (EV Override-11.c 3633-3681) — AI picks the first armed, in-range HOMING weapon
// (guidance 1) worth firing at the locked target, into SelectedWeaponSlot (HasSelectedWeapon = the
// found flag). A slot qualifies when its reach — 1.5 × projectile speed × lifetime — squared covers
// 0.95 × the target distance².
public static class PickHomingWeaponForTarget
{
    public static void Run(ShipRec ship)
    {
        if (ship.TargetSlot == -1)
            return;
        var target = ShipTable.Ships[ship.TargetSlot];
        if (ship.CurrentSystem != target.CurrentSystem
            || target.IsActive == 0
            || ShipAi.ArmorBelowRetreatThreshold(target))
            return;

        double targetDistSq = EvMath.FloatAbs(
            EvMath.DistanceSquared(ship.PosX, ship.PosY, target.PosX, target.PosY));

        short bestSlot = -1;
        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (ship.WeaponSlotType[slot] < 1)
                continue;
            if (ship.WeaponSlotAmmo[slot] < 1 && ship.WeaponSlotAmmo[slot] != -1)
                continue;
            var weapon = Core.Model.GameData.Weapons[slot];
            if ((WeaponGuidanceType)weapon.GuidanceType != WeaponGuidanceType.HomingWeapon)
                continue;
            if (!WeaponWorthFiringAtTarget.Run(slot, target))
                continue;
            if (!ShipDerivedStats.CanFireWeapon(ship, slot))
                continue;
            // Reach = 1.5 × projectile speed × lifetime; keep the slot when reach² covers
            // 0.95 × the target distance².
            float reach = (float)(ShipStatConstants.AiStrafeAccelScale *
                (double)(weapon.ProjectileSpeed * (float)(int)weapon.Lifetime));
            if ((double)(reach * reach) < ShipStatConstants.AiBeamRangeSquaredScale * targetDistSq)
                continue;
            bestSlot = slot;
            break;
        }

        if (bestSlot == -1)
        {
            ship.HasSelectedWeapon = 0;
        }
        else
        {
            ship.SelectedWeaponSlot = bestSlot;
            ship.HasSelectedWeapon = 1;
        }
    }
}
