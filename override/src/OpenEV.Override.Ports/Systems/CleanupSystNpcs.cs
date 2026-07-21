using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_10064728 (EV Override-11.c lines 41999-42079).
public static class CleanupSystNpcs
{
    // Original decompile signature is FUN_10064728(char param_1) — one parameter.
    // Every call site in the source passes 2 args (e.g. FUN_10064728(1,1)) via an
    // unprototyped C call, but the ASM only ever reads the first (param_1, r25);
    // r4 (the would-be second arg) is never consumed — it's genuinely dead in the
    // original, so the port only takes the one real parameter.
    public static void Run(byte forceCleanup)
    {
        for (short shipIdx = 1; shipIdx < ShipTable.Count; shipIdx = (short)(shipIdx + 1))
        {
            var ship = ShipTable.Ships[shipIdx];
            if (!ShouldCleanupShip(ship, forceCleanup)) continue;

            if (ship.IsActive == 0 || ship.SpawningMissionSlot == -1)
            {
                if (ship.IsActive != 0 && ship.DefendedSpobIndex != -1)
                {
                    GameData.Spobs[ship.DefendedSpobIndex].Tribute = (short)(GameData.Spobs[ship.DefendedSpobIndex].Tribute + 1);
                    if (GameData.Spobs[ship.DefendedSpobIndex].TributeMax < GameData.Spobs[ship.DefendedSpobIndex].Tribute)
                    {
                        GameData.Spobs[ship.DefendedSpobIndex].Tribute = GameData.Spobs[ship.DefendedSpobIndex].TributeMax;
                    }
                }
            }
            else
            {
                if (GameData.MissionStates[ship.SpawningMissionSlot].IsActive != 0)
                {
                    GameData.Missions[ship.SpawningMissionSlot].RemainingSpawnCount += 1;
                }
                if (GameData.Missions[ship.SpawningMissionSlot].AuxShipCount < GameData.Missions[ship.SpawningMissionSlot].RemainingSpawnCount)
                {
                    GameData.Missions[ship.SpawningMissionSlot].RemainingSpawnCount = GameData.Missions[ship.SpawningMissionSlot].AuxShipCount;
                }
            }

            ship.IsActive = 0;
            ship.GrudgeMissionIndex = -1;
            ship.DefendedSpobIndex = -1;
            ship.IsTractored = 0;
            ship.IsCarriedFighter = 0;
            ship.HailQuoteSpoken = 0;
            ship.HasAfterburner = (byte)(HasAfterburner.Run(ship) ? 1 : 0);
            ship.SpawningMissionSlot = -1;
            ship.CurrentSystem = -1;
            ship.OwnerSlot = -1;
        }
    }

    // FUN_10064728 42009-42026 — whether this NPC is due for despawn/reset this pass:
    // a non-player-owned, non-guarding, non-mission-linked ship that is either already
    // disabled or forceCleanup was requested.
    private static bool ShouldCleanupShip(ShipRec ship, byte forceCleanup)
    {
        if (ShipAiType.Interceptor < ship.AiBehaviorType && ship.OwnerSlot == 0 && ship.DefendedSpobIndex == -1 &&
            ship.GrudgeMissionIndex == -1)
        {
            return ShipDerivedStats.IsDisabled(ship) || forceCleanup != 0;
        }
        return true;
    }
}
