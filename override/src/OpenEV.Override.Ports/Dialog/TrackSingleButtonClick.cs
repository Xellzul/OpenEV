using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000f3b0 (EV Override-11.c lines 7996-8027): tracks a mouse-down on a
// single dialog button — draws the pressed/normal PICT (DrawButtonPressed,
// FUN_1000f498) as the mouse moves in/out of the rect until StillDown()
// clears, and returns whether the release happened inside the rect. Used by
// the Generic/PictureAlertDialogFilter's OK button.
public static class TrackSingleButtonClick
{
    public static bool Run(short[] rect)
    {
        bool prevInRect = MacToolbox.PtInRect(MacToolbox.GetMouse(), rect);
        DrawButtonPressed.Run(rect, prevInRect);
        while (MacToolbox.StillDown())
        {
            bool inRect = MacToolbox.PtInRect(MacToolbox.GetMouse(), rect);
            bool stateChanged = inRect != prevInRect;
            prevInRect = inRect;
            if (stateChanged)
            {
                DrawButtonPressed.Run(rect, inRect);
            }
        }
        return prevInRect;
    }
}
