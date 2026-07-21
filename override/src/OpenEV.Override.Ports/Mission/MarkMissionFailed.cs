using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004c908 (EV Override-11.c lines 31522-31552). Shared mission-failure finalizer:
// every caller enqueues its own failure chatter/sound first, then calls this to fire the
// two ON-FAIL control-bit links (+0x26/+0x28 — same mechanism as ApplyMissionFailure,
// minus its cron re-arm step), mark the mission Failed, abort it if AbortMissionOnScan is
// set, and invalidate the BBS mission-list cache.
public static class MarkMissionFailed
{
    public static void Run(int missionIdx)
    {
        var mission = GameData.Missions[missionIdx];
        for (short i = 0; i < MissionRecord.FailBitCount; i = (short)(i + 1))
        {
            short link = i == 0 ? mission.FailBitA : mission.FailBitB;
            if (link == -1)
                continue;
            if (-1 < link && link < 512)
            {
                ControlBits.Set(link, 1);
            }
            if (999 < link && link < 1512)
            {
                ControlBits.Set(link - 1000, 0);
            }
        }
        GameData.MissionStates[missionIdx].Failed = 1;
        if (mission.AbortMissionOnScan != 0)
        {
            AbortMission.Run((short)missionIdx);
        }
        SpaceportGlobals.BbsLastSpob = -1;
    }
}
