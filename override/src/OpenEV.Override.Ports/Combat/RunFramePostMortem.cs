using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10068304 (EV Override-11.c 43243-43369): the player-death cargo settlement.
// Sums the player's cargo holds and all collected junk, forfeits the cargo of any
// in-progress mission (failing it), then jettisons the lot as escape pods spawned from
// the player and each low-AI hired escort.
public static class RunFramePostMortem
{
    public static void Run(byte dramaticEndFlag)
    {
        short cargoJunkTotal = 0;

        // Sum the player's cargo holds (one per base commodity).
        for (short i = 0; i < ShipRecord.CargoHoldCount; i++)
            cargoJunkTotal += GameData.Player.CargoHold[i];

        // Sum and clear every collected-junk slot.
        for (short i = 0; i < JunkTable.Count; i++)
        {
            if (GameData.Junk[i].PlayerQty > 0)
            {
                cargoJunkTotal += GameData.Junk[i].PlayerQty;
                GameData.Junk[i].PlayerQty = 0;
            }
        }

        short jettisonTotal = cargoJunkTotal;
        short playerPodBasis = cargoJunkTotal;   // separate register, but always equal to jettisonTotal
        bool anyMissionFailed = false;
        if (dramaticEndFlag != 0)
        {
            // Fail every active mission still carrying its (string-described) cargo:
            // forfeit the cargo mass into the jettison total and notify the player.
            for (short m = 0; m < MissionStateTable.Count; m++)
            {
                if (GameData.MissionStates[m].IsActive == 0
                    || GameData.Missions[m].CargoPickedUp == 0
                    || GameData.Missions[m].CargoStringIndex == -1)
                    continue;

                short cargoMass = GameData.Missions[m].CargoMass;
                jettisonTotal += cargoMass;
                playerPodBasis += cargoMass;
                GameData.Missions[m].CargoPickedUp = 0;
                MarkMissionFailed.Run(m);
                EnqueueChatterEvent.Run("Mission failed.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                anyMissionFailed = true;
                if (jettisonTotal == 0)
                {
                    jettisonTotal = 1;
                    playerPodBasis += 1;
                }
            }
        }

        // The player + escorts' combined cargo capacity: the divisor for each ship's fleet share.
        double fleetCapacity = (short)TotalMassWithEscorts.Run();

        // Spawn escape pods from the player (slot 0) and every eligible hired escort.
        for (short shipIdx = 0; shipIdx < ShipTable.Count; shipIdx++)
        {
            if (!QualifiesForEscapePods(shipIdx))
                continue;

            double capacity = shipIdx == 0
                ? (short)ShipDerivedStats.EffectiveCargoMax()
                : GameData.ShipClasses[GameData.Ships[shipIdx].ShipClass].Holds;

            double scaled = shipIdx == 0
                ? playerPodBasis
                : (capacity / fleetCapacity) * cargoJunkTotal;

            if (scaled <= ShipStatConstants.ZeroDouble)
                continue;

            short podCount = (short)(int)(scaled / ShipStatConstants.PostMortemWageDivisor);   // / 5.0
            if (podCount < 1)
                podCount += 1;
            if (podCount > 12)
                podCount = 12;
            for (short p = 0; p < podCount; p++)
                SpawnEscapePodFromShip.Run(ShipTable.Ships[shipIdx]);
            WorldState.HudStatusPanelDirty = 1;
        }

        // Past the ship table: clear the player's cargo holds and announce the dump.
        for (short c = 0; c < ShipRecord.CargoHoldCount; c++)
            GameData.Ships[0].CargoHold[c] = 0;
        if (jettisonTotal > 0)
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
            if (!anyMissionFailed)
            {
                string message = dramaticEndFlag == 0 ? "Non-mission cargo jettisoned." : "Cargo jettisoned.";
                EnqueueChatterEvent.Run(message, 240, 0, 12, UiColors.ChatterText, 0, 0);
            }
        }
    }

    // FUN_10068304 43317-43361 — escape-pod eligibility (LAB_10068598/LAB_100685a0 cross-jump).
    // Eligible when this is one of the player's own ships (OwnerSlot 0) that is a hired escort
    // (AI behaviour 6), not grudging a govt, and of low inherent AI (<= 2); OR it is slot 0.
    private static bool QualifiesForEscapePods(short shipIdx)
    {
        var ship = ShipTable.Ships[shipIdx];
        if (ship.OwnerSlot == 0
            && ship.AiBehaviorType == ShipAiType.Escort
            && ship.GrudgeMissionIndex == -1
            && GameData.ShipClasses[ship.ShipClass].InherentAI <= ShipAiType.BraveTrader)
            return true;
        return shipIdx == 0;   // cross-jump fallthrough: slot 0 always qualifies
    }
}
