using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1004165c (EV Override-11.c lines 26894-26917) — boot step 24: draw the Ambrosia
// logo (PICT 8100) centred in the game port, fill the port black, then reveal it from
// the boot fade (FUN_1005d17c). The logo persists through boot steps 25-31 and is faded
// back out at step 32.
public static class DrawTransitionSplashPict
{
    public static void Run()
    {
        int pictHandle = MacToolbox.GetPicture(8100);
        SetGamePortAndDevice.Run();
        // Stack Rect copied from the ctx port rect (GlobalState.PortRect; the
        // getter returns a fresh copy), passed BY ADDRESS to RectCenter/DrawPicture.
        short[] dstRect = GlobalState.PortRect;
        RectCenter.Run(pictHandle, dstRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.DrawPicture(pictHandle, dstRect);
        Palette.FadeOut(16);
        return;
    }
}
