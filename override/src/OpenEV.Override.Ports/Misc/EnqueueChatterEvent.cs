using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100679b0 from EV Override-11.c lines 43051-43103. Draws one HUD chatter
// line into the play-area clip rect and remembers it for the DispatchPendingChatter replay.
public static class EnqueueChatterEvent
{
    // Managed-string CORE: the original copied the message into the aux replay buffer
    // (*0x1008122c, 0xff cap) and a local C-string copy (0xfe cap) for TETextBox; both
    // are the managed string now.
    public static void Run(string message, short refValue, short fontId, short fontSize,
                     int textColor, byte boldFlag, byte skipCopyBack)
    {
        short[] textRect = GlobalState.HudPlayAreaClipRect;
        WorldState.FlashChatterCountdown = refValue;
        ChatterState.LastMessage = message;
        GWorldPort.SetActivePortScratch();
        MacToolbox.RGBForeColor((uint)textColor);
        MacToolbox.TextFont(fontId);
        MacToolbox.TextSize(fontSize);
        if (boldFlag != 0)
        {
            MacToolbox.TextFace(2);
        }
        MacToolbox.BackColor(QuickDrawColor.Black);
        MacToolbox.TETextBox(message, textRect, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.BackColor(QuickDrawColor.White);
        if (skipCopyBack == 0)
        {
            SetGamePortAndDevice.Run();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.BackColor(QuickDrawColor.White);
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.OffscreenGameGWorld + 2, textRect, textRect, 0, 0);
            MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, GlobalState.ActivePortPixmap + 2, textRect, textRect, 0, 0);
        }
    }
}
