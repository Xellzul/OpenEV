using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004ead4 (EV Override-11.c lines 32287-32388).
public static class UpdateMissionStatusFlags
{
    public static void Run(int missionIdx)
    {
        short m = (short)missionIdx;
        var missionState = GameData.MissionStates[m];
        if (missionState.IsActive != 0)
        {
            var mission = GameData.Missions[m];
            if (mission.MissionGoalType == MissionGoalKind.None)
            {
                missionState.ObjectiveComplete = 1;
            }
            else if (mission.GoalThreshold < 1)
            {
                missionState.ObjectiveComplete = 1;
            }
            else if (mission.MissionShipsSpawnedCount < mission.GoalThreshold)
            {
                missionState.ObjectiveComplete = 0;
            }
            else
            {
                if (mission.MissionGoalType == MissionGoalKind.DestroyAll && mission.GoalThreshold <= mission.DestroyedShipCount)
                {
                    missionState.ObjectiveComplete = 1;
                }
                if (mission.MissionGoalType == MissionGoalKind.Disable)
                {
                    if (mission.DestroyedShipCount < 1)
                    {
                        if (mission.GoalThreshold <= mission.DisabledShipCount)
                        {
                            missionState.ObjectiveComplete = 1;
                        }
                    }
                    else
                    {
                        missionState.Failed = 1;
                    }
                }
                if ((mission.MissionGoalType == MissionGoalKind.Board || mission.MissionGoalType == MissionGoalKind.RescueDisabled) &&
                    mission.GoalThreshold <= mission.BoardedShipCount)
                {
                    missionState.ObjectiveComplete = 1;
                }
                if (mission.MissionGoalType == MissionGoalKind.Escort)
                {
                    if (mission.DestroyedShipCount < 1 && mission.DisabledShipCount < 1)
                    {
                        short escortCount = 0;
                        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
                        {
                            if (m == GameData.Ships[i].GrudgeMissionIndex && GameData.Ships[i].IsActive != 0)
                            {
                                escortCount = (short)(escortCount + 1);
                            }
                        }
                        if (escortCount == 0)
                        {
                            missionState.ObjectiveComplete = 0;
                        }
                        else
                        {
                            missionState.ObjectiveComplete = 1;
                        }
                    }
                    else
                    {
                        missionState.Failed = 1;
                    }
                }
                if (mission.MissionGoalType == MissionGoalKind.Observe &&
                    (GameData.Player.CurrentSystem == mission.DestSystem || mission.DestSystem == -6) &&
                    mission.SpawnCount <= mission.MissionShipsSpawnedCount)
                {
                    missionState.ObjectiveComplete = 1;
                }
                if (mission.MissionGoalType == MissionGoalKind.ChaseOff)
                {
                    if (mission.DestroyedShipCount + mission.DepartedShipCount < mission.GoalThreshold)
                    {
                        missionState.ObjectiveComplete = 0;
                    }
                    else
                    {
                        missionState.ObjectiveComplete = 1;
                    }
                }
            }
            if (missionState.Failed == 0 && mission.TimeLimit < 1 &&
                -32000 < mission.TimeLimit && SpaceportGlobals.DialogWindow == 0)
            {
                missionState.Failed = 1;
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                EnqueueChatterEvent.Run("Time limit exceeded - mission failed.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                MarkMissionFailed.Run(missionIdx);
            }
        }
    }
}
