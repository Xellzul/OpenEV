using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004c780 (EV Override-11.c lines 31489-31521). Drops every active
// ship's grudge on mission slot missionIndex (freed escorts revert to their class AI, go
// owner-less and inert; ships shown while a spaceport dialog is open deactivate),
// then clears that mission slot's cargo-pickup + active state and forces a BBS
// availability regenerate.
public static class AbortMission
{
    public static void Run(short missionIndex)
    {
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            var s = ShipTable.Ships[i];
            if (s.IsActive != 0 && missionIndex == s.GrudgeMissionIndex)
            {
                s.GrudgeMissionIndex = -1;
                if (s.OwnerSlot != -1)
                {
                    s.AiBehaviorType = GameData.ShipClasses[s.ShipClass].InherentAI;
                    s.OwnerSlot = -1;
                    ShipAi.SetStateInert(s);
                }
                if (SpaceportGlobals.DialogWindow != 0)
                {
                    s.IsActive = 0;
                }
            }
        }
        GameData.Missions[missionIndex].CargoPickedUp = 0;
        GameData.MissionStates[missionIndex].IsActive = 0;
        SpaceportGlobals.BbsLastSpob = -1;
    }
}
