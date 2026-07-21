using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_10034b90 (EV Override-11.c lines 21604-21626)
public static class EngageAutopilotToHistoryTarget
{
    public static void Run()
    {
        short targetSyst = GalaxyMapGlobals.NavHistory[1];
        if (targetSyst != -1)
        {
            var player = GameData.Player;
            var currentSyst = GameData.Systems[player.CurrentSystem];

            for (short linkIndex = 0; linkIndex < SystRecord.HyperLinkCount; linkIndex++)
            {
                if (targetSyst == currentSyst.HyperLink[linkIndex])
                {
                    player.NavMode = 3;
                    player.NavTargetSpob = linkIndex;
                    WorldState.SpawnPulseDirty = 1;
                    return;
                }
            }
        }
    }
}
