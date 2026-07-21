using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10044370 (EV Override-11.c lines 28330-28359).
// Black-clears the BACKDROP GWorld, then stages PICT 8002 (the title's
// secondary art strip) into the ANIM scratch GWorld after black-clearing its
// stage rect. ("Title" in the name is apt — this runs from TitleMainLoop —
// but the same backdrop GWorld serves gameplay too.)
public static class DrawTitleSecondaryPict
{
    public static void Run()
    {
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        // Stack COPY of the backdrop GWorld portRect (decompile local_18/_14).
        short[] rect = RenderGlobals.BackdropPortRect;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(rect);
        SetGamePortAndDevice.Run();
        int pictHandle = MacToolbox.GetPicture(8002);
        if (pictHandle != 0)
        {
            GWorldPort.SetActivePortScratch();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(GlobalState.ScratchStageRect);
            MacToolbox.SetRect(rect, 0, 0, 174, 177);
            MacToolbox.DrawPicture(pictHandle, rect);
            MacToolbox.HPurge(pictHandle);
            MacToolbox.ReleaseResource(pictHandle);
        }
        SetGamePortAndDevice.Run();
    }
}
