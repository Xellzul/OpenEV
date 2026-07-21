using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_100414a0 (EV Override-11.c 26840-26875) — fade the credits screen in: prime the
// fade if needed, load PICT 131 + the credits CTable, centre and draw the picture, then
// transition the palette from the screen-fade colour to the credits palette. The credits /
// screen-fade / screen palette CTabHandles live behind the named Palette accessors.
public static class ApplyCreditsScreenFade
{
    public static void Run()
    {
        if (Palette.CreditsFadePrimed == 0)
            Palette.FadeIn(16, Palette.ScreenFadeCTab);

        int picture = MacToolbox.GetPicture(131);
        Palette.CreditsPaletteCTab = MacToolbox.GetCTable(1000);
        SetGamePortAndDevice.Run();

        // dstRect is a centred COPY of the port rect (PortRect's getter returns a fresh
        // copy); PaintRect below uses the live PortRect (uncentred).
        short[] dstRect = GlobalState.PortRect;
        RectCenter.Run(picture, dstRect);
        Palette.InstallScreenPalette(Palette.CreditsPaletteCTab, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.DrawPicture(picture, dstRect);
        MacToolbox.HPurge(picture);
        MacToolbox.ReleaseResource(picture);
        SetGamePortAndDevice.Run();
        Palette.InstallColorEntries(Palette.ScreenFadeCTab, 0);
        AnimatePaletteTransition.Run(16, Palette.CreditsPaletteCTab);
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
    }
}
