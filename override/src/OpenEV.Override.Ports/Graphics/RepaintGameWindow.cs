using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1005ff4c (EV Override-11.c lines 40037-40054): repaint the game window —
// restore the screen port/device, then blit the offscreen compose port onto the screen
// port, over the window port rect with the right edge pulled in 144px (the HUD status
// panel keeps its own contents). No-op unless the screen, offscreen and anim-scratch
// ports all exist.
public static class RepaintGameWindow
{
    public static void Run()
    {
        if (GlobalState.ActivePortPixmap != 0 &&
            GlobalState.OffscreenGameGWorld != 0 &&
            GlobalState.AnimScratchPort != 0)
        {
            short[] rect =
            {
                GlobalState.PortTop, GlobalState.PortLeft,
                GlobalState.PortBottom, (short)(GlobalState.PortRight - 144),
            };
            SetGamePortAndDevice.Run();
            MacToolbox.CopyBits(GlobalState.OffscreenGameGWorld + 2,
                                 GlobalState.ActivePortPixmap + 2, rect, rect, 0, 0);
        }
    }
}
