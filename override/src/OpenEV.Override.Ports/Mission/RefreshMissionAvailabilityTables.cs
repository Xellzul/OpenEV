using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_10048658 (EV Override-11.c lines 30216-30266) — rebuild both mission/
// person availability lists (MissionAvailGrid.ByMode: [0] = mission BBS,
// [1] = bar). For each mode it sets InBarFlag (so the eligibility check sees
// the right context), collects every 'bär' person that is eligible and not
// already an active mission (MissionStateTable slot live + MissionDefIndex
// match), then resolves each entry's mission spawn; the saved InBarFlag is
// restored at the end.
public static class RefreshMissionAvailabilityTables
{
    public static void Run()
    {
        short saved = SpaceportGlobals.InBarFlag;
        for (short mode = 0; mode < MissionAvailGrid.ByMode.Length; mode = (short)(mode + 1))
        {
            SpaceportGlobals.InBarFlag = mode;
            var list = MissionAvailGrid.ByMode[mode];
            for (short i = 0; i < MissionAvailGrid.Count; i = (short)(i + 1))
            {
                list[i] = -1;
            }
            short count = 0;
            for (short pers = 0; pers < MissionAvailTable.Count; pers = (short)(pers + 1))
            {
                bool alreadyActive = false;
                for (short g = 0; g < MissionStateTable.Count; g = (short)(g + 1))
                {
                    if (GameData.MissionStates[g].IsActive != 0 &&
                        pers == GameData.Missions[g].MissionDefIndex)
                    {
                        alreadyActive = true;
                    }
                }
                if (!alreadyActive && IsBarPersEligible.Run(pers))
                {
                    list[count] = pers;
                    count = (short)(count + 1);
                }
            }
            for (short i = 0; i < MissionAvailGrid.Count; i = (short)(i + 1))
            {
                if (list[i] != -1)
                {
                    ResolveSingleMissionSpawn.Run(list[i]);
                }
            }
        }
        SpaceportGlobals.InBarFlag = saved;
    }
}
