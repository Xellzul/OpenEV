using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000e688 (EV Override-11.c lines 7560-7610) — hit-test + press-track the
// player-info dialog's 4-tab row (DITL items 1..4). While the button stays down
// re-tests the mouse and redraws the row with the hovered tab pressed. Returns
// the final tab index 0..3, or -1. page (param_1) is forwarded unchanged as
// RenderPlayerInfoTabRow's highlightTabA (the original passes the 1-based page).
public static class TrackPlayerInfoTabMouseDown
{
    private const int TabCount = 4;

    public static int Run(int page, int clickPoint)
    {
        short[][] tabRects = new short[TabCount][];
        int window = PlayerInfoGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (int tab = 0; tab < TabCount; tab++)
        {
            tabRects[tab] = new short[4];
            MacToolbox.GetDialogItem(window, tab + 1, 0, 0, tabRects[tab]);
        }
        int selectedTab = HitTest(clickPoint, tabRects);
        if ((short)selectedTab != -1)
        {
            RenderPlayerInfoTabRow.Run((short)page, (short)selectedTab);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();   // packed (v<<16 | h)
                int currentTab = HitTest(mousePoint, tabRects);
                short prevTab = (short)selectedTab;
                selectedTab = currentTab;
                if ((short)currentTab != prevTab)
                {
                    RenderPlayerInfoTabRow.Run((short)page, (short)currentTab);
                }
            }
        }
        return selectedTab;
    }

    // First of the TabCount rects containing the packed point, else -1.
    private static int HitTest(int packedPoint, short[][] tabRects)
    {
        for (int tab = 0; tab < TabCount; tab++)
        {
            if (MacToolbox.PtInRect(packedPoint, tabRects[tab])) return tab;
        }
        return -1;
    }
}
