// Port of FUN_1001b56c (EV Override-11.c lines 12370-12418).

namespace OpenEV.Override.Ports.Boot;

using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Pilot;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;

// Boot last-pilot auto-load: draws the loading screen, then — if a "Last Pilot"
// file exists — resumes it (LoadPluginPilotData) and re-primes the galaxy-map and
// active-mission state. Called near the end of boot init (entry → FUN_10061bb0,
// line 41051).
public static class AutoLoadLastPilotAtBoot
{
    public static void Run()
    {
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        Palette.FadeOut(4);

        // "Last Pilot" — the Pascal data-seg constant at toc-0x5313 (0x1008334d); the
        // same string seeds both the exists-check and the loader call.
        var pilotFileName = "Last Pilot";
        var pilotExists = PilotFileExistsOnDefaultVolume.Run(pilotFileName) != 0;
        if (pilotExists)
        {
            CleanupSystNpcs.Run(1);
            InitGameWorldState.Run(1);
            ResetCommodityPriceLimits.Run(1);
            short loadResult = (short)LoadPluginPilotData.Run(
                PrefsFolderLocation.VRefNum,
                PrefsFolderLocation.DirID,
                pilotFileName);
            if (loadResult == 0)
            {
                WorldState.PilotLoaded = true;
                for (short systIndex = 0; systIndex < SystTable.Count; systIndex++)
                {
                    if (SystTable.Store[systIndex].ShownFlag != 0 &&
                        0 < SystTable.Store[systIndex].Visited)
                    {
                        MarkGalaxyMapClustersForSyst.Run(systIndex);
                    }
                }
                for (short missionIndex = 0; missionIndex < MissionTable.Count; missionIndex++)
                {
                    if (GameData.MissionStates[missionIndex].IsActive != 0)
                    {
                        if ((GameData.Missions[missionIndex].Flags & MisnFlags.AuxShipsReplacedWhenDestroyed) != 0)
                        {
                            GameData.Missions[missionIndex].RemainingSpawnCount =
                                GameData.Missions[missionIndex].AuxShipCount;
                        }
                        short randomDelay = (short)SeedEvoRng.Run(70);
                        GameData.Missions[missionIndex].SpawnCountdown = (short)(randomDelay + 70);
                        GameData.Missions[missionIndex].LiveSpawnCount = 0;
                    }
                }
            }
        }
        Palette.FadeIn(4, Palette.ScreenFadeCTab);   // cell 0x10080e00 the original never writes → fade to black
    }
}
