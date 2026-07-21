using System.Threading;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Title;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Misc;

// FUN_10045778 — EV Override-11.c lines 28932-29059. The Enter Ship launcher: tears down the
// title GWorlds/sound, runs the first-entry intro cutscene, paints the in-game frame + HUD
// panel, then enters the main game loop (RunMainGameLoop / FUN_100621a8).
public static class RunGameSessionLauncher
{
    public static void Run()
    {
        // DEVIATION (faithful): close the host offscreen->screen flush gate before the world
        // build flips GameWorldActive on — the build + HUD paint span several frames during which
        // the offscreen buffer is half-drawn (black), so the host keeps presenting the last good
        // frame (the title) until RunMainGameLoop reopens the gate once the HUD is painted.
        // Mac-invisible host plumbing; without it the radar box flashed black for a split second.
        MacToolbox.GameSceneReady = false;

        // Host render-world setup (sprite tables + textures, screen centre, node UPP tokens,
        // world-state activation, offscreen-GWorld routing) for Enter Ship;
        // the original built these at boot via stores the decompile couldn't resolve. Without it
        // the loop runs but no ship/planet sprites exist — only the HUD shows.
        MacToolbox.OnEnterGameWorld?.Invoke();

        HideCursorOnce.Run();
        DisposeSoundFileChannel.Run(WorldState.FirstEntryCutsceneShown);
        SetMasterVolume.Run((ushort)(GamePrefs.MasterVolume << 5));

        // Dispose the title's leftover sound channels.
        for (short slot = 1; slot < SndChannelTable.Count; slot++)
        {
            int channel = SndChannelTable.Handle(slot);
            if (channel != 0)
            {
                FlushMixQueueEntries.Run(channel);
                MacToolbox.DisposePtr(channel);
                SndChannelTable.SetHandle(slot, 0);
            }
        }

        int backdropPict = TitleScreenGlobals.Pict8000Handle;
        if (backdropPict != 0)
        {
            MacToolbox.HPurge(backdropPict);
            MacToolbox.ReleaseResource(backdropPict);
            TitleScreenGlobals.Pict8000Handle = 0;
        }

        // First-entry cutscene (PICT 8200 + scroll), the first time only. ShowIntroCutscene…
        // reopens the host present gate itself after its opening fade-to-black; it is re-closed
        // below once the post-cutscene fade lands.
        if (!WorldState.FirstEntryCutsceneShown)
        {
            ShowIntroCutsceneAndStartMusic.Run();
            // Fade the intro to black (ScreenFadeCTab — the original never writes it, so it stays
            // black). Composite-paired with RunMainGameLoop's FadeOut: intro→black, world builds
            // black, then the game fades in.
            Palette.FadeIn(16, Palette.ScreenFadeCTab);
            // DEVIATION (faithful): screen is black now — re-close the gate for the world paint +
            // HUD build (no ASM call here; RunMainGameLoop reopens it once the panel is up).
            MacToolbox.GameSceneReady = false;
            StopAndDisposeSoundPair.Run();
            WorldState.FirstEntryCutsceneShown = true;
        }
        else
        {
            Palette.FadeIn(16, Palette.ScreenFadeCTab);   // fade to black, as above
        }

        CheckShareWareRegistrationMatch.Run(out byte matchByte);
        WorldState.SharewareRegisteredMatch = matchByte;

        MacToolbox.MaxMem();   // grow out-param never read; fixed-value shim
        GWorldPort.PaintLetterboxAndBlitInner();
        GWorldPort.SetActivePortSecondaryGame();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PrimaryStageRect);
        GWorldPort.SetActivePortScratch();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.ScratchStageRect);
        SetGamePortAndDevice.Run();

        // Menu-bar region rect = the port rect with the top pulled up 20px; installed into the
        // window's clip + vis regions.
        short[] menuBarRect =
        {
            (short)(GlobalState.PortTop - 20), GlobalState.PortLeft,
            GlobalState.PortBottom, GlobalState.PortRight,
        };
        int window = GlobalState.ActivePortPixmap;
        MacToolbox.RectRgn(MacToolbox.GetPortClipRgn(window), menuBarRect);
        MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(window), menuBarRect);
        HideMacMenuBar.Run();
        RefreshStatusPanel.Run();
        DispatchPendingChatter.Run(0);
        WorldState.HudStatusPanelDirty = 1;
        WorldState.MenuBarHidden = 1;
        EvoGlobals.PlayerDead = false;
        SetGamePortAndDevice.Run();
        SetMasterVolume.Run((ushort)(GamePrefs.MasterVolume << 5));
        if (WorldState.IsCloaked)
        {
            Palette.SetHudColorsWhite();
            ReapplyCloakPalette.Run();
        }
        if (!BugBits.IsSet(BugBit.SkipMainGameLoop))
        {
            RunMainGameLoop.Run();
        }
        // DEVIATION (faithful): RunMainGameLoop's exit fade-to-black left the host's true-color
        // fade composite at level 0. On real Mac the CLUT restore below (RestoreDefault) inherently
        // un-fades; v2 is true-color (no CLUT), so the fade level is a separate composite parameter
        // RestoreDefault doesn't touch — clear it explicitly, else the title stays composited black.
        MacToolbox.ClearScreenFade();
        StopAmbientSoundChannel.Run();
        Palette.RestoreDefault();
        Palette.SetHudColorsActive();
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);

        short[] menuStrip = { 0, 0, 20, GlobalState.PortRight };
        MacToolbox.PaintRect(menuStrip);
        MacToolbox.InvalRect(menuStrip);
        MacToolbox.InvalRect(GlobalState.PortRect);
        TitleScreenGlobals.Pict8000Handle = MacToolbox.GetPicture(8000);

        if (!EvoGlobals.QuitRequested)
        {
            DrawTitleSecondaryPict.Run();
            InitTitleRects.Run();
            // DEVIATION (faithful): the ASM spins `while (StillDown()) {}`. The port adds a
            // Thread.Sleep so a live host StillDown sample doesn't peg the CPU, plus a 200-iteration
            // cap so a headless run doesn't hang if the button never "releases".
            for (int sd = 0; MacToolbox.StillDown(); sd++)
            {
                if (sd > 100) Thread.Sleep(50);
                if (sd > 200) break;
            }
            // BUG (OGB-42, kept): raw event-code ordinal used as mask (ORIGINAL_GAME_BUGS.md) —
            // only ever flushes mouseDown, never keyDown/keyUp/autoKey (also below in this method).
            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
        }
        if (WorldState.IsCursorHiddenByGame)
        {
            MacToolbox.ShowCursor();
            WorldState.IsCursorHiddenByGame = false;
        }
        GWorldPort.PaintLetterboxAndBlitInner();
        HideMacMenuBar.Run();
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.InvalRect(GlobalState.PortRect);
        if (!EvoGlobals.QuitRequested)
        {
            DrawClosedButtons.Run();
            AnimateRowReveal.Run();
            DrawTitleSecondaryPict.Run();
            DrawPilotInfo.Run(1);
        }
        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
        MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
        MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
    }
}
