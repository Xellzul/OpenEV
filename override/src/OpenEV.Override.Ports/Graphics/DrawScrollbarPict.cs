using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000ba68 (EV Override-11.c 6070-6091) — draws PICT 9001 twice, stacked
// vertically (64×385 each), into the scrollbar-strip rect, then restores the port.
public static class DrawScrollbarPict
{
    public static void Run()
    {
        int pictHandle = MacToolbox.GetPicture(9001);
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.SetRect(DialogScratch.ScrollbarStripRect, 0, 0, 64, 385);
        MacToolbox.DrawPicture(pictHandle, DialogScratch.ScrollbarStripRect);
        // A stack copy of the strip rect, offset down one strip.
        short[] stripRect =
        {
            DialogScratch.ScrollbarStripRect[0], DialogScratch.ScrollbarStripRect[1],
            DialogScratch.ScrollbarStripRect[2], DialogScratch.ScrollbarStripRect[3]
        };
        MacToolbox.OffsetRect(stripRect, 0, 385);
        MacToolbox.DrawPicture(pictHandle, stripRect);
        MacToolbox.HPurge(pictHandle);
        MacToolbox.ReleaseResource(pictHandle);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
    }
}
