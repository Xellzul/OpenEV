using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_10066904 — the per-system NPC traffic spawner. For each of the 8 active
// mission slots it tops up the standing aux-ship patrol and the mission ships;
// then it rolls random fleet arrivals, spaceport tribute defenders,
// reinforcements, and a periodic respawn fleet.
// Decompile: EV Override-11.c lines 42670-42992.
//
// The decompile reaches the spawn-physics constants through the TOC (rendered as
// `(float)ppuVar3[-N]`); the real loads are plain floats and the i2d-bias idiom
// collapses to (float)/(int) casts. Spawn physics shared with SpawnFleet.cs.
public static class SpawnFleetShips
{
    public static void Run(int systemIndex)
    {
        short sys = (short)systemIndex;
        var player = GameData.Ships[0];

        // The original reuses one variable (decompile sVar8 / ASM r31) as both the mission-ship spawn
        // slot and the present-fleet counter in TryRandomFleetArrival, and never re-zeroes it between
        // the two. So the random-fleet count is seeded with the last mission-ship slot spawned this
        // frame (or 0 when no mission spawned) — TopUpMissionShips returns it; carried here verbatim.
        short presentFleetSeed = 0;
        for (short missionIndex = 0; missionIndex < GameData.Missions.Length; missionIndex++)
        {
            if (GameData.MissionStates[missionIndex].IsActive == 0) continue;
            var mission = GameData.Missions[missionIndex];
            TopUpStandingPatrol(missionIndex, mission, sys, player);
            presentFleetSeed = TopUpMissionShips(missionIndex, mission, sys, player);
            if (mission.SpawnCountdown > 0) mission.SpawnCountdown -= 1;
        }

        TryRandomFleetArrival(systemIndex, sys, presentFleetSeed);
        TrySpawnTributeDefenders(sys);
        TrySpawnRespawnFleet(player);
    }

    // FUN_10066904 (sub_66904 loc_66944-loc_66D14) — top up the mission's standing aux-ship
    // patrol toward RemainingSpawnCount when the spawn timer is ready and the mission matches
    // the player's system.
    private static void TopUpStandingPatrol(short missionIndex, MissionRecord mission,
                                            short sys, ShipRecord player)
    {
        if (mission.SpawnCountdown >= 1 || mission.SpawnDudeId == -1
            || mission.LiveSpawnCount >= mission.RemainingSpawnCount)
        {
            return;
        }
        short need = (short)(mission.RemainingSpawnCount - mission.LiveSpawnCount);
        if (!MissionSystMatches.Run(player.CurrentSystem, missionIndex) || need <= 0) return;

        OffsetSpawnBase((int)SeedEvoRng.Run(360), out float baseX, out float baseY);
        for (short k = 0; k < need; k++)
        {
            short slot = (short)SpawnDudeShip.Run(mission.SpawnDudeId, sys);
            if (slot == -1) continue;

            var ship = GameData.Ships[slot];
            ship.SpawningMissionSlot = missionIndex;
            mission.LiveSpawnCount += 1;
            if ((mission.Flags & MisnFlags.AuxShipsReplacedWhenDestroyed) == 0)
            {
                mission.RemainingSpawnCount -= 1;
            }
            PlaceSpawnedShip(ship, baseX, baseY);
        }
        if ((mission.Flags & MisnFlags.AutoAborting) != 0) ApplyMissionCompletionBits.Run(missionIndex);
    }

    // FUN_10066904 (sub_66904 loc_66D14-loc_67268) — top up the mission's special ships
    // (board/scan targets) up to SpawnCount, aiming the formation at the player's last system.
    // Returns the last mission-ship slot spawned this frame (or 0 if none); the original leaks this
    // value into the random-fleet count via a reused variable (see the seed comment in Run).
    private static short TopUpMissionShips(short missionIndex, MissionRecord mission,
                                           short sys, ShipRecord player)
    {
        short lastSpawnedSlot = 0;   // reused variable (decompile sVar8 / ASM r31), zeroed at this phase entry
        short count = 0;
        if (mission.DestSystem == -6 || player.CurrentSystem == mission.DestSystem)
        {
            count = mission.SpawnCount;
        }
        if (mission.ShipBehavior > 8)
        {
            count = (short)(mission.SpawnCount - mission.MissionShipsSpawnedCount);
            if (mission.DestSystem != -6 && player.CurrentSystem != mission.DestSystem && mission.MissionShipsSpawnedCount < 1)
            {
                count = 0;
            }
        }
        if (count <= 0) return lastSpawnedSlot;

        if (mission.MissionShipSpawnCountdown > 0) mission.MissionShipSpawnCountdown -= 1;
        if (mission.SpawnCount <= 0 || mission.MissionShipSpawnCountdown != 0) return lastSpawnedSlot;
        mission.MissionShipSpawnCountdown = -1;

        // Aim the formation at the player's last system (or a random heading if there is none).
        int angle;
        if (player.PriorSystem == -1)
        {
            angle = (int)SeedEvoRng.Run(360);
        }
        else
        {
            var here = SystTable.Store[player.CurrentSystem];
            var prev = SystTable.Store[player.PriorSystem];
            angle = EvMath.HeadingBetween(here.XPos, here.YPos, prev.XPos, prev.YPos);
        }

        OffsetSpawnBase(angle, out float baseX, out float baseY);
        for (short k = 0; k < count; k++)
        {
            short slot = (short)SpawnMissionNpc.Run(mission.ShipToBoardOrScan, sys, missionIndex);
            lastSpawnedSlot = slot;   // set even when -1, matching the ASM ordering (r31 = result before the -1 test)
            if (slot == -1) continue;

            var ship = GameData.Ships[slot];
            mission.MissionShipsSpawnedCount += 1;
            PlaceSpawnedShip(ship, baseX, baseY);
        }
        if ((mission.Flags & MisnFlags.AutoAborting) != 0) ApplyMissionCompletionBits.Run(missionIndex);
        return lastSpawnedSlot;
    }

    // FUN_10066904 (sub_66904 loc_672B8-loc_67478) — roll a random fleet arrival: if the system
    // holds fewer fleet ships than its cap, weight-pick one of its 4 fleets and spawn it.
    private static void TryRandomFleetArrival(int systemIndex, short sys, short presentFleetSeed)
    {
        // syst FleetSpawn layout: [0..3] = fleet ids, [4..7] = spawn weights, [8] = average ship cap.
        const int FleetSlotCount = 4;

        short presentFleet = presentFleetSeed;   // see Run: the original leaks the last mission-ship slot in here
        for (short i = 1; i < ShipTable.Count; i++)
        {
            var s = GameData.Ships[i];
            if (s.IsActive != 0 && sys == s.CurrentSystem && s.OwnerSlot != 0) presentFleet++;
        }
        if (presentFleet >= SystTable.Store[sys].FleetSpawn[8] || SeedEvoRng.Run(500) != 0)
        {
            return;
        }

        short roll = (short)(SeedEvoRng.Run(100) + 1);
        short[] cumulative = new short[FleetSlotCount];
        for (int i = 0; i < FleetSlotCount; i++)
        {
            cumulative[i] = 0;
            for (int j = 0; j <= i; j++)
            {
                cumulative[i] = (short)(cumulative[i] + SystTable.Store[sys].FleetSpawn[j + 4]);
            }
        }
        // The chosen fleet is held as a float sentinel (-1 = none); only its not-none state is
        // consumed (RollNpcArrival re-rolls the actual fleet from the system).
        float chosenFleet = ShipStatConstants.NoFleetSentinel;
        for (int i = FleetSlotCount - 1; i >= 0; i--)
        {
            if (roll <= cumulative[i] && SystTable.Store[sys].FleetSpawn[i] >= 0)
            {
                chosenFleet = i;
            }
        }
        if (ShipStatConstants.NoFleetSentinel != chosenFleet) RollNpcArrival.Run(systemIndex);
    }

    // FUN_10066904 (sub_66904 loc_67480-loc_6766C) — for each inhabited spaceport in the system
    // that still owes tribute, spawn one government defender (once per call) and decrement its tribute.
    private static void TrySpawnTributeDefenders(short sys)
    {
        for (int idx = 0; idx < SystRecord.StellarLinkCount; idx++)
        {
            short spobLink = SystTable.SpobLink(sys, idx);
            if (spobLink == -1 || !ShipDerivedStats.AnyShipDefendingSpob(spobLink)
                || GameData.Spobs[spobLink].Tribute <= 0)
            {
                continue;
            }

            short defenders = 0;
            for (short i = 1; i < ShipTable.Count; i++)
            {
                var s = GameData.Ships[i];
                if (s.IsActive != 0 && s.DefendedSpobIndex != -1 && s.DefendedSpobIndex == spobLink) defenders++;
            }
            if (defenders < GameData.Spobs[spobLink].TributeMax % 10)
            {
                SpawnGovtDefender.Run(spobLink);
                GameData.Spobs[spobLink].Tribute = (short)(GameData.Spobs[spobLink].Tribute - 1);
                break;
            }
        }
    }

    // FUN_10066904 (sub_66904 loc_6766C-loc_677D8) — top-level reinforcement spawn, then the
    // periodic respawn fleet: when the respawn counter is due, size the fleet by the player's
    // escort cargo mass.
    private static void TrySpawnRespawnFleet(ShipRecord player)
    {
        if (WorldState.SharewareRegisteredMatch == 0) SpawnReinforcement.Run();

        if (WorldState.RespawnCounter != 0)
        {
            WorldState.RespawnCounter -= 1;
            return;
        }
        WorldState.RespawnCounter = -1;

        short escortMass = 0;
        for (short i = 1; i < ShipTable.Count; i++)
        {
            var s = GameData.Ships[i];
            if (s.IsActive != 0 && s.OwnerSlot == 0 && s.AiBehaviorType == ShipAiType.Escort && s.GrudgeMissionIndex == -1
                && GameData.ShipClasses[s.ShipClass].InherentAI < ShipAiType.Warship)
            {
                escortMass = (short)(escortMass + GameData.ShipClasses[s.ShipClass].Holds);
            }
        }
        if (escortMass >= 200) SpawnFleet.Run(player.CurrentSystem, 127);
        else if (escortMass > 49) SpawnFleet.Run(player.CurrentSystem, 126);
    }

    // FUN_10066904 (sub_66904 loc_66A24-loc_66A6C) — seed a formation base at the origin and offset
    // it by `angle` at a spread-summed distance. The spread sum reads only constants, so its order
    // relative to the caller's angle RNG does not affect the result.
    private static void OffsetSpawnBase(int angle, out float x, out float y)
    {
        float zero = ShipStatConstants.SpawnZeroDefault;
        float spreadStep = ShipStatConstants.SpawnSpreadStep;
        float spreadAccum = zero;
        for (float spread = ShipStatConstants.SpawnSpreadStart; zero < spread; spread -= spreadStep)
        {
            spreadAccum += spread;
        }
        x = zero;
        y = zero;
        EvMath.OffsetByHeading(ShipStatConstants.SpawnBaseOffset + spreadAccum, angle, ref x, ref y);
    }

    // FUN_10066904 (sub_66904 loc_66B0C / loc_67114) — scatter a freshly spawned NPC around the
    // formation base and launch it along its heading.
    private static void PlaceSpawnedShip(ShipRecord ship, float baseX, float baseY)
    {
        float scatterBase = ShipStatConstants.FleetScatterBase;
        float zero = ShipStatConstants.SpawnZeroDefault;
        ship.PosX = scatterBase + (baseX + (short)SeedEvoRng.Run(512));
        ship.PosY = scatterBase + (baseY + (short)SeedEvoRng.Run(512));
        ship.Heading = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, zero, zero);
        ship.DockedSpobIndex = -2;
        ship.PriorSystem = -2;
        ship.VelY = zero;
        ship.VelX = zero;
        ship.JumpWindupTimer = -999;
        ship.AiTickStamp = (int)MacToolbox.TickCount();   // raw-int stamp (read back as raw int, not float bits)
        // The original recomputes a spread-sum here but discards it: the launch impulse uses the raw
        // SpawnSpreadStart, not the summed distance. Omitted as dead, side-effect-free arithmetic.
        EvMath.OffsetByHeading(ShipStatConstants.SpawnSpreadStart, ship.Heading, ref ship.VelX, ref ship.VelY);
    }
}
