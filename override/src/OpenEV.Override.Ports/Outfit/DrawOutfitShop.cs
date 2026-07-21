using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Outfit;

// FUN_100396bc — the OUTFITTER (AdvanceLoadout, DLOG 0x3ea) dialog redraw
// (EV Override-11.c lines 23530-23756). Draws the buy-multiple readout
// (item 3), the 4×5 outfit grid (item 5), the selected outfit's picture +
// stats (items 8/9), the description text (item 6) and the 5-button row
// into the BACKDROP GWorld, then CopyBits the lot onto the dialog window.
//
// Dialog 4-rules rewrite: string staging (auStack_178) is C# strings;
// GetDialogItem outs are managed arrays; win+0x10/+0x18 go through
// GetDialogPortRect/GetDialogVisRgn; state through the managed homes.
// The grid Rect arrays + icon-strip sheet stay raw heap (BOUNDARY — owned
// by LayoutShopGridAndIconStrip), as does the description-text
// C-string buffer (*0x10081020, walked in place by LoadDescriptionText).
public static class DrawOutfitShop
{
    private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    public static void Run()
    {
        int window = OutfitShopState.DialogWindow;
        var itemKind = new short[1];   // auStack_6a
        var itemHandle = new int[1];     // auStack_74
        var itemRect = new short[4];   // local_54/52/50/4e {top,left,bottom,right}

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // ── Item 3: "×N" buy/sell-multiple readout ────────────────────
        MacToolbox.GetDialogItem(window, 3, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            if (OutfitShopState.SelectedRow != -1)
            {
                short multiple = 1;   // local_6c
                // FAITHFUL: decompile tests keymap bits 0x32 (×5) and 0x3f (×10). Those are EVO
                // keymap-bit space (real-ADB keycode ^ 8), so the physical keys are 0x32^8 = 0x3A
                // = Option and 0x3f^8 = 0x37 = Command — reading the raw literals as Grave /
                // kVK_Function is the ^8 trap. TestLiveKeymapBit's MacKeycode overload re-applies
                // ^8, so Option/Command reproduce the decompile bit-for-bit; neither is a rebind
                // or stand-in. See OutfitShopFilter.cs / FindNextShipSlot.cs / Keymap.cs.
                if (Keymap.TestLiveKeymapBit(MacKeycode.Option) != 0)   // Option(Win) ×5
                {
                    multiple = (short)(multiple * 5);
                }
                if (Keymap.TestLiveKeymapBit(MacKeycode.Command) != 0)   // ×10
                {
                    multiple = (short)(multiple * 10);
                }
                if (1 < multiple &&
                   (ShipyardState.BuyEnabled != 0 || OutfitShopState.SellEnabled != 0))
                {
                    MacToolbox.TextFont(3);
                    MacToolbox.TextSize(9);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(itemRect[1], itemRect[2]);
                    MacToolbox.DrawString("x");   // GameToc data-seg Pascal str 0x10081fa8 (PEF dump)
                    MacToolbox.DrawString(multiple.ToString());   // NumToString + DrawString
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
            }
        }
        // ── Item 5: the 4×5 outfit grid (icons + counts + names) ──────
        MacToolbox.GetDialogItem(window, 5, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            string[] nameTable = ResourceGlobals.NamesStr5000;   // STR# 5000 outfit-name table
            short drawnCount = 0;   // sVar16
            for (short row = OutfitShopState.FirstVisibleRow; row < OutfitShopState.RowCount; row = (short)(row + 1))
            {
                int cellIdx = row - OutfitShopState.FirstVisibleRow;
                // DEVIATION (faithful): the decompile reads a cell Rect at (heap base +
                // cellIdx*8) for every row up to 127, even past the real 20-entry (4x5)
                // heap block — on real Mac hardware this silently reads OOB garbage, which
                // RectInRgn always rejects (drawnCount caps real draws at 20 regardless).
                // The managed CellRects array is sized exactly 20 and throws instead of
                // reading garbage, so stop the scan at the array's real length — same fix
                // as RedrawShipyardDialog.cs's identical grid-scan crash.
                if (cellIdx >= GridLayout.CellRects.Length) break;
                var cellRect = GridLayout.CellRects[cellIdx];
                if (MacToolbox.RectInRgn(cellRect, MacToolbox.GetDialogVisRgn(window)) && drawnCount < GridLayout.CellCount &&
                    OutfitShopState.AvailableRowIndex[row] != -1)
                {
                    short outfitIdx = OutfitShopState.AvailableRowIndex[row];
                    // CopyBits(*(ctx+0x38)+2, iRam1008f6ec+2, iconSrc[outfitIdx], iconDst[cell], 0, 0).
                    MacToolbox.CopyBits(GlobalState.AnimScratchPort + 2, RenderGlobals.BackdropGWorld + 2,
                                       GridLayout.IconStripRects[outfitIdx],
                                       GridLayout.IconCellRects[cellIdx], 0, 0);
                    if (0 < OwnedOutfitGrid.Store[outfitIdx])
                    {
                        string countText = OwnedOutfitGrid.Store[outfitIdx].ToString();
                        MacToolbox.ForeColor(QuickDrawColor.White);
                        MacToolbox.MoveTo(cellRect[3] - (MacToolbox.StringWidth(countText) + 3),
                                          cellRect[0] + 12);
                        MacToolbox.DrawString(countText);
                        MacToolbox.ForeColor(QuickDrawColor.Black);
                    }
                    // FUN_10076178(buf, names + idx*0x100, 0x13): Pascal copy, no p2cstr —
                    // DrawString/StringWidth read it as Pascal (19-cap, family convention).
                    string name = Trunc(nameTable[outfitIdx], 19);
                    // Centring: (left+right)/2 − width/2. C# int division reproduces the
                    // decompile's signed >>1+addze idiom exactly — do NOT simplify back to
                    // a bare >>1 (diverges for negative operands).
                    int center = cellRect[1] + cellRect[3];
                    MacToolbox.MoveTo(center / 2 - MacToolbox.StringWidth(name) / 2,
                                      cellRect[2] - 6);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.DrawString(name);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.RGBForeColor((uint)UiColors.OutfitFrame);
                    MacToolbox.FrameRect(cellRect);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    drawnCount = (short)(drawnCount + 1);
                }
            }
            if (OutfitShopState.SelectedSlot != -1)
            {
                MacToolbox.RGBForeColor((uint)UiColors.Neutral);
                MacToolbox.FrameRect(GridLayout.CellRects[OutfitShopState.SelectedSlot]);
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        if (OutfitShopState.SelectedSlot != -1)
        {
            // ── Item 8: selected outfit's picture ─────────────────────
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
                    // GameToc-0x47f6 = 0x10083e6a "No Picture", GameToc-0x47eb = 0x10083e75
                    // "Available" (dumped in a prior session) → C# literals.
                    DrawCenteredString.Run("No Picture", itemRect[1], itemRect[3], (short)(itemRect[0] + 44));
                    DrawCenteredString.Run("Available", itemRect[1], itemRect[3], (short)(itemRect[0] + 56));
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
                else
                {
                    MacToolbox.DrawPicture(ShipyardState.SelectedShipPict, itemRect);
                }
            }
            // ── Item 9: selected outfit's stats ───────────────────────
            MacToolbox.GetDialogItem(window, 9, itemKind, itemHandle, itemRect);
            if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
            {
                var outfit = OutfitTable.Store[OutfitShopState.SelectedRow];
                var player = GameData.Player;
                MacToolbox.TextFont(3);
                MacToolbox.TextSize(9);
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                MacToolbox.DrawString("Item Price:");   // GameToc data-seg Pascal str 0x10083e7f (PEF dump)
                MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 12);
                // PriceQuantize's spob-ptr arg (3rd) is unused by the body — 0 here is safe
                // (see PriceQuantize.cs).
                int price = PriceQuantize.Run((int)SpaceportGlobals.ShopPriceScale[0],
                                               outfit.Cost, 0, outfit.TechLevel,
                                               GameData.Spobs[player.NavTargetSpob].TechLevel);
                FormatCredits.Run(price);
                MacToolbox.DrawString(" cr");   // GameToc data-seg Pascal str 0x10081faa (PEF dump)
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 24);
                MacToolbox.DrawString("You Have:");   // GameToc data-seg Pascal str 0x10083e8b (PEF dump)
                MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 24);
                FormatCredits.Run(player.Credits);
                MacToolbox.DrawString(" cr");   // GameToc data-seg Pascal str 0x10081faa (PEF dump)
                if (0 < outfit.Mass)
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 48);
                    MacToolbox.DrawString("Item Mass:");   // GameToc data-seg Pascal str 0x10083e95 (PEF dump)
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 48);
                    short tons = outfit.Mass;
                    FormatCredits.Run(tons);
                    MacToolbox.DrawString(" ton");   // GameToc data-seg Pascal str 0x10083ea0 (PEF dump)
                    if (tons != 1)
                    {
                        MacToolbox.DrawString("s");   // GameToc data-seg Pascal str 0x10081fae (PEF dump)
                    }
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 60);
                    MacToolbox.DrawString("Available:");   // GameToc data-seg Pascal str 0x10083ea5 (PEF dump)
                    MacToolbox.MoveTo(itemRect[1] + 70, itemRect[0] + 60);
                    tons = (short)ShipDerivedStats.FreeMassSpace();
                    if (tons < 0)
                    {
                        tons = 0;
                    }
                    FormatCredits.Run(tons);
                    MacToolbox.DrawString(" ton");   // GameToc data-seg Pascal str 0x10083ea0 (PEF dump)
                    if (tons != 1)
                    {
                        MacToolbox.DrawString("s");   // GameToc data-seg Pascal str 0x10081fae (PEF dump)
                    }
                }
                if (CannotBuyOutfit.Run(OutfitShopState.SelectedRow) == 0)
                {
                    if (0 < outfit.Mass)
                    {
                        short freeMass = (short)ShipDerivedStats.FreeMassSpace();
                        if (freeMass < outfit.Mass)
                        {
                            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 96);
                            if (OwnedOutfitGrid.Store[OutfitShopState.SelectedRow] < 1)
                            {
                                MacToolbox.DrawString("Can’t hold any of this item!");   // GameToc data-seg Pascal str 0x10083ef7 (PEF dump)
                            }
                            else
                            {
                                MacToolbox.DrawString("Can’t hold any more!");   // GameToc data-seg Pascal str 0x10083ee2 (PEF dump)
                            }
                        }
                    }
                }
                else
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 96);
                    if (OwnedOutfitGrid.Store[OutfitShopState.SelectedRow] < 1)
                    {
                        MacToolbox.DrawString("Can’t have any of this item!");   // GameToc data-seg Pascal str 0x10083ec5 (PEF dump)
                    }
                    else
                    {
                        MacToolbox.DrawString("Can’t have any more!");   // GameToc data-seg Pascal str 0x10083eb0 (PEF dump)
                    }
                }
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        // ── Item 6: description text ──────────────────────────────────
        MacToolbox.GetDialogItem(window, 6, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.TETextBox(OutfitDescText.Text, itemRect, 0);
            MacToolbox.InvertRect(itemRect);
        }
        Render5OutfitButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        // CopyBits(iRam1008f6ec+2, win+2, win+0x10, win+0x10, 0, *(win+0x18)).
        var portRect = MacToolbox.GetDialogPortRect(window);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, window + 2,
                            portRect, portRect, 0, MacToolbox.GetDialogVisRgn(window));
        return;
    }
}
