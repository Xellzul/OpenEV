using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;

namespace OpenEV.Override.Ports.Outfit;

// FUN_10035ddc (EV Override-11.c lines 22110-22461) — the spaceport COMMODITY
// EXCHANGE / TRADE dialog redraw (DLOG 0x3e9, shown by
// ShowCommodityExchangeDialog; renamed from the Pass-1 mislabel
// "DrawOutfitterDialog" — this draws the hub's Trade tab, not the separate
// Outfitter dialog, DLOG 0x3ea). Draws the cargo-bay header (item 3), the 8
// commodity/junk rows (items 4..11), the cargo-summary blurb (item 12), the
// spob picture + buy/sell pict buttons (items 1/13/14), and the cron
// special-cargo blurb (item 15) into the BACKDROP GWorld, then CopyBits the
// lot onto the dialog.
public static class DrawCommodityTradeDialog
{
    private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    public static void Run()
    {
        int window = TradeGlobals.DialogWindow;
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];   // {top, left, bottom, right}

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);

        // ── Item 3: cargo-bay header strip ────────────────────────────
        MacToolbox.GetDialogItem(window, 3, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
            MacToolbox.MoveTo(itemRect[1] + 6, itemRect[0] + 12);
            MacToolbox.DrawString("Commodity:");
            MacToolbox.MoveTo(itemRect[1] + 77, itemRect[0] + 12);
            short cargoMax = (short)ShipDerivedStats.EffectiveCargoMax();
            short cargoUsed = (short)TotalMassWithEscorts.Run();
            if (cargoMax < cargoUsed)
            {
                MacToolbox.DrawString("In Fleet:");
            }
            else
            {
                MacToolbox.DrawString("In Hold:");
            }
            MacToolbox.MoveTo(itemRect[1] + 127, itemRect[0] + 12);
            MacToolbox.DrawString("Price:");
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        // ── Items 4..11: the 6 commodity rows + 2 junk-slot rows ──────
        for (short row = 0; row < CommodityPricing.FinalPrice.Length; row = (short)(row + 1))
        {
            MacToolbox.GetDialogItem(window, row + 4, itemKind, itemHandle, itemRect);
            if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
            {
                if (row == WorldState.TradeCurrentTab)
                {
                    var fillRect = new short[] { itemRect[0], itemRect[1], itemRect[2], itemRect[3] };
                    MacToolbox.InsetRect(fillRect, 1, 1);
                    MacToolbox.RGBForeColor(UiColorConstants.CommodityStockFillGreen);
                    MacToolbox.PaintRect(fillRect);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                }
                MacToolbox.RGBForeColor((uint)UiColors.Frame);
                MacToolbox.MoveTo(itemRect[1], itemRect[0]);
                MacToolbox.LineTo(itemRect[1], itemRect[2] + -1);
                MacToolbox.MoveTo(itemRect[3] + -1, itemRect[0]);
                MacToolbox.LineTo(itemRect[3] + -1, itemRect[2] + -1);
                if (row == 0)
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[0]);
                    MacToolbox.LineTo(itemRect[3] + -1, itemRect[0]);
                }
                if (row == 7)
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[2] + -1);
                    MacToolbox.LineTo(itemRect[3] + -1, itemRect[2] + -1);
                }
                if (CommodityPricing.FinalPrice[row] != 0)
                {
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    if (row < 6)
                    {
                        MacToolbox.MoveTo(itemRect[1] + 6, itemRect[0] + 10);
                        MacToolbox.DrawString(ResourceGlobals.NamesStr4000[row]);
                        if (0 < GameData.Player.CargoHold[row])
                        {
                            string qtyText = ((int)GameData.Player.CargoHold[row]).ToString();
                            MacToolbox.MoveTo((itemRect[1] + 100) - MacToolbox.StringWidth(qtyText), itemRect[0] + 10);
                            MacToolbox.DrawString(qtyText);
                        }
                    }
                    else if (WeaponSlotOutfitMap.Store[row - 6] != -1)
                    {
                        MacToolbox.MoveTo(itemRect[1] + 6, itemRect[0] + 10);
                        MacToolbox.DrawString(GameData.Junk[WeaponSlotOutfitMap.Store[row - 6]].Name);
                        if (0 < GameData.Junk[WeaponSlotOutfitMap.Store[row - 6]].PlayerQty)
                        {
                            string qtyText = ((int)GameData.Junk[WeaponSlotOutfitMap.Store[row - 6]].PlayerQty).ToString();
                            MacToolbox.MoveTo((itemRect[1] + 100) - MacToolbox.StringWidth(qtyText), itemRect[0] + 10);
                            MacToolbox.DrawString(qtyText);
                        }
                    }
                    MacToolbox.MoveTo(itemRect[1] + 127, itemRect[0] + 10);
                    bool cronMatched = false;
                    foreach (var cron in GameData.Crons)
                    {
                        if (0 < cron.StateCountdown &&
                            GameData.Player.NavTargetSpob == cron.ChosenSpob &&
                            row == cron.Commodity)
                        {
                            cronMatched = true;
                            if (0 < cron.PriceDelta)
                            {
                                MacToolbox.DrawString("Higher");
                            }
                            if (cron.PriceDelta < 0)
                            {
                                MacToolbox.DrawString("Lower");
                            }
                            break;
                        }
                    }
                    if (!cronMatched)
                    {
                        if (row < 6)
                        {
                            if (CommodityPricing.PriceMode[row] == 1)
                            {
                                MacToolbox.DrawString("Low");
                            }
                            if (CommodityPricing.PriceMode[row] == 2)
                            {
                                MacToolbox.DrawString("Med");
                            }
                            if (CommodityPricing.PriceMode[row] == 4)
                            {
                                MacToolbox.DrawString("High");
                            }
                        }
                        else if (WeaponSlotOutfitMap.Store[row - 6] != -1)
                        {
                            if (row == 6)
                            {
                                MacToolbox.DrawString("High");
                            }
                            if (row == 7)
                            {
                                MacToolbox.DrawString("Low");
                            }
                        }
                    }
                    string priceText = ((int)CommodityPricing.FinalPrice[row]).ToString();
                    MacToolbox.MoveTo((itemRect[1] + 190) - MacToolbox.StringWidth(priceText), itemRect[0] + 10);
                    MacToolbox.DrawString(priceText);
                }
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        // ── Item 12: "You are carrying …" cargo-summary blurb ─────────
        MacToolbox.GetDialogItem(window, 12, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            // decompile declares nameSeen as 65 bytes; only indices < NamesStr0fa1.Length
            // (64, the CargoStringIndex range) are ever touched.
            var nameSeen = new byte[ResourceGlobals.NamesStr0fa1.Length];
            var cargoSeen = new byte[GameData.Junk.Length];
            short entryCount = 0;
            short distinctCount = 0;
            short missionTons = 0;
            short junkTons = 0;
            short missionCount = 0;
            short junkCount = 0;
            for (short i = 0; i < nameSeen.Length; i = (short)(i + 1))
            {
                nameSeen[i] = 0;
            }
            for (short i = 0; i < cargoSeen.Length; i = (short)(i + 1))
            {
                cargoSeen[i] = 0;
            }
            foreach (var slot in WeaponSlotOutfitMap.Store)
            {
                if (slot != -1)
                {
                    cargoSeen[slot] = 1;
                }
            }
            for (short i = 0; i < GameData.Missions.Length; i = (short)(i + 1))
            {
                if (GameData.MissionStates[i].IsActive != 0 &&
                    GameData.Missions[i].CargoPickedUp != 0 &&
                    GameData.Missions[i].CargoStringIndex != -1)
                {
                    entryCount = (short)(entryCount + 1);
                    missionCount = (short)(missionCount + 1);
                    missionTons = (short)(missionTons + GameData.Missions[i].CargoMass);
                    if (nameSeen[GameData.Missions[i].CargoStringIndex] == 0)
                    {
                        nameSeen[GameData.Missions[i].CargoStringIndex] = 1;
                        distinctCount = (short)(distinctCount + 1);
                    }
                }
            }
            for (short i = 0; i < GameData.Junk.Length; i = (short)(i + 1))
            {
                if (0 < GameData.Junk[i].PlayerQty &&
                    i != WeaponSlotOutfitMap.Store[0] &&
                    i != WeaponSlotOutfitMap.Store[1])
                {
                    entryCount = (short)(entryCount + 1);
                    junkTons = (short)(junkTons + GameData.Junk[i].PlayerQty);
                    junkCount = (short)(junkCount + 1);
                    distinctCount = (short)(distinctCount + 1);
                }
            }
            // NOTE: junkTons is summed but never read again — dead store in the
            // decompile too; kept for the faithful counters.
            _ = junkTons;
            if (0 < entryCount)
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                var insetRect = new short[] { itemRect[0], itemRect[1], itemRect[2], itemRect[3] };
                MacToolbox.InsetRect(insetRect, 5, 3);
                short emitted = 0;
                string text = Trunc("Other cargo: ", 20);
                if (0 < missionCount)
                {
                    if (0 < missionTons)
                    {
                        text += ((int)missionTons).ToString();
                        if (missionTons == 1)
                        {
                            text += " ton";
                        }
                        else
                        {
                            text += " tons";
                        }
                        text += " of ";
                    }
                    for (short i = 0; i < nameSeen.Length; i = (short)(i + 1))
                    {
                        nameSeen[i] = 0;
                    }
                    for (short i = 0; i < GameData.Missions.Length; i = (short)(i + 1))
                    {
                        if (GameData.MissionStates[i].IsActive != 0 &&
                            GameData.Missions[i].CargoPickedUp != 0 &&
                            GameData.Missions[i].CargoStringIndex != -1 &&
                            nameSeen[GameData.Missions[i].CargoStringIndex] == 0)
                        {
                            nameSeen[GameData.Missions[i].CargoStringIndex] = 1;
                            text += Trunc(ResourceGlobals.NamesStr0fa1[GameData.Missions[i].CargoStringIndex], 19);
                            if (emitted < distinctCount - 2)
                            {
                                text += ", ";
                            }
                            if (emitted == distinctCount - 2)
                            {
                                if (2 < distinctCount)
                                {
                                    text += ",";
                                }
                                text += " and ";
                            }
                            emitted = (short)(emitted + 1);
                        }
                    }
                }
                if (0 < junkCount)
                {
                    for (short i = 0; i < GameData.Junk.Length; i = (short)(i + 1))
                    {
                        if (0 < GameData.Junk[i].PlayerQty && cargoSeen[i] == 0 &&
                            i != WeaponSlotOutfitMap.Store[0] && i != WeaponSlotOutfitMap.Store[1])
                        {
                            text += Trunc(ResourceGlobals.NamesStr0fa6[i], 19);
                            if (emitted < distinctCount - 2)
                            {
                                text += ", ";
                            }
                            if (emitted == distinctCount - 2)
                            {
                                if (2 < distinctCount)
                                {
                                    text += ",";
                                }
                                text += " and ";
                            }
                            emitted = (short)(emitted + 1);
                        }
                    }
                }
                MacToolbox.TETextBox(text, insetRect, 0);
                MacToolbox.InvertRect(insetRect);
                MacToolbox.RGBForeColor((uint)UiColors.Frame);
                MacToolbox.FrameRect(itemRect);
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        // ── Item 1: spob picture ──────────────────────────────────────
        MacToolbox.GetDialogItem(window, 1, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.DrawPicture(TradeGlobals.Picts[0], itemRect);
        }
        // ── Item 13: buy pict button (blanked when can't buy) ─────────
        MacToolbox.GetDialogItem(window, 13, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            if (CanBuyCommodity.Run(WorldState.TradeCurrentTab) == 0)
            {
                MacToolbox.PaintRect(itemRect);
            }
            else
            {
                MacToolbox.DrawPicture(TradeGlobals.Picts[2], itemRect);
            }
        }
        // ── Item 14: sell pict button (blanked when nothing to sell) ──
        MacToolbox.GetDialogItem(window, 14, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            if (!HasWeaponInSlot.Run(WorldState.TradeCurrentTab))
            {
                MacToolbox.PaintRect(itemRect);
            }
            else
            {
                MacToolbox.DrawPicture(TradeGlobals.Picts[4], itemRect);
            }
        }
        // ── Item 15: cron special-cargo blurb ─────────────────────────
        MacToolbox.GetDialogItem(window, 15, itemKind, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            foreach (var cron in GameData.Crons)
            {
                if (0 < cron.StateCountdown && GameData.Player.NavTargetSpob == cron.ChosenSpob)
                {
                    string text = Trunc(cron.Name, 63);
                    text += " has ";
                    if (cron.PriceDelta < 1)
                    {
                        text += "lowered";
                    }
                    else
                    {
                        text += "raised";
                    }
                    text += " the price of ";
                    text += Trunc(ResourceGlobals.NamesStr0fa1[cron.Commodity], 19);
                    text += ".";
                    text = Trunc(text, 250);
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.TextFont(3);
                    MacToolbox.TextSize(9);
                    MacToolbox.TETextBox(text, itemRect, 0);
                    MacToolbox.InvertRect(itemRect);
                    break;
                }
            }
        }
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        var portRect = MacToolbox.GetDialogPortRect(window);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, window + 2,
                            portRect, portRect, 0, MacToolbox.GetDialogVisRgn(window));
    }
}
