using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000d874 (EV Override-11.c lines 7071-7119): mouse-tracks the 4-item
// in-flight comm dialog row; returns the item index at release (-1 none).
public static class HitTestCommButtonRow
{
    private const int CommButtonCount = 4;

    public static int Run(int clickPoint)
    {
        // Comm-button item Rects: {top,left,bottom,right}.
        short[][] itemRects = new short[CommButtonCount][];
        for (int i = 0; i < CommButtonCount; i++)
        {
            itemRects[i] = new short[4];
            MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, i + 1, 0, 0, itemRects[i]);
        }
        int hitItem = HitTest(clickPoint, itemRects);
        if ((short)hitItem != -1)
        {
            RenderCommButtonRow.Run((short)hitItem);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();   // packed (v<<16 | h)
                int dragHitItem = HitTest(mousePoint, itemRects);
                short prevItem = (short)hitItem;
                hitItem = dragHitItem;
                if ((short)dragHitItem != prevItem)
                {
                    RenderCommButtonRow.Run((short)dragHitItem);
                }
            }
        }
        return hitItem;
    }

    // First of the CommButtonCount rects containing the packed point, else -1.
    private static int HitTest(int packedPoint, short[][] itemRects)
    {
        for (int i = 0; i < CommButtonCount; i++)
        {
            if (MacToolbox.PtInRect(packedPoint, itemRects[i])) return i;
        }
        return -1;
    }
}
