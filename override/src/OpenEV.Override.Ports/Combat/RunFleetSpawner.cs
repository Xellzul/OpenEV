using System;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10064b58 (EV Override-11.c 42080-42280) — the per-system fleet/NPC spawner, run on
// system entry/refresh. Three passes:
//   1. Recycle the player's own ships (slot 0 = player; NPC escorts 1..35): respawn the
//      undisabled, un-grudged ones beside the player; mark disabled ones dead, handing a
//      low-AI escort hauler's cargo to the fleet first.
//   2. For each active mission slot targeting this system, spawn the mission's ships,
//      scattering them per the goal type and pre-damaging derelicts.
//   3. Fill ambient traffic up to the system's average-ship count (pers / random eligible
//      fleet / weighted fleet or arrival NPC), then the system's forced-pers spawns.
public static class RunFleetSpawner
{
    public static void Run(int systemId)
    {
        var systemSlot = (short)systemId;

        CleanupSystNpcs.Run(0);

        // ---- Pass 1: recycle the player's own ships ----
        for (short slot = 1; slot < ShipTable.Count; slot++)
        {
            var escort = ShipTable.Ships[slot];
            if (escort.OwnerSlot != 0 || escort.IsActive == 0)
                continue;

            if (ShipDerivedStats.IsDisabled(escort))
            {
                escort.IsActive = 0;
                // A grudge-free escort-AI hauler (behaviour 6) hands its cargo to the fleet first.
                if (escort.OwnerSlot == 0 && escort.AiBehaviorType == ShipAiType.Escort && escort.GrudgeMissionIndex == -1
                    && GameData.ShipClasses[escort.ShipClass].InherentAI < ShipAiType.Warship)
                    RedistributeCargoAmongShips.Run(slot);
            }
            else if (escort.GrudgeMissionIndex == -1)
            {
                // Re-test — ship state is untouched here, so it matches the test above (this else
                // is effectively dead, preserved from the decompile's two FUN_10059c58 calls).
                if (ShipDerivedStats.IsDisabled(escort))
                {
                    escort.IsActive = 0;
                    escort.OwnerSlot = -1;
                }
                else
                {
                    RespawnEscortAdjacentToPlayer.Run(escort);
                }
            }
        }

        // ---- Pass 2: spawn ships for each active mission slot targeting this system ----
        for (short mission = 0; mission < MissionStateTable.Count; mission++)
        {
            var missionRec = GameData.Missions[mission];
            var targetsSystem = systemSlot == missionRec.DestSystem || missionRec.DestSystem == -6;
            if (GameData.MissionStates[mission].IsActive == 0 || !targetsSystem
                || missionRec.SpawnCount <= 0 || missionRec.MissionShipSpawnCountdown >= 0)
                continue;

            short toSpawn = missionRec.SpawnCount;
            // "Any system" missions (-6) of ShipBehavior digit 1 discount ships already alive for them.
            if (missionRec.DestSystem == -6 && missionRec.ShipBehavior % 10 == 1)
                for (short other = 1; other < ShipTable.Count; other++)
                    if (GameData.Ships[other].IsActive != 0 && mission == GameData.Ships[other].GrudgeMissionIndex)
                        toSpawn--;

            if (toSpawn <= 0)
                continue;

            for (short n = 0; n < toSpawn; n++)
            {
                var spawned = (short)SpawnMissionNpc.Run(missionRec.ShipToBoardOrScan, systemSlot, mission);
                if (spawned == -1)
                    continue;
                var ship = GameData.Ships[spawned];

                if (missionRec.MissionGoalType == MissionGoalKind.Escort)   // scatter within ±256 of the origin
                {
                    ship.PosX = (float)((short)SeedEvoRng.Run(512) - 256);
                    ship.PosY = (float)((short)SeedEvoRng.Run(512) - 256);
                }
                if (missionRec.MissionGoalType == MissionGoalKind.RescueDisabled)   // derelict: random facing, no velocity, armor damage
                {
                    ship.Heading = (short)SeedEvoRng.Run(360);
                    ship.VelY = ShipStatConstants.SpawnZeroDefault;
                    ship.VelX = ShipStatConstants.SpawnZeroDefault;
                    var cls = GameData.ShipClasses[ship.ShipClass];
                    // Shield (+0x68) holds an int here: a negative scale × BaseArmor leaves the derelict
                    // pre-damaged (-0.6 normally, -0.85 for the tough DisabledAt10PctArmor class flag).
                    double armorScale = (cls.Flags & ShipFlags.DisabledAt10PctArmor) == 0
                        ? ShipStatConstants.SpawnArmorScale
                        : ShipStatConstants.SpawnArmorScaleTough;
                    ship.Shield = (int)(armorScale * cls.BaseArmor);
                    if (missionRec.TimeLimit < 1 && -32000 < missionRec.TimeLimit)
                        ship.SalvageClaimed = 1;
                }
            }

            if ((missionRec.Flags & MisnFlags.AutoAborting) != 0)
                ApplyMissionCompletionBits.Run(mission);
        }

        // ---- Pass 3: ambient traffic up to the average-ship count, then forced pers ----
        var system = SystTable.Store[systemSlot];
        for (short ambient = 0; ambient < system.FleetSpawn[8]; ambient++)   // FleetSpawn[8] = average ships
        {
            short spawned = -1;
            if (SeedEvoRng.Run(7) == 0)
            {
                spawned = (short)SpawnPers.Run(GameData.Player.CurrentSystem, 0, -1);
            }
            else if (SeedEvoRng.Run(7) == 0)
            {
                SpawnRandomEligibleFleet.Run(GameData.Player.CurrentSystem);   // spawns its own ships; nothing to launch here
            }
            else
            {
                var roll = (short)SeedEvoRng.Run(100);
                // Cumulative spawn weights of the 4 fleet-type slots (FleetSpawn[4..7]).
                var weights = new short[4];
                weights[0] = system.FleetSpawn[4];
                for (short tier = 1; tier < weights.Length; tier++)
                    weights[tier] = (short)(weights[tier - 1] + system.FleetSpawn[4 + tier]);
                for (short tier = 0; tier < weights.Length; tier++)
                {
                    if (roll + 1 <= weights[tier])
                    {
                        if (system.FleetSpawn[tier] < 0)
                            // fleet id = abs(slot + 128); ASM uses the branchless srawi/xor/subf idiom.
                            SpawnFleet.Run(systemId, (short)Math.Abs(system.FleetSpawn[tier] + 128));
                        // Preserved quirk: this gate reads the PLAYER's current system, not systemSlot
                        // (matches ASM lha 0x34(player) at loc_652EC).
                        else if (-1 < SystTable.Store[GameData.Player.CurrentSystem].FleetSpawn[tier])
                            spawned = (short)SpawnSystArrivalNpc.Run(systemSlot, 2);
                        break;
                    }
                }
            }
            if (spawned != -1)
            {
                var cls = GameData.ShipClasses[GameData.Ships[spawned].ShipClass];
                EvMath.AccelerateAlongHeading(cls.Speed, cls.Speed, GameData.Ships[spawned].Heading, ShipTable.Ships[spawned]);
            }
        }

        // Forced pers — the system always tries to spawn each pers it lists.
        for (short i = 0; i < system.ForcedPers.Length; i++)
        {
            short pers = system.ForcedPers[i];
            if (pers == -1 || GameData.Pers[pers].AvailableFlag == 0)
                continue;
            short spawned = (short)SpawnPers.Run(GameData.Player.CurrentSystem, 0, pers);
            if (spawned == -1)
                continue;
            var cls = GameData.ShipClasses[GameData.Ships[spawned].ShipClass];
            EvMath.AccelerateAlongHeading(cls.Speed, cls.Speed, GameData.Ships[spawned].Heading, ShipTable.Ships[spawned]);
        }
    }
}
