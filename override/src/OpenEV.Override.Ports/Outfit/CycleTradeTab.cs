using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_10035c34 (EV Override-11.c lines 22054-22109) — cycles the Commodity
// Exchange dialog's selected row (WorldState.TradeCurrentTab), skipping zero-price
// rows, then invalidates the buy/sell buttons (items 13/14) and the union of the
// cargo-bay header (item 3) with the cargo-summary blurb (item 12).
public static class CycleTradeTab
{
    public static void Run(byte cycleBackward)
    {
        AdvanceTab(cycleBackward);
        while (CommodityPricing.FinalPrice[WorldState.TradeCurrentTab] == 0)
        {
            AdvanceTab(cycleBackward);
        }

        short[] itemRect = new short[4];
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 13, 0, 0, itemRect);
        MacToolbox.InvalRect(itemRect);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 14, 0, 0, itemRect);
        MacToolbox.InvalRect(itemRect);

        // Union rect: item 3's top/left/right with item 12's bottom.
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 3, 0, 0, itemRect);
        short[] unionRect = { itemRect[0], itemRect[1], itemRect[2], itemRect[3] };
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 12, 0, 0, itemRect);
        unionRect[2] = itemRect[2];
        MacToolbox.InvalRect(unionRect);
        return;
    }

    // Step TradeCurrentTab by one row, wrapping at the row count (0..FinalPrice.Length-1).
    private static void AdvanceTab(byte cycleBackward)
    {
        if (cycleBackward == 0)
        {
            WorldState.TradeCurrentTab += 1;
        }
        else
        {
            WorldState.TradeCurrentTab -= 1;
        }
        if (WorldState.TradeCurrentTab >= CommodityPricing.FinalPrice.Length)
        {
            WorldState.TradeCurrentTab = 0;
        }
        if (WorldState.TradeCurrentTab < 0)
        {
            WorldState.TradeCurrentTab = (short)(CommodityPricing.FinalPrice.Length - 1);
        }
    }
}
