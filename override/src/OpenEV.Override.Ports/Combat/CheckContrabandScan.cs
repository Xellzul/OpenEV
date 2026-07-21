namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Pilot.Model;

// Port of FUN_10000210 (EV Override-11.c 1249-1298) — a scan-penalty government's govt-defender or
// interceptor (AiBehaviorType 3/4) scans a nearby player for an active smuggling mission's cargo: it
// fails the mission (or just warns, if the scan is disarmed or already failed), then breaks off to engage.
public static class CheckContrabandScan
{
    public static void Run(ShipRec ship)
    {
        if (ship.AiBehaviorType != ShipAiType.Warship && ship.AiBehaviorType != ShipAiType.Interceptor)
            return;
        if (ship.Govt == -1 || Core.Model.GameData.Governments[ship.Govt].ScanPenalty == 0)
            return;

        for (int slotIndex = 0; slotIndex < MissionTable.Count; slotIndex++)
        {
            // Decompile dual induction (int iVar2 + short sVar3): the short indexes the tables, the int
            // is passed to MarkMissionFailed.
            short slot = (short)slotIndex;
            var mission = Core.Model.GameData.Missions[slot];
            if (ship.Govt != mission.ScanPersIndex || mission.CargoPickedUp == 0 || mission.CargoStringIndex == -1)
                continue;
            if (EvMath.FloatAbs((double)(ship.PosX - ShipTable.PosX)) > ShipStatConstants.AiScanApproachDistance) continue;
            if (EvMath.FloatAbs((double)(ship.PosY - ShipTable.PosY)) > ShipStatConstants.AiScanApproachDistance) continue;

            if (mission.ContrabandScanArmed != 0 && Core.Model.GameData.MissionStates[slot].Failed == 0)
            {
                Core.Model.GameData.MissionStates[slot].Failed = 1;
                TriggerSoundPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128);
                EnqueueChatterEvent.Run("Your ship has been scanned - mission failed.", 400, 0, 12, UiColors.ChatterText, 0, 0);
                MarkMissionFailed.Run(slotIndex);
            }
            else
            {
                TriggerSoundPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128);
                EnqueueChatterEvent.Run(
                    PilotIdentity.ShipName + ", your attempt to smuggle illegal cargo through this system has been detected.",
                    400, 0, 12, UiColors.ChatterText, 0, 0);
            }
            ship.TargetSlot = 0;
            ship.NavTargetSpob = -1;
            ship.AiState = ShipAiState.AttackShip;
            return;
        }
    }
}
