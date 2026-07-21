using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Title;

namespace OpenEV.Override.Ports.Boot;

// FUN_10054734 (EV Override-11.c lines 34549-34581) — boot low-memory check:
// if MaxMem reports under 9.2MB free, compose "Sorry, EV needs at least an
// additional NNNK of memory…" and fatal-exit through the one-button alert.
// The two warning strings are GameToc data-seg C-strings, dumped to literals
// (GameToc-0x3db5 / GameToc-0x3d8d). The branch is dead here — MaxMem is a fixed
// 256MB shim, so freeMem is always well above the threshold.
public static class MemoryCheckOnStartup
{
    public static void Run()
    {
        int freeMem = MacToolbox.MaxMem();   // grow out-param never read; fixed-value shim
        if (freeMem < 9200000)
        {
            TearDownSavedPalette.Run();
            DisposeSoundFileChannel.Run(false);
            string message = "Sorry, EV needs at least an additional "                     // C string GameToc-0x3db5
                           + (((9200000 - freeMem) / 10000 + 1) * 10)                      // NumToString(shortfall, rounded up to 10K)
                           + "K of memory to run in this resolution. Please increase its memory allocation and try again.";   // GameToc-0x3d8d
            Palette.FadeOut(16);   // fade step count
            SetGamePortAndDevice.Run();
            MacToolbox.HideWindow(GlobalState.ActivePortPixmap);   // the on-screen game window
            RestoreMacMenuBar.Run();
            MacToolbox.ShowCursor();
            AlertModal_OneButton.Run(message);
            TeardownSoundSubsystem.Run();
            MacToolbox.ExitToShell();
        }
    }
}
