using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10043458 (EV Override-11.c lines 27885-28011).
// Mac mouseDown dispatch: FindWindow routes desk/menu/system clicks; content
// clicks hit-test the title buttons:
//   0 → New Pilot (overwrite alert → NewPilotOrchestrator)
//   1 → Enter Ship
//   2 → Open Pilot
//   3 → Set Prefs
//   4 → Quit (delay + set quit flag)
//   5/6 → About EVÉ
//
// The port's FindWindow stub always returns inContent(3), so only the which<4
// branch below ever runs; the desk/menu and system-window (SystemClick, a
// no-op stub either way) branches are unreachable but kept for parity.
//
// The Mac passes an EventRecord and reads ONLY its 'where' Point (+10), so the
// managed signature takes the packed mouse point directly.
public static class DispatchTitleEvent
{
    public static void Run(int mousePoint)
    {
        int window = 0;
        short which = (short)MacToolbox.FindWindow(mousePoint, window);
        if (which == 2)
        {
            MacToolbox.SystemClick(0, window);
        }
        else if (which < 2)
        {
            if (which == 0)
            {
                MacToolbox.SysBeep(1);
            }
            else if (-1 < which)
            {
                MacToolbox.InitCursor();
                int menuSelection = MacToolbox.MenuSelect(mousePoint);
                HandleMenuChoice.Run(menuSelection);
            }
        }
        else if (which < 4)
        {
            // Cancel the title intro-pulse on the first content-region click. ButtonRevealPulse
            // gates AnimateRowReveal + DrawClosedButtons' overlay. The Mac clears it in
            // DrawClosedButtons (decompile L29516) when Button() is down; the credits
            // dismiss-click is still held when that runs, so the flag clears there and the
            // following AnimateRowReveal no-ops. The port's Button() reads false by then (release
            // happened during PaletteFadeIn), so that click-driven clear never fires — clearing
            // here on the content click is the faithful equivalent.
            TitleScreenGlobals.ButtonRevealPulse = false;
            which = (short)HitTestTitleButton.Run(mousePoint);
            if (which == 0)
            {
                SndPlay.Run(SndChannelTable.Handle(1), 10, 128, 128);
                byte confirmed;
                if (!WorldState.PilotLoaded)
                {
                    confirmed = 1;
                }
                else
                {
                    // toc-0x42a0 Pascal string (data-seg 0x100843c0, PEF dump).
                    confirmed = (byte)AlertModal_TwoButton.Run(
                        "There’s already a pilot file loaded. Are you sure you want to create a new one?");
                    // Restore the screen from the BACKDROP GWorld.
                    MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2,
                                        GlobalState.ActivePortPixmap + 2,
                                        GlobalState.PortRect, GlobalState.PortRect, 0, 0);
                }
                if (confirmed != 0)
                {
                    NewPilotOrchestrator.Run();
                    SetGamePortAndDevice.Run();
                    // The InvalRect queues an updateEvt whose handler (DrawPilotInfo) repaints
                    // the title behind the disposed name dialog — the original's mechanism.
                    // (An earlier port repainted inline here because InvalRect was a no-op.)
                    MacToolbox.InvalRect(GlobalState.PortRect);
                }
            }
            if (which == 2)
            {
                SndPlay.Run(SndChannelTable.Handle(1), 10, 128, 128);
                byte confirmed = (byte)OpenPilot.Run();
                if (confirmed != 0)
                {
                    WorldState.PilotLoaded = true;
                    MacToolbox.InvalRect(GlobalState.PortRect);
                    CleanupSystNpcs.Run(0);
                    RunFleetSpawner.Run(GameData.Player.CurrentSystem);
                    RecomputeWorldVisibility.Run();
                    Asteroids.Init();
                    for (short systIdx = 0; systIdx < SystTable.Count; systIdx++)
                    {
                        if (SystTable.Store[systIdx].ShownFlag != 0 &&
                            0 < SystTable.Store[systIdx].Visited)
                        {
                            MarkGalaxyMapClustersForSyst.Run(systIdx);
                        }
                    }
                    for (short misnStateIndex = 0; misnStateIndex < MissionStateTable.Count; misnStateIndex++)
                    {
                        if (GameData.MissionStates[misnStateIndex].IsActive != 0)
                        {
                            // BUG-FOR-PARITY: the decompile indexes the 8-entry MissionTable with
                            // sVar9 — the STALE syst-loop var (= 1000 after the loop above), not
                            // misnStateIndex. That's out of bounds, on never-written heap tail that
                            // the port's zeroed memory always read as 0: the +0x58 flag check (& 0x10) and
                            // the +100 counter check were both always false, so the wild
                            // +100/+0x62/+0x60 writes never executed. The whole body is dead —
                            // preserved as this note instead of raw reads of
                            // MissionTable.Base + 1000*0x186 (the EvoMemory tail is gone).
                        }
                    }
                    // new-game / Enter-Ship world-setup flags.
                    GalaxyMapState.TradeKeyLock = 0;
                    WorldState.NoAsteroidsFlag = 1;
                    WorldState.ClearStreaksFlag = 1;
                    WorldState.ClearExplosionsFlag = 1;
                    WorldState.ClearCarriedSpritesFlag = 1;
                    WorldState.ClearShotsFlag = 1;
                    TickSpriteSystem.Run();
                    // The mid-branch InvalRect above already queued the updateEvt that
                    // repaints the panel with the loaded pilot (the original's mechanism;
                    // an earlier port repainted inline here because InvalRect was a no-op).
                }
            }
            if (which == 4)
            {
                SndPlay.Run(SndChannelTable.Handle(1), 10, 128, 128);
                MacToolbox.Delay(15, 0);           // finalTicks out-param discarded
                EvoGlobals.QuitRequested = true;
            }
            if (which == 1)
            {
                if (!WorldState.PilotLoaded)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 10, 128, 128);
                }
                else
                {
                    SndPlay.Run(SndChannelTable.Handle(1), 1, 128, 128);
                    bool alreadyInShip = ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Player);
                    if (!alreadyInShip)
                    {
                        RunGameSessionLauncher.Run();
                    }
                    else
                    {
                        MacToolbox.SysBeep(0);
                    }
                }
            }
            if (which == 3)
            {
                SndPlay.Run(SndChannelTable.Handle(1), 10, 128, 128);
                PrefsDialog.Run();
                SetGamePortAndDevice.Run();
                DrawPilotInfo.Run(1);
                SetGamePortAndDevice.Run();
                MacToolbox.InvalRect(GlobalState.PortRect);
            }
            if (which == 5 || which == 6)
            {
                SndPlay.Run(SndChannelTable.Handle(1), 10, 128, 128);
                AboutEvoModal.Run();
            }
        }
    }
}
