using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1000c7b4 (EV Override-11.c lines 6502-6602) — tracks a mouse-down over
// the OUTFITTER dialog's 5-button row (items 1=leave / 7=buy / 4=sell /
// 10=scroll-up / 11=scroll-down of DLOG 1002). While the button stays down
// it follows the mouse with the enable gates re-checked (buy =
// ShipyardState.BuyEnabled, sell = OutfitShopState.SellEnabled, scroll-up =
// FirstVisibleRow > 3, scroll-down = more available rows than fit on the
// 20-row page), redrawing via Render5OutfitButtonRow on every change.
// Returns the 0..4 row index or -1. Called by OutfitShopFilter.
public static class TrackOutfitButtonMouseDown
{
    public static int Run(int mousePoint)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var leaveRect = new short[4];
        var buyRect = new short[4];
        var sellRect = new short[4];
        var scrollUpRect = new short[4];
        var scrollDownRect = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 1, itemType, itemHandle, leaveRect);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, itemType, itemHandle, buyRect);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, itemType, itemHandle, sellRect);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 10, itemType, itemHandle, scrollUpRect);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 11, itemType, itemHandle, scrollDownRect);
        int selectedButton = -1;
        if (MacToolbox.PtInRect(mousePoint, leaveRect))
        {
            selectedButton = 0;
        }
        if (MacToolbox.PtInRect(mousePoint, buyRect))
        {
            selectedButton = 1;
        }
        if (MacToolbox.PtInRect(mousePoint, sellRect))
        {
            selectedButton = 2;
        }
        if (MacToolbox.PtInRect(mousePoint, scrollUpRect))
        {
            selectedButton = 3;
        }
        if (MacToolbox.PtInRect(mousePoint, scrollDownRect))
        {
            selectedButton = 4;
        }
        if ((short)selectedButton != -1)
        {
            Render5OutfitButtonRow.Run((short)selectedButton);
            while (MacToolbox.StillDown())
            {
                int livePoint = MacToolbox.GetMouse();
                int currentButton = -1;
                if (MacToolbox.PtInRect(livePoint, leaveRect))
                {
                    currentButton = 0;
                }
                if (MacToolbox.PtInRect(livePoint, buyRect) && ShipyardState.BuyEnabled != 0)
                {
                    currentButton = 1;
                }
                if (MacToolbox.PtInRect(livePoint, sellRect) && OutfitShopState.SellEnabled != 0)
                {
                    currentButton = 2;
                }
                if (MacToolbox.PtInRect(livePoint, scrollUpRect) && 3 < OutfitShopState.FirstVisibleRow)
                {
                    currentButton = 3;
                }
                if (MacToolbox.PtInRect(livePoint, scrollDownRect))
                {
                    short availCount = 0;
                    for (short loopIndex = 0; loopIndex < OutfitShopState.RowCount; loopIndex = (short)(loopIndex + 1))
                    {
                        if (OutfitShopState.AvailableRowIndex[loopIndex] != -1)
                        {
                            availCount = (short)(availCount + 1);
                        }
                    }
                    if (OutfitShopState.FirstVisibleRow < availCount + -20)
                    {
                        currentButton = 4;
                    }
                }
                short prevButton = (short)selectedButton;
                selectedButton = currentButton;
                if ((short)currentButton != prevButton)
                {
                    Render5OutfitButtonRow.Run((short)currentButton);
                }
            }
        }
        return selectedButton;
    }
}
