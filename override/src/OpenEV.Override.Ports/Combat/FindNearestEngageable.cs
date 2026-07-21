namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

// Port of FUN_1005961c (EV Override-11.c 36770-36801) — the slot of the engageable NPC
// nearest the player in the same system, or -1; skips the player slot, inactive slots, and
// ships already in their death throes. The distance metric keeps the original's mixed
// float/double rounding (dx*dx as double + (float)(dy*dy) widened back to double).
public static class FindNearestEngageable
{
    public static int Run()
    {
        int nearestIndex = -1;
        float nearestDistSquared = ShipStatConstants.NearestSearchMaxDist;

        for (int loopIndex = 0; (short)loopIndex < ShipTable.Count; loopIndex++)
        {
            short slotIndex = (short)loopIndex;
            var ship = ShipTable.Ships[slotIndex];
            if (slotIndex != 0 && ship.IsActive != 0 && !ShipDerivedStats.IsDyingOrDestroyed(ship) &&
                Core.Model.GameData.Player.CurrentSystem == ship.CurrentSystem && ShipAi.IsEngageableTarget(ship))
            {
                double deltaX = EvMath.FloatAbs((double)(ShipTable.PosX - ship.PosX));
                double deltaY = EvMath.FloatAbs((double)(ShipTable.PosY - ship.PosY));
                float distSquared = (float)(deltaX * deltaX + (double)(float)(deltaY * deltaY));
                // The second clause tests the running best (the -1 "none found"
                // sentinel), not the candidate — so the first valid ship is accepted.
                // Faithful to the decompile; do not rewrite it to compare distSquared.
                if (distSquared < nearestDistSquared || nearestDistSquared < ShipStatConstants.NearestSearchEpsilon)
                {
                    nearestIndex = loopIndex;
                    nearestDistSquared = distSquared;
                }
            }
        }
        return nearestIndex;
    }
}
