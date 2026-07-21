using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000db44 (EV Override-11.c lines 7173-7221): mouse-tracks the 3-button
// spaceport comm row; returns the button index under the mouse at release (-1 none).
public static class TrackPlanetCommButtonRow
{
    private const int CommButtonCount = 3;

    public static int Run(int startPoint)
    {
        // Comm-button item Rects: {top,left,bottom,right}.
        short[][] itemRects = new short[CommButtonCount][];
        for (int i = 0; i < CommButtonCount; i++)
        {
            itemRects[i] = new short[4];
            MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, i + 1, 0, 0, itemRects[i]);
        }

        int hitIndex = HitTest(startPoint, itemRects);
        if ((short)hitIndex != -1)
        {
            RenderPlanetCommButtonRow.Run((short)hitIndex);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();   // packed (v<<16 | h)
                int dragHitIndex = HitTest(mousePoint, itemRects);
                short prevHitIndex = (short)hitIndex;
                hitIndex = dragHitIndex;
                if ((short)dragHitIndex != prevHitIndex)
                {
                    RenderPlanetCommButtonRow.Run((short)dragHitIndex);
                }
            }
        }
        return hitIndex;
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
