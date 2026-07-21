using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1003bbd0 (EV Override-11.c lines 24517-24717) — the SHIPYARD
// (RunShipyardDialog, DLOG 0x3ec) dialog redraw. Draws the 4x5 ship grid
// (item 5), the selected ship's picture + price readout (items 8/9), the
// description text (item 6) and the 5-button row into the BACKDROP GWorld,
// then CopyBits the lot onto the dialog window.
//
// The grid Rect arrays + icon-strip sheet stay raw heap (BOUNDARY — owned by
// LayoutShopGridAndIconStrip), as does the description-text
// C-string buffer (*0x10081020, LoadDescriptionText fills it in place).
// The trade-in PriceQuantize call's 3rd arg is the spob TABLE base pointer
// in the decompile, but PriceQuantize.Run never reads that parameter (see
// PriceQuantize.cs) — passed as 0 here.
public static class RedrawShipyardDialog
{
    public static void Run()
    {
        int window = ShipyardState.DialogWindow;
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];   // {top,left,bottom,right}
        var player = GameData.Player;

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // ── Item 5: the 4×5 ship grid ─────────────────────────────────────
        MacToolbox.GetDialogItem(window, 5, itemKind, itemHandle, itemRect);
        itemRect[3] = (short)(itemRect[3] + 1);   // right + 1 — inflates only the visRgn test
        itemRect[2] = (short)(itemRect[2] + 1);   // bottom + 1
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            short drawnCount = 0;
            for (short row = ShipyardState.FirstVisibleRow; row < ShipClassTable.Count; row = (short)(row + 1))
            {
                // The visible grid holds only CellCount (20) cells. The decompile indexes the
                // 20-rect stack array with (row - FirstVisibleRow) and reads PAST it as harmless
                // garbage for rows beyond the window (RectInRgn rejected the garbage rect and
                // drawnCount capped draws at 20). The managed arrays are bounds-checked, so stop
                // at the grid edge — nothing is ever visible/drawn beyond it (cellIdx only grows).
                int cellIdx = (int)row - (int)ShipyardState.FirstVisibleRow;
                if (cellIdx >= GridLayout.CellRects.Length) break;
                var cellRect = GridLayout.CellRects[cellIdx];
                if (MacToolbox.RectInRgn(cellRect, MacToolbox.GetDialogVisRgn(window)) && drawnCount < GridLayout.CellCount &&
                    ShipyardState.AvailableRowIndex[row] != -1)
                {
                    short classIdx = ShipyardState.AvailableRowIndex[row];
                    MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, RenderGlobals.BackdropGWorld + 2,
                                        GridLayout.IconStripRects[classIdx],
                                        GridLayout.IconCellRects[cellIdx], 0, 0);
                    string name = ResourceGlobals.NamesStr1389[classIdx];   // STR# 0x1389 ship-class names
                    // Centring: (left+right)/2 − width/2; C# int division == the decompile's
                    // signed >>1 + odd-negative rounding dance (srawi+addze).
                    int center = (int)cellRect[1] + (int)cellRect[3];   // Rect {top@0,left@1,bottom@2,right@3}
                    MacToolbox.MoveTo(center / 2 - MacToolbox.StringWidth(name) / 2, cellRect[2] + -6);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.DrawString(name);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.RGBForeColor((uint)UiColors.OutfitFrame);
                    MacToolbox.FrameRect(cellRect);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    drawnCount = (short)(drawnCount + 1);
                }
            }
            if (ShipyardState.SelectedSlot != -1)
            {
                MacToolbox.RGBForeColor((uint)UiColors.Neutral);
                MacToolbox.FrameRect(GridLayout.CellRects[ShipyardState.SelectedSlot]);
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        if (ShipyardState.SelectedRow != -1)
        {
            // ── Item 8: selected ship's picture ─────────────────────────────
            MacToolbox.GetDialogItem(window, 8, itemKind, itemHandle, itemRect);
            if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
            {
                if (ShipyardState.SelectedShipPict == 0)
                {
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.PaintRect(itemRect);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.FrameRect(itemRect);
                    MacToolbox.TextFont(3);
                    MacToolbox.TextSize(9);
                    DrawCenteredString.Run("No Picture", itemRect[1], itemRect[3], (short)(itemRect[0] + 44));
                    DrawCenteredString.Run("Available", itemRect[1], itemRect[3], (short)(itemRect[0] + 56));
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
                else
                {
                    MacToolbox.DrawPicture(ShipyardState.SelectedShipPict, itemRect);
                }
            }
            // ── Item 9: the price readout ───────────────────────────────────
            MacToolbox.GetDialogItem(window, 9, itemKind, itemHandle, itemRect);
            if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
            {
                MacToolbox.TextFont(3);
                MacToolbox.TextSize(9);
                if (ShipyardState.EscortMode == 0 && ShipyardState.SelectedRow != player.ShipClass)
                {
                    var sel = GameData.ShipClasses[ShipyardState.SelectedRow];
                    short spobTech = GameData.Spobs[player.NavTargetSpob].TechLevel;
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                    MacToolbox.DrawString("Ship Price:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 12);
                    int spobOffset = player.NavTargetSpob * 0x48;
                    int shipPrice = PriceQuantize.Run((int)SpaceportGlobals.ShopPriceScale[1],
                                        sel.Cost, (short)spobOffset, sel.TechLevel, spobTech);
                    FormatCredits.Run(shipPrice);
                    MacToolbox.DrawString(" cr");
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 24);
                    MacToolbox.DrawString("Trade-In:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 24);
                    var owned = GameData.ShipClasses[player.ShipClass];
                    int tradeIn = (int)ComputeShipResaleValue.Run();
                    tradeIn = PriceQuantize.Run((int)SpaceportGlobals.ShopPriceScale[0], tradeIn,
                                  0, owned.TechLevel, spobTech);
                    spobOffset = player.NavTargetSpob * 0x48;
                    tradeIn = PriceQuantize.Run((int)SpaceportGlobals.ShopPriceScale[1], tradeIn,
                                  (short)spobOffset, owned.TechLevel, spobTech);
                    FormatCredits.Run(tradeIn);
                    MacToolbox.DrawString(" cr");
                    int priceDiff = shipPrice - tradeIn;
                    if (priceDiff < 0)
                    {
                        priceDiff = 0;
                    }
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 48);
                    MacToolbox.DrawString("Final Price:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 48);
                    FormatCredits.Run(priceDiff);
                    MacToolbox.DrawString(" cr");
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 72);
                    MacToolbox.DrawString("You Have:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 72);
                    FormatCredits.Run(player.Credits);
                    MacToolbox.DrawString(" cr");
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
                else if (ShipyardState.EscortMode != 0)
                {
                    var sel = GameData.ShipClasses[ShipyardState.SelectedRow];
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                    MacToolbox.DrawString("Hiring Price:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 12);
                    int spobOffset = player.NavTargetSpob * 0x48;
                    int hirePrice = PriceQuantize.Run((int)SpaceportGlobals.ShopPriceScale[1],
                                        sel.Cost, (short)spobOffset, sel.TechLevel,
                                        GameData.Spobs[player.NavTargetSpob].TechLevel);
                    // Hire price = 10% of the quantized ship price (0.1, dumped double; the
                    // decompile's longlong round-trip there is a dead store).
                    hirePrice = (int)(0.1 * hirePrice);
                    FormatCredits.Run(hirePrice);
                    MacToolbox.DrawString(" cr");
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 36);
                    MacToolbox.DrawString("You Have:");
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 36);
                    FormatCredits.Run(player.Credits);
                    MacToolbox.DrawString(" cr");
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
            }
        }
        // ── Item 6: description text ──────────────────────────────────────
        MacToolbox.GetDialogItem(window, 6, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.TETextBox(OutfitDescText.Text, itemRect, 0);
            MacToolbox.InvertRect(itemRect);
        }
        RenderShipyardButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        var portRect = MacToolbox.GetDialogPortRect(window);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, window + 2, portRect, portRect, 0, 0);
    }
}
