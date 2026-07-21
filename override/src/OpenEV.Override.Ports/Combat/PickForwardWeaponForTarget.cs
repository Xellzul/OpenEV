using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10006194 (EV Override-11.c 3682-3769) — AI auto-selection of the best FORWARD-firing weapon
// for the locked target (companion to PickBestWeaponForTarget, which handles turrets). Scans the
// ship's 64 weapon slots for loaded, ready, forward weapons (guidance -1/0/6) and keeps the
// highest-priority weapon for each of the two priority slots; free-flight rockets with a minimum
// range only count when the target is far enough away on both axes. With shields up the
// energy-priority pick wins, else the mass-priority pick.
public static class PickForwardWeaponForTarget
{
    public static void Run(ShipRec ship)
    {
        // best[0..1] = highest priority seen per priority slot (mass at +2, energy at +4);
        // best[2..3] = the weapon slot that achieved each. (decompile sized local_38[6]; [4]/[5] were
        // the int→double CONCAT staging overlapping the array — a decompile stack artifact, only [0..3] real)
        var best = new short[4];
        best[3] = -1;
        best[2] = -1;
        best[1] = 0;
        best[0] = 0;

        short targetSlot = ship.TargetSlot;
        if (targetSlot == -1)
            return;
        var target = ShipTable.Ships[targetSlot];
        if (ship.CurrentSystem != target.CurrentSystem || target.IsActive == 0)
            return;

        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (!ForwardWeaponUsable(ship, slot))
                continue;
            var weapon = Core.Model.GameData.Weapons[slot];
            for (short prio = 0; prio < 2; prio++)
            {
                short priority = weapon.ShortAt(prio * 2 + 2);   // +2 mass / +4 energy
                if (priority < 1)
                    priority = 1;
                if ((WeaponGuidanceType)weapon.GuidanceType == WeaponGuidanceType.FreeflightRocket && weapon.ShotOffset > 0)
                {
                    // Rockets with a minimum range (ShotOffset): only pick when the target is at least
                    // AiRangeScaleA × that range away on BOTH axes. (The decompile also computes the
                    // straight-line distance into a dead local here — pure and unused, so dropped.)
                    double dist = EvMath.FloatAbs(ship.PosX - target.PosX);
                    if (ShipStatConstants.AiRangeScaleA * weapon.ShotOffset <= dist)
                    {
                        dist = EvMath.FloatAbs(ship.PosY - target.PosY);
                        if (ShipStatConstants.AiRangeScaleA * weapon.ShotOffset <= dist && best[prio] < priority)
                        {
                            best[prio] = priority;
                            best[prio + 2] = slot;
                        }
                    }
                }
                else if (best[prio] < priority)
                {
                    best[prio] = priority;
                    best[prio + 2] = slot;
                }
            }
        }

        if (-1 < (int)target.Shield)   // +0x68 read as int (was SingleToInt32Bits; field holds integer shield values)
            best[2] = best[3];
        if (-1 < best[2])
            ship.SelectedWeaponSlot = best[2];
        if (ship.SelectedWeaponSlot != -1)
            ship.HasSelectedWeapon = 1;
    }

    // Decompile 3706-3719 — slot holds a loaded, forward-guidance weapon that is ready to fire right
    // now. Early returns keep the original's short-circuit order: CanFireWeapon is only called once
    // the cheaper slot/ammo/guidance gates pass.
    private static bool ForwardWeaponUsable(ShipRec ship, short slot)
    {
        if (ship.WeaponSlotType[slot] <= 0)
            return false;
        var weapon = Core.Model.GameData.Weapons[slot];
        short ammoCount = weapon.AmmoLink;
        // Has rounds in the slot, OR is a fuel-burning weapon (cost encoded as -(cost+1000) in
        // AmmoLink) whose cost the current fuel covers, OR uses unlimited ammo (-1).
        bool hasUsableAmmo = (ship.WeaponSlotAmmo[slot] > 0 && ammoCount > -1000)
            || ((float)(System.Math.Abs((int)ammoCount) - 1000) <= ship.Fuel && ammoCount < -999)
            || ammoCount == -1;
        if (!hasUsableAmmo)
            return false;
        var guidance = (WeaponGuidanceType)weapon.GuidanceType;
        if (guidance is not (WeaponGuidanceType.UnguidedProjectile or WeaponGuidanceType.BeamWeapon
            or WeaponGuidanceType.FreeflightRocket))
            return false;
        return ShipDerivedStats.CanFireWeapon(ship, slot) && ship.WeaponSlotReload[slot] <= 0.0;
    }
}
