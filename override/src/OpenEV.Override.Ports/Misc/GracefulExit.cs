using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 33857-33880.
// FUN_1005296c reads three globals through a raw, never-initialized local acting as the r2
// TOC base (tocBase): tocBase-0x7958 = 0x10080d08 (main window ctx; +0xc is its port rect,
// GlobalState.PortRect), tocBase-0x74a0 = 0x100811c0 (double-deref to the game-window ptr,
// GameWindowGlobals.GameWindowPtr), tocBase+0x6d60 = 0x1008f3c0 (cursor-hidden flag,
// WorldState.IsCursorHiddenByGame).
public static class GracefulExit
{
    public static void Run()
    {
        Palette.FadeIn(8, Palette.ScreenFadeCTab);   // ScreenFadeCTab never seeded -> fades to black
        TeardownSoundSubsystem.Run();
        RestoreMacMenuBar.Run();
        junkcode.FUN_100600f0();
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.HideWindow(GameWindowGlobals.GameWindowPtr);
        if (WorldState.IsCursorHiddenByGame)
        {
            MacToolbox.ShowCursor();
        }
        Palette.FadeOut(8);
        TearDownSavedPalette.Run();
        CloseShareWareRegistrationSession.Run();
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
        SetGamePortAndDevice.Run();
        MacToolbox.ExitToShell();
    }
}
