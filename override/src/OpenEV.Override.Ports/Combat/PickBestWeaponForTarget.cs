using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10005c38 (EV Override-11.c 3549-3632) — AI auto-selection of the best turreted weapon for the
// locked target (companion to PickForwardWeaponForTarget, which handles forward-firing weapons).
// Scans the ship's 64 weapon slots for turreted / quadrant weapons (guidance 3/4/7/8) that can
// currently bear on and fire at the target, keeping the highest-priority weapon for each of the two
// priority slots. Quadrant turrets (7/8) only qualify when the target lies within 45° of their
// firing arc. With shields up the energy-priority pick wins; once shields are down the mass-priority
// pick does.
public static class PickBestWeaponForTarget
{
    public static void Run(ShipRec ship)
    {
        // best[0..1] = highest priority seen for the two priority slots (mass at +2, energy at +4);
        // best[2..3] = the weapon slot that achieved each. (decompile sized local_38[8]; only [0..3] used)
        var best = new short[4];
        best[3] = -1;
        best[2] = -1;
        best[1] = 0;
        best[0] = 0;

        if (ship.TargetSlot == -1 || Core.Model.GameData.Ships[ship.TargetSlot].IsActive == 0)
            return;

        var target = ShipTable.Ships[ship.TargetSlot];
        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (ship.WeaponSlotType[slot] <= 0)
                continue;
            var guidance = (WeaponGuidanceType)Core.Model.GameData.Weapons[slot].GuidanceType;
            if (guidance is not (WeaponGuidanceType.TurretedBeam or WeaponGuidanceType.TurretedUnguided
                or WeaponGuidanceType.FrontQuadrantTurret or WeaponGuidanceType.RearQuadrantTurret))
                continue;

            int bearing = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            bool isCandidate;
            if (guidance is WeaponGuidanceType.FrontQuadrantTurret or WeaponGuidanceType.RearQuadrantTurret)
            {
                // Front quadrant aims off the ship's heading; rear quadrant off the opposite heading.
                int referenceHeading = guidance == WeaponGuidanceType.FrontQuadrantTurret
                    ? ship.Heading
                    : (ship.Heading + 180) % 360;
                uint angleDiff = (uint)((short)bearing - referenceHeading);
                uint signMask = (uint)((int)angleDiff >> 0x1f);
                int relAngle = (int)(((signMask ^ angleDiff) - signMask) % 360);   // |bearing - reference| % 360
                isCandidate = (short)relAngle < 46;
            }
            else
            {
                isCandidate = true;
            }

            // ORIGINAL bug (decompile 3599): the third arg is the BEARING, not a weapon index, so
            // IsTurretBlindToTarget indexes the weapon table with it (out-of-table read in the
            // original; the typed port clamps an out-of-range index to no flags).
            if (IsTurretBlindToTarget.Run(ship, target, (short)bearing))
                isCandidate = false;

            if (!ShipDerivedStats.CanFireWeapon(ship, slot) || !isCandidate)
                continue;

            for (short prio = 0; prio < 2; prio++)
            {
                short priority = Core.Model.GameData.Weapons[slot].ShortAt(prio * 2 + 2);   // +2 mass / +4 energy
                if (priority < 1)
                    priority = 1;
                if (best[prio] < priority)
                {
                    best[prio] = priority;
                    best[prio + 2] = slot;
                }
            }
        }

        if (-1 < (int)target.Shield)   // decompile reads +0x68 as int; shields intact → prefer the energy pick
            best[2] = best[3];
        if (best[2] != -1)
        {
            ship.SelectedWeaponSlot = best[2];
            ship.HasSelectedWeapon = 1;
        }
    }
}
