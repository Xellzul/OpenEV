using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_10051c90 (EV Override-11.c lines 33509-33545).
public static class ApplyMissionCompletionBits
{
    public static void Run(int missionIndex)
    {
        short missionSlot = (short)missionIndex;
        for (short i = 0; i < MissionRecord.CompletionBitCount; i = (short)(i + 1))
        {
            short link = LinkAt(missionSlot, i);
            if (link != -1)
            {
                if (-1 < link && link < 512)
                {
                    ControlBits.Set(link, 1);
                    foreach (var cron in GameData.Crons)
                    {
                        if (cron.ControlBit == link && 0 < cron.DurationDays)
                        {
                            cron.StateCountdown = cron.DurationDays;
                        }
                    }
                }
                if (999 < link && link < 1512)
                {
                    ControlBits.Set(link - 1000, 0);
                }
            }
        }
        AbortMission.Run(missionSlot);
    }

    // Returns CompletionBit A/B/C/D — the decompile's contiguous +0x1a + i*2 halfword read.
    private static short LinkAt(short missionSlot, short i)
    {
        var mission = GameData.Missions[missionSlot];
        return i switch { 0 => mission.CompletionBitA, 1 => mission.CompletionBitB, 2 => mission.CompletionBitC, _ => mission.CompletionBitD };
    }
}
