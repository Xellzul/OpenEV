using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1000d2e4 (EV Override-11.c lines 6874-6939) — tracks a mouse-down over
// the COMMODITY TRADE dialog's leave/buy/sell button row (items 1/13/14 of
// DLOG 1001): hit-tests the three button rects, then while the button stays
// down follows the mouse (buy gated on CanAffordCommodity, sell on the
// player holding some of the selected commodity), redrawing the pressed
// state via DrawTradeButtonRow on every change. Returns 0=leave, 1=buy,
// 2=sell, -1=none.
public static class TrackTradeButtonRow
{
    public static int Run(int clickPoint)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var leaveRect = new short[4];
        var buyRect = new short[4];
        var sellRect = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 1, itemType, itemHandle, leaveRect);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 13, itemType, itemHandle, buyRect);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 14, itemType, itemHandle, sellRect);
        int selected = -1;
        if (MacToolbox.PtInRect(clickPoint, leaveRect))
        {
            selected = 0;
        }
        if (MacToolbox.PtInRect(clickPoint, buyRect))
        {
            selected = 1;
        }
        if (MacToolbox.PtInRect(clickPoint, sellRect))
        {
            selected = 2;
        }
        if ((short)selected != -1)
        {
            DrawTradeButtonRow.Run((short)selected);
            while (MacToolbox.StillDown())
            {
                int livePoint = MacToolbox.GetMouse();
                int newSelected = -1;
                if (MacToolbox.PtInRect(livePoint, leaveRect))
                {
                    newSelected = 0;
                }
                if (MacToolbox.PtInRect(livePoint, buyRect) &&
                    CanBuyCommodity.Run(WorldState.TradeCurrentTab) != 0)
                {
                    newSelected = 1;
                }
                if (MacToolbox.PtInRect(livePoint, sellRect) &&
                    HasWeaponInSlot.Run(WorldState.TradeCurrentTab))
                {
                    newSelected = 2;
                }
                short prevSelected = (short)selected;
                selected = newSelected;
                if ((short)newSelected != prevSelected)
                {
                    DrawTradeButtonRow.Run((short)newSelected);
                }
            }
        }
        return selected;
    }
}
