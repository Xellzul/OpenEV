using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Misc;

// FUN_1004e44c — per-frame pass over the 8 governments: for each active govt that
// isn't already failed, update its mission status, check for an encounter, and
// (when the player is at the govt's target spob) mark / complete / abort its
// mission as the flags dictate.
// Decompile: EV Override-11.c lines 32151-32193.
public static class TickGovtEncounters
{
    public static void Run()
    {
        for (int g = 0; g < MissionStateTable.Count; g++)
        {
            // Cached by reference — the Mission.* calls mutate these same records,
            // so later field reads see the updates (as the raw-memory original did).
            var gflag = GameData.MissionStates[g];
            var govt = GameData.Missions[g];

            if (gflag.IsActive == 0 || gflag.Failed != 0)
            {
                continue;
            }

            UpdateMissionStatusFlags.Run(g);
            CheckMissionEncounter.Run(g);

            if (GameData.Player.NavTargetSpob == govt.ReturnSpob)
            {
                if (govt.ReturnSpob == govt.TargetSpob)
                {
                    gflag.ArrivedAtTarget = 1;
                }
                if (govt.TargetSpob == -1)
                {
                    gflag.ArrivedAtTarget = 1;
                }

                if (gflag.Failed == 0)
                {
                    if (gflag.ArrivedAtTarget != 0 && gflag.ObjectiveComplete != 0)
                    {
                        ApplyMissionCompletion.Run(g);
                    }
                }
                else
                {
                    ApplyMissionFailure.Run(g);
                }
            }

            UpdateMissionStatusFlags.Run(g);

            if (gflag.Failed != 0 && govt.FailText == -1)
            {
                AbortMission.Run((short)g);
            }
        }
    }
}
