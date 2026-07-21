using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1000d4c0 (EV Override-11.c lines 6940-6991) — draws the COMMODITY TRADE
// dialog's leave/buy/sell button row (items 1/13/14 of DLOG 1001) from the
// TradeGlobals.Picts pairs (PICTs 7000..7005: [0]/[1] leave normal/pressed,
// [2]/[3] buy, [4]/[5] sell). pressedButton 0/1/2 selects which button draws
// pressed; a disabled buy (can't afford) or sell (none held) button is blanked
// with a black PaintRect.
public static class DrawTradeButtonRow
{
    public static void Run(short pressedButton)
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
        if (pressedButton == 0)
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[1], leaveRect);   // pressed
        }
        else
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[0], leaveRect);
        }
        var canAfford = CanBuyCommodity.Run(WorldState.TradeCurrentTab);
        if (canAfford == 0)
        {
            MacToolbox.PaintRect(buyRect);
        }
        else if (pressedButton == 1)
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[3], buyRect);   // pressed
        }
        else
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[2], buyRect);
        }
        var holdsAny = HasWeaponInSlot.Run(WorldState.TradeCurrentTab);
        if (!holdsAny)
        {
            MacToolbox.PaintRect(sellRect);
        }
        else if (pressedButton == 2)
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[5], sellRect);   // pressed
        }
        else
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[4], sellRect);
        }
    }
}
