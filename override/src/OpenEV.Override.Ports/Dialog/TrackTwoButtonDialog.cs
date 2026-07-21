using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000e8ec (EV Override-11.c lines 7639-7691): mouse-tracks the 2-button
// game-speed/buy-ship dialog row; returns the button index at release (-1 none).
// The decompile's TOC base here is an unassigned r2/RTOC artifact — the real
// base is GameToc (see DialogScratch.BuyShipDialogRecord's own field comment).
public static class TrackTwoButtonDialog
{
    public static int Run(int clickPoint)
    {
        // auStack_1e / auStack_16: the two button item Rects {top,left,bottom,right}.
        short[] rect0 = new short[4];
        short[] rect1 = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(DialogScratch.BuyShipDialogRecord, 1, 0, 0, rect0);
        MacToolbox.GetDialogItem(DialogScratch.BuyShipDialogRecord, 2, 0, 0, rect1);
        int hitItem = HitTest(clickPoint, rect0, rect1);
        if ((short)hitItem != -1)
        {
            Graphics.DrawGameSpeedDialogButtons.Run((short)hitItem);
            while (MacToolbox.StillDown())
            {
                // GetMouse returns the packed Point VALUE; pass it straight to PtInRect
                // (don't pass a pointer/ref to the local — the decompile's PtInRect(local_28, ...)
                // reads the same VALUE GetMouse just wrote, not its address).
                int mousePoint = MacToolbox.GetMouse();   // packed (v<<16 | h)
                int curItem = HitTest(mousePoint, rect0, rect1);
                short prevItem = (short)hitItem;
                hitItem = curItem;
                if ((short)curItem != prevItem)
                {
                    Graphics.DrawGameSpeedDialogButtons.Run((short)curItem);
                }
            }
        }
        return hitItem;
    }

    // Index of the button rect containing the packed point, else -1
    // (rect1 wins when the rects overlap, matching the original's write order).
    private static int HitTest(int packedPoint, short[] rect0, short[] rect1)
    {
        int hit = -1;
        if (MacToolbox.PtInRect(packedPoint, rect0)) hit = 0;
        if (MacToolbox.PtInRect(packedPoint, rect1)) hit = 1;
        return hit;
    }
}
