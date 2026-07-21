using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_100415cc (EV Override-11.c 26876-26893) — fade the credits/boot splash out to black:
// install the credits palette, colour-cycle the screen-fade palette, reinstall the screen
// palette, then black-clear the port rect. The credits/screen-fade/screen palette CTabHandles
// live behind the named Palette accessors.
public static class ApplyCreditsScreenFadeOut
{
    public static void Run()
    {
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // Host-substrate bridge (not in the decompile): composite-fade the buffer to black,
        // then ClearScreenFade restores full brightness so boot/title draw visibly. The
        // AnimatePaletteColorCycle below is the faithful but inert CLUT fade.
        MacToolbox.ScreenFadeToColor(24, 0, 0, 0);
        Palette.InstallScreenPalette(Palette.CreditsPaletteCTab, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        AnimatePaletteColorCycle.Run(24, Palette.ScreenFadeCTab);
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);   // black-clear the live port/blit rect
        MacToolbox.ClearScreenFade();
    }
}
