using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1000c244 (EV Override-11.c lines 6316-6424) — hit-test + press-track
// the shipyard's 5-button row: 0 = leave (item 7), 1 = buy (item 1, gated
// on BuyEnabled), 2 = specs (item 10, needs a selection), 3 = scroll-up
// (item 12, gated on FirstVisibleRow > 3), 4 = scroll-down (item 13,
// gated on more rows below the page). While the button stays down it
// re-renders the row whenever the hovered button changes; returns the
// button index under the mouse (-1 = none).
// AvailableRowIndex (was the raw BSS short[] at 0x1008f87a) is managed here.
public static class TrackShipyardButtonHit
{
    public static int Run(int mousePt)
    {
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var leaveRect = new short[4];
        var buyRect = new short[4];
        var specsRect = new short[4];
        var upRect = new short[4];
        var downRect = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 7, itemKind, itemHandle, leaveRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 1, itemKind, itemHandle, buyRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 10, itemKind, itemHandle, specsRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 12, itemKind, itemHandle, upRect);
        MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 13, itemKind, itemHandle, downRect);
        int selectedItem = -1;
        if (MacToolbox.PtInRect(mousePt, buyRect) && ShipyardState.BuyEnabled != 0)
        {
            selectedItem = 1;
        }
        if (MacToolbox.PtInRect(mousePt, leaveRect))
        {
            selectedItem = 0;
        }
        if (MacToolbox.PtInRect(mousePt, specsRect) && ShipyardState.SelectedRow != -1)
        {
            selectedItem = 2;
        }
        if (MacToolbox.PtInRect(mousePt, upRect) && 3 < ShipyardState.FirstVisibleRow)
        {
            selectedItem = 3;
        }
        if (MacToolbox.PtInRect(mousePt, downRect) && MoreRowsBelowPage())
        {
            selectedItem = 4;
        }
        if ((short)selectedItem != -1)
        {
            RenderShipyardButtonRow.Run((short)selectedItem);
            while (MacToolbox.StillDown())
            {
                int mouse = MacToolbox.GetMouse();
                int hoverItem = -1;
                if (MacToolbox.PtInRect(mouse, buyRect) && ShipyardState.BuyEnabled != 0)
                {
                    hoverItem = 1;
                }
                if (MacToolbox.PtInRect(mouse, leaveRect))
                {
                    hoverItem = 0;
                }
                if (MacToolbox.PtInRect(mouse, specsRect) && ShipyardState.SelectedRow != -1)
                {
                    hoverItem = 2;
                }
                if (MacToolbox.PtInRect(mouse, upRect) && 3 < ShipyardState.FirstVisibleRow)
                {
                    hoverItem = 3;
                }
                if (MacToolbox.PtInRect(mouse, downRect) && MoreRowsBelowPage())
                {
                    hoverItem = 4;
                }
                short prevItem = (short)selectedItem;
                selectedItem = hoverItem;
                if ((short)hoverItem != prevItem)
                {
                    RenderShipyardButtonRow.Run((short)hoverItem);
                }
            }
        }
        return selectedItem;
    }

    // FUN_1000c244 6389-6404 — true when the shipyard grid has more available rows
    // than fit below FirstVisibleRow (the scroll-down button is live). The decompile
    // repeats this exact scan+compare twice (initial hit-test + hover loop); both
    // call sites here share this one helper instead.
    private static bool MoreRowsBelowPage()
    {
        short filledCount = 0;
        for (short row = 0; row < ShipyardState.ButtonRowScanLimit; row = (short)(row + 1))
        {
            if (ShipyardState.AvailableRowIndex[row] != -1)
            {
                filledCount = (short)(filledCount + 1);
            }
        }
        return ShipyardState.FirstVisibleRow < filledCount - 20;
    }
}
