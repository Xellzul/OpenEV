using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000ccb8 (EV Override-11.c lines 6668-6764) — hit-test + press-track the
// spaceport hub's tab bar. Maps a local click point onto the 7 tab rects (same
// DITL items as DrawSpaceportTabBar), then while the button stays down
// re-tests the mouse (with each tab's enable gate) and redraws the bar with
// the hovered tab pressed. Returns the final tab index 0..6, or -1.
public static class TrackDialogButtonHit
{
    public static int Run(int clickPoint)
    {
        var tabRects = new short[7][];
        for (int i = 0; i < 7; i++) tabRects[i] = new short[4];

        int window = SpaceportGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 12, 0, 0, tabRects[0]);
        MacToolbox.GetDialogItem(window, 4, 0, 0, tabRects[1]);
        MacToolbox.GetDialogItem(window, 7, 0, 0, tabRects[2]);
        MacToolbox.GetDialogItem(window, 8, 0, 0, tabRects[3]);
        MacToolbox.GetDialogItem(window, 9, 0, 0, tabRects[4]);
        if (((uint)CurrentSpob.Rec.Flags & 0x40) == 0)
        {
            MacToolbox.GetDialogItem(window, 13, 0, 0, tabRects[5]);
        }
        else
        {
            MacToolbox.GetDialogItem(window, 10, 0, 0, tabRects[5]);
        }
        MacToolbox.GetDialogItem(window, 11, 0, 0, tabRects[6]);
        int hitItem = -1;
        for (int tab = 0; tab < 7; tab++)
        {
            if (MacToolbox.PtInRect(clickPoint, tabRects[tab]))
            {
                hitItem = tab;
            }
        }
        uint spobFlags = (uint)CurrentSpob.Rec.Flags;
        if ((short)hitItem != -1)
        {
            DrawSpaceportTabBar.Run((short)hitItem);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();
                int curItem = -1;
                if (MacToolbox.PtInRect(mousePoint, tabRects[0]))
                {
                    curItem = 0;
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[1]) && (spobFlags & 0x20) == 0
                    && 0 < GameData.Ships[0].Credits)
                {
                    short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player);
                    if (GameData.Ships[0].Fuel < (float)fuelMax)
                    {
                        curItem = 1;
                    }
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[2]) && ((uint)CurrentSpob.Rec.Flags & 2) != 0)
                {
                    curItem = 2;
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[3]) && ((uint)CurrentSpob.Rec.Flags & 4) != 0)
                {
                    curItem = 3;
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[4]) && ((uint)CurrentSpob.Rec.Flags & 8) != 0)
                {
                    curItem = 4;
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[5]) && SpaceportGlobals.MissionBbsEnabled != 0)
                {
                    curItem = 5;
                }
                if (MacToolbox.PtInRect(mousePoint, tabRects[6]) && ((uint)CurrentSpob.Rec.Flags & 0x40) != 0)
                {
                    curItem = 6;
                }
                short prevItem = (short)hitItem;
                hitItem = curItem;
                if ((short)curItem != prevItem)
                {
                    DrawSpaceportTabBar.Run((short)curItem);
                }
            }
        }
        return hitItem;
    }
}
