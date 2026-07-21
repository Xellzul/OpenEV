using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.GalaxyMap;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000be2c (EV Override-11.c lines 6174-6255) — mouse-down tracker for the
// galaxy-map dialog's 4-button row (OK item 1, zoom-in item 4, zoom-out item 5,
// route item 8; items 4/5/8 gated by the Plus/Minus/Route enable flags). Returns
// the pressed button index (0..3) or -1, repainting the pressed cell as the mouse
// drags across the row while held.
public static class Track4ButtonMouseDown
{
    public static int Run(int mousePt)
    {
        var rectOk = new short[4];     // item 1 (OK)
        var rectPlus = new short[4];   // item 4 (zoom in)
        var rectMinus = new short[4];  // item 5 (zoom out)
        var rectRoute = new short[4];  // item 8 (route)

        int dialogPtr = GalaxyMapState.MapDialog;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(dialogPtr, 1, 0, 0, rectOk);
        MacToolbox.GetDialogItem(dialogPtr, 4, 0, 0, rectPlus);
        MacToolbox.GetDialogItem(dialogPtr, 5, 0, 0, rectMinus);
        MacToolbox.GetDialogItem(dialogPtr, 8, 0, 0, rectRoute);
        int selectedIndex = -1;
        if (MacToolbox.PtInRect(mousePt, rectOk))
        {
            selectedIndex = 0;
        }
        if (MacToolbox.PtInRect(mousePt, rectPlus) && GalaxyMapState.PlusEnabled != 0)
        {
            selectedIndex = 1;
        }
        if (MacToolbox.PtInRect(mousePt, rectMinus) && GalaxyMapState.MinusEnabled != 0)
        {
            selectedIndex = 2;
        }
        if (MacToolbox.PtInRect(mousePt, rectRoute) && GalaxyMapState.RouteActive != 0)
        {
            selectedIndex = 3;
        }
        if ((short)selectedIndex != -1)
        {
            Render4ButtonRow.Run((short)selectedIndex);
            while (MacToolbox.StillDown())
            {
                int loopPt = MacToolbox.GetMouse();
                int newIndex = -1;
                if (MacToolbox.PtInRect(loopPt, rectOk))
                {
                    newIndex = 0;
                }
                if (MacToolbox.PtInRect(loopPt, rectPlus) && GalaxyMapState.PlusEnabled != 0)
                {
                    newIndex = 1;
                }
                if (MacToolbox.PtInRect(loopPt, rectMinus) && GalaxyMapState.MinusEnabled != 0)
                {
                    newIndex = 2;
                }
                if (MacToolbox.PtInRect(loopPt, rectRoute) && GalaxyMapState.RouteActive != 0)
                {
                    newIndex = 3;
                }
                short prevIndex = (short)selectedIndex;
                selectedIndex = newIndex;
                if ((short)newIndex != prevIndex)
                {
                    Render4ButtonRow.Run((short)newIndex);
                }
            }
        }
        return selectedIndex;
    }
}
