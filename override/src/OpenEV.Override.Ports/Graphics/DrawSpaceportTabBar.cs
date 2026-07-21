using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000d004 (EV Override-11.c 6770-6868) — draws the spaceport hub's 7-button
// tab bar. Tab i ({normal,pressed} PICT pair SpaceportGlobals.TabPicts[i*2 /
// i*2+1]) draws into its DITL item rect:
//   0 leave(12)  1 refuel(4)  2 trade(7)  3 outfitter(8)  4 shipyard(9)
//   5 mission BBS (item 10 when the spob has a bar, else 13)  6 bar(11)
// selectedTab draws pressed art (-8 = none). Visibility: leave always; refuel
// when credits>0, spob inhabited and fuel below max; trade/outfitter/shipyard/bar
// by spob service flags; BBS by SpaceportGlobals.MissionBbsEnabled.
public static class DrawSpaceportTabBar
{
    public static void Run(short selectedTab)
    {
        var tabRects = new short[7][];
        for (int i = 0; i < 7; i++) tabRects[i] = new short[4];
        var tabVisible = new byte[7];

        int window = SpaceportGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 12, null, null, tabRects[0]);
        MacToolbox.GetDialogItem(window, 4, null, null, tabRects[1]);
        MacToolbox.GetDialogItem(window, 7, null, null, tabRects[2]);
        MacToolbox.GetDialogItem(window, 8, null, null, tabRects[3]);
        MacToolbox.GetDialogItem(window, 9, null, null, tabRects[4]);
        var spobFlags = (SpobFlags)CurrentSpob.Rec.Flags;
        MacToolbox.GetDialogItem(window, (spobFlags & SpobFlags.Bar) == 0 ? 13 : 10, null, null, tabRects[5]);
        MacToolbox.GetDialogItem(window, 11, null, null, tabRects[6]);

        tabVisible[0] = 1;
        tabVisible[1] = 0;
        if (Core.Model.GameData.Ships[0].Credits > 0 && (spobFlags & SpobFlags.Uninhabited) == 0)
        {
            short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player);
            if (Core.Model.GameData.Ships[0].Fuel < fuelMax)
            {
                tabVisible[1] = 1;
            }
        }
        tabVisible[2] = (byte)((spobFlags & SpobFlags.Exchange) != 0 ? 1 : 0);
        tabVisible[3] = (byte)((spobFlags & SpobFlags.Outfitter) != 0 ? 1 : 0);
        tabVisible[4] = (byte)((spobFlags & SpobFlags.Shipyard) != 0 ? 1 : 0);
        tabVisible[5] = SpaceportGlobals.MissionBbsEnabled;
        tabVisible[6] = (byte)((spobFlags & SpobFlags.Bar) != 0 ? 1 : 0);

        for (short tab = 0; tab < 7; tab++)
        {
            if (tabVisible[tab] != 0)
            {
                bool pressed = tab == selectedTab;
                MacToolbox.DrawPicture(SpaceportGlobals.TabPicts[tab * 2 + (pressed ? 1 : 0)], tabRects[tab]);
            }
        }
    }
}
