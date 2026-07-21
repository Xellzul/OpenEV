// Port of FUN_1000ca70 (EV Override-11.c lines 6603-6667).

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

public static class Render5OutfitButtonRow
{
    public static void Run(short selectedButton)
    {
        // btnRects[0..4] = dialog items 1,7,4,10,11 (leave/buy/sell/scroll-up/scroll-down,
        // row order); each needs a real short[4] rect out-param for GetDialogItem.
        short[][] btnRects = { new short[4], new short[4], new short[4], new short[4], new short[4] };
        byte[] btnEnabled = new byte[6];   // per-button enabled flag (1 = draw, 0 = paint over)

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 1, null, null, btnRects[0]);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, null, null, btnRects[1]);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, null, null, btnRects[2]);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 10, null, null, btnRects[3]);
        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 11, null, null, btnRects[4]);
        for (short i = 0; i < btnRects.Length; i = (short)(i + 1))
        {
            btnEnabled[i] = 1;
        }
        if (ShipyardState.BuyEnabled == 0)
        {
            btnEnabled[1] = 0;
        }
        if (OutfitShopState.SellEnabled == 0)
        {
            btnEnabled[2] = 0;
        }
        if (OutfitShopState.FirstVisibleRow < 4)
        {
            btnEnabled[3] = 0;
        }
        short availableCount = 0;
        for (short scanIndex = 0; scanIndex < OutfitShopState.RowCount; scanIndex = (short)(scanIndex + 1))
        {
            if (OutfitShopState.AvailableRowIndex[scanIndex] != -1)
            {
                availableCount = (short)(availableCount + 1);
            }
        }
        if (availableCount - 20 <= OutfitShopState.FirstVisibleRow)
        {
            btnEnabled[4] = 0;
        }
        for (short i = 0; i < btnRects.Length; i = (short)(i + 1))
        {
            if (btnEnabled[i] == 0)
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(btnRects[i]);
            }
            else if (selectedButton == i)
            {
                MacToolbox.DrawPicture(OutfitShopState.Picts[i * 2 + 1], btnRects[i]);   // highlighted icon
            }
            else
            {
                MacToolbox.DrawPicture(OutfitShopState.Picts[i * 2], btnRects[i]);   // normal icon
            }
        }
    }
}
