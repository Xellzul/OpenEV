using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Ship;

// Port of FUN_100594c8 (EV Override-11.c 36726-36764) — the slot of the nearest
// active, non-dying NPC in the player's system, or -1. The distance metric keeps
// the original's mixed float/double rounding (dx*dx as double + (float)(dy*dy)
// widened back to double).
public static class FindNearestActiveShip
{
    public static int Run()
    {
        var player = ShipTable.Player;
        int best = -1;
        float bestDist = ShipStatConstants.NearestSearchMaxDist;

        for (int i = 1; i < ShipTable.Count; i++)
        {
            var ship = ShipTable.Ships[i];
            if (ship.IsActive == 0 || ShipDerivedStats.IsDyingOrDestroyed(ship))
            {
                continue;
            }
            if (player.CurrentSystem != ship.CurrentSystem)
            {
                continue;
            }
            double dx = EvMath.FloatAbs((double)(player.PosX - ship.PosX));
            double dy = EvMath.FloatAbs((double)(player.PosY - ship.PosY));
            float distSq = (float)(dx * dx + (double)(float)(dy * dy));
            // The second clause tests the running best (the -1 "none found"
            // sentinel), not the candidate — so the first valid ship is accepted.
            // Faithful to the decompile; do not rewrite it to compare distSq.
            if (distSq < bestDist || bestDist < ShipStatConstants.NearestSearchEpsilon)
            {
                best = i;
                bestDist = distSq;
            }
        }
        return best;
    }
}
