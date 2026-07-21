using OpenEV.Override.Ports.Core.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1005ffe8 (EV Override-11.c lines 40061-40076): two-step backbuffer flush —
// copy the backdrop GWorld into the offscreen compose port, then that onto the screen
// port, through the HUD play-area clip rect. No-op unless all three ports exist.
public static class TwoStepRepaintGameWindow
{
    public static void Run()
    {
        short[] clipRect = GlobalState.HudPlayAreaClipRect;
        if (GlobalState.ActivePortPixmap != 0 &&
            GlobalState.OffscreenGameGWorld != 0 &&
            GlobalState.AnimScratchPort != 0)
        {
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.OffscreenGameGWorld + 2, clipRect, clipRect, 0, 0);
            MacToolbox.CopyBits(GlobalState.OffscreenGameGWorld + 2, GlobalState.ActivePortPixmap + 2, clipRect, clipRect, 0, 0);
        }
    }
}
