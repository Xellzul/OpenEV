using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1000c56c (EV Override-11.c lines 6425-6501) — render the shipyard's
// 5-button row from ShipyardState.Picts: leave (item 7, [2]/[3] pressed),
// buy (item 1, [0]/[1] — BLANKED while BuyEnabled is 0), specs (item 10,
// [6]/[7]), scroll-up (item 12, [8]/[9] — blanked on the first page) and
// scroll-down (item 13, [10]/[11] — blanked when no rows below the page).
// selectedButton (0..4) picks which button draws its pressed PICT; -1 = none.
// AvailableRowIndex (was the raw BSS short[] at 0x1008f87a) is managed here.
public static class RenderShipyardButtonRow
{
    public static void Run(short selectedButton)
    {
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var leaveRect = new short[4];
        var buyRect = new short[4];
        var specsRect = new short[4];
        var upRect = new short[4];
        var downRect = new short[4];

        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 7, itemKind, itemHandle, leaveRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 1, itemKind, itemHandle, buyRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 10, itemKind, itemHandle, specsRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 12, itemKind, itemHandle, upRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 13, itemKind, itemHandle, downRect);
        if (selectedButton == 0)
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[3], leaveRect);
        }
        else
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[2], leaveRect);
        }
        if (ShipyardState.BuyEnabled == 0)
        {
            MacToolbox.PaintRect(buyRect);
        }
        else if (selectedButton == 1)
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[1], buyRect);
        }
        else
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[0], buyRect);
        }
        if (selectedButton == 2)
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[7], specsRect);
        }
        else
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[6], specsRect);
        }
        if (ShipyardState.FirstVisibleRow < 4)
        {
            MacToolbox.PaintRect(upRect);
        }
        else if (selectedButton == 3)
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[9], upRect);
        }
        else
        {
            MacToolbox.DrawPicture(ShipyardState.Picts[8], upRect);
        }
        short availCount = 0;
        for (short row = 0; row < ShipyardState.ButtonRowScanLimit; row = (short)(row + 1))
        {
            if (ShipyardState.AvailableRowIndex[row] != -1)
            {
                availCount = (short)(availCount + 1);
            }
        }
        if (ShipyardState.FirstVisibleRow < availCount - 20)
        {
            if (selectedButton == 4)
            {
                MacToolbox.DrawPicture(ShipyardState.Picts[11], downRect);
            }
            else
            {
                MacToolbox.DrawPicture(ShipyardState.Picts[10], downRect);
            }
        }
        else
        {
            MacToolbox.PaintRect(downRect);
        }
    }
}
