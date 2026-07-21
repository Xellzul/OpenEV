using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004e404 (EV Override-11.c lines 32136-32145) — loops over every
// mission slot calling FUN_1004ead4 (UpdateMissionStatusFlags).
public static class TickAllMissions
{
    public static void Run()
    {
        for (int i = 0; i < MissionStateTable.Count; i = i + 1)
        {
            UpdateMissionStatusFlags.Run(i);
        }
    }
}
