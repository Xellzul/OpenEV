using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10020f20 (EV Override-11.c lines 14636-14661): tick the comm-flash countdown.
// While active, decrement it; when it expires, clear the scratch port to black and copy the
// backdrop GWorld back over the on-screen window and the offscreen compose port, through the
// HUD play-area clip rect.
public static class TickFlashEffectCountdown
{
    public static void Run()
    {
        short[] clipRect = GlobalState.HudPlayAreaClipRect;

        if (-1 < WorldState.FlashChatterCountdown)
        {
            WorldState.FlashChatterCountdown -= 1;
            if (WorldState.FlashChatterCountdown < 1)
            {
                GWorldPort.SetActivePortScratch();
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(clipRect);
                SetGamePortAndDevice.Run();
                MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.ActivePortPixmap + 2, clipRect, clipRect, 0, 0);
                MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.OffscreenGameGWorld + 2, clipRect, clipRect, 0, 0);
            }
        }
    }
}
