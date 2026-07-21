using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006b564 (EV Override-11.c 44180-44200) — propagate a fleeing leader's hyper-jump to its
// escorts: for each active escort owned by this leader that isn't defending a planet, copy the
// leader's jump-windup timer and AI tick stamp, set TargetSlot to -2, and start it leaving hyperspace.
public static class PropagateFleeToEscorts
{
    public static void Run(ShipRec leader)
    {
        short leaderSlot = leader.SlotIndex;
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            var escort = ShipTable.Ships[shipIndex];
            if (escort.IsActive == 0 || escort.OwnerSlot != leaderSlot || escort.DefendedSpobIndex != -1)
                continue;
            var leaderShip = ShipTable.Ships[escort.OwnerSlot];   // == the leader (OwnerSlot == leaderSlot here)
            escort.JumpWindupTimer = leaderShip.JumpWindupTimer;
            escort.AiTickStamp = leaderShip.AiTickStamp;
            escort.TargetSlot = -2;
            ShipAi.SetStateLeavingHyper(escort);
        }
    }
}
