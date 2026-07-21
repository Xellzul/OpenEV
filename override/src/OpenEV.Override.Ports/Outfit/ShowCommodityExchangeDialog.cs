using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Outfit;

// FUN_10034c20 (EV Override-11.c lines 21627-21915) — the spaceport COMMODITY
// EXCHANGE dialog (DLOG 0x3e9, opened from the spaceport hub's Trade tab).
// Derives the six standard commodity prices from the spob's price-mode bits
// (1 = cheap here: base ÷ multiplier, 2 = base, 4 = expensive: base ×
// multiplier; clamped >= 5; multiplier 1.25 / 1.1 in revolt / 1.5 once the
// spob is player-dominated — toc-0x65f0/-0x65f8/-0x6600 doubles, dumped),
// applies any active cron price event, resolves the spob's two junk
// commodities (tabs 6/7), then runs the modal loop: item 1 = leave, 2 = map,
// 16 = player info, 17 = missions, 13 = buy, 14 = sell (x10 per click, x100
// with the key 0x32 modifier held).
//
// Typed CommodityExchangeFilter registration, real short[]/int[] GetDialogItem
// outs, ModalDialog(ref short), and managed GetDialogPortRect for the window
// invalidations.
//
// ORIGINAL BUG (kept, bug-for-bug parity): on the sell path, qtyHeld/tradeQty
// mirror the decompile's untracked unaff_r16/unaff_r30 registers — when tab
// >= 6 with no junk mapping, they keep whatever value a previous branch last
// left in them (the original read uninitialized/stale registers there).
public static class ShowCommodityExchangeDialog
{
    // Bridges the modal-filter UPP (TradeGlobals.FilterProc -> FUN_1003579c) to
    // the typed CommodityExchangeFilter — adapts the short-itemHit/MacEvent shapes.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = CommodityExchangeFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    public static void Run()
    {
        int tmp;
        bool done;
        int modalFilterUpp;
        int pictHandle;
        short priceType;
        short loopIndex;
        short cargoHeadroom;   // buy side: TotalMassWithEscorts - TotalMassCarried
        short unitSize;        // sell side: 10/100 trade unit from the key modifier
        short tmpShort;
        short qtyHeld = default;    // unaff_r16 — persists across loop iterations (see header note)
        int tradeQty = default;     // unaff_r30 — same register-persistence semantics
        int scratch;
        double priceMultiplier;
        int affordableQty;
        short savedSpobIndex;
        short itemHit = 0;
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        done = false;
        modalFilterUpp = MacToolbox.NewRoutineDescriptor(TradeGlobals.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(TradeGlobals.FilterProc, FilterAdapter);
        for (scratch = 0; (short)scratch < TradeGlobals.Picts.Length; scratch = scratch + 1)
        {
            pictHandle = MacToolbox.GetPicture(scratch + 7000);
            TradeGlobals.Picts[(short)scratch] = pictHandle;
        }
        savedSpobIndex = GameData.Player.NavTargetSpob;
        var spob = CurrentSpob.Rec;
        // Buy/sell spread multiplier (data-seg doubles toc-0x65f0/-0x65f8/-0x6600):
        // 1.25 normally, 1.1 in revolt, 1.5 once trading is player-enabled.
        priceMultiplier = 1.25;
        if (spob.Govt != -1 && GalaxyMapGlobals.SystemStatus(spob.System) < 0)
        {
            priceMultiplier = 1.1;
        }
        if (spob.TradingEnabled != 0)
        {
            priceMultiplier = 1.5;
        }
        for (scratch = 0; (loopIndex = (short)scratch) < CommodityPricing.PriceMode.Length; scratch = scratch + 1)
        {
            priceType = (short)CommodityPriceMode.Run((short)scratch, (uint)spob.Flags);
            CommodityPricing.PriceMode[loopIndex] = priceType;
            if (CommodityPricing.PriceMode[loopIndex] == 0)
            {
                CommodityPricing.FinalPrice[loopIndex] = 0;
            }
            else
            {
                if (CommodityPricing.PriceMode[loopIndex] == 1)
                {
                    tmp = (int)((double)CommodityPricing.BasePrice[loopIndex] / priceMultiplier);
                    CommodityPricing.FinalPrice[loopIndex] = (short)tmp;
                }
                if (CommodityPricing.PriceMode[loopIndex] == 2)
                {
                    CommodityPricing.FinalPrice[loopIndex] = CommodityPricing.BasePrice[loopIndex];
                }
                if (CommodityPricing.PriceMode[loopIndex] == 4)
                {
                    tmp = (int)(priceMultiplier * (double)CommodityPricing.BasePrice[loopIndex]);
                    CommodityPricing.FinalPrice[loopIndex] = (short)tmp;
                }
                if (CommodityPricing.FinalPrice[loopIndex] < 5)
                {
                    CommodityPricing.FinalPrice[loopIndex] = 5;
                }
            }
        }
        for (loopIndex = 0; loopIndex < CronTable.Count; loopIndex = (short)(loopIndex + 1))
        {
            if (0 < GameData.Crons[loopIndex].StateCountdown &&
               GameData.Player.NavTargetSpob == GameData.Crons[loopIndex].ChosenSpob)
            {
                short cronCommodity = GameData.Crons[loopIndex].Commodity;
                CommodityPricing.FinalPrice[cronCommodity] = CommodityPricing.BasePrice[cronCommodity];
                CommodityPricing.FinalPrice[cronCommodity] =
                     (short)(CommodityPricing.FinalPrice[cronCommodity] + GameData.Crons[loopIndex].PriceDelta);
                if (CommodityPricing.FinalPrice[cronCommodity] < 5)
                {
                    CommodityPricing.FinalPrice[cronCommodity] = 5;
                }
            }
        }
        WeaponSlotOutfitMap.Store[0] = -1;
        WeaponSlotOutfitMap.Store[1] = -1;
        CommodityPricing.FinalPrice[7] = 0;
        CommodityPricing.FinalPrice[6] = 0;
        for (loopIndex = 0; loopIndex < JunkTable.Count; loopIndex = (short)(loopIndex + 1))
        {
            if (GameData.Player.NavTargetSpob == GameData.Junk[loopIndex].BoughtAtSpob)
            {
                WeaponSlotOutfitMap.Store[0] = loopIndex;
                scratch = (int)(priceMultiplier * (double)GameData.Junk[loopIndex].BasePrice);
                CommodityPricing.FinalPrice[6] = (short)scratch;
            }
            if (GameData.Player.NavTargetSpob == GameData.Junk[loopIndex].SoldAtSpob)
            {
                WeaponSlotOutfitMap.Store[1] = loopIndex;
                scratch = (int)((double)GameData.Junk[loopIndex].BasePrice / priceMultiplier);
                CommodityPricing.FinalPrice[7] = (short)scratch;
            }
        }
        TradeGlobals.DialogWindow = 0;
        scratch = MacToolbox.GetNewDialog(0x3e9, 0, -1);   // behind window = (WindowPtr)-1 (frontmost)
        TradeGlobals.DialogWindow = scratch;
        if (TradeGlobals.DialogWindow != 0)
        {
            NewDialogHook.Run(TradeGlobals.DialogWindow, 0);
            RecenterWindowIntoPlayArea.Run(TradeGlobals.DialogWindow);
            WorldState.TradeCurrentTab = 7;
            CycleTradeTab.Run(0);
            MacToolbox.ShowWindow(TradeGlobals.DialogWindow);
            MacToolbox.SelectWindow(TradeGlobals.DialogWindow);
            MacToolbox.SetPort(TradeGlobals.DialogWindow);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(TradeGlobals.DialogWindow));
            do
            {
                MacToolbox.ModalDialog(modalFilterUpp, ref itemHit);
                if (itemHit == 1)
                {
                    done = true;
                }
                if (itemHit == 2)
                {
                    RunGalaxyMapDialog.Run();
                    GameData.Ships[0].NavTargetSpob = savedSpobIndex;
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(TradeGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(TradeGlobals.DialogWindow));
                }
                if (itemHit == 16)
                {
                    RunPlayerInfoDialog.Run();
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(TradeGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(TradeGlobals.DialogWindow));
                }
                if (itemHit == 17)
                {
                    tradeQty = 0;
                    for (loopIndex = 0; loopIndex < MissionStateTable.Count; loopIndex = (short)(loopIndex + 1))
                    {
                        if (GameData.MissionStates[loopIndex].IsActive != 0)
                        {
                            tradeQty = tradeQty + 1;
                        }
                    }
                    if (0 < tradeQty)
                    {
                        RunMissionInfoDialog.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                        MacToolbox.SetPort(TradeGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(TradeGlobals.DialogWindow));
                    }
                }
                if (itemHit == 13)
                {
                    MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 13, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 14, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    cargoHeadroom = (short)TotalMassWithEscorts.Run();
                    tmpShort = (short)ShipDerivedStats.TotalMassCarried(ShipTable.Player);
                    cargoHeadroom = (short)(cargoHeadroom - tmpShort);
                    if (0 < cargoHeadroom)
                    {
                        affordableQty = (int)((float)GameData.Player.Credits /
                                        (float)CommodityPricing.FinalPrice[WorldState.TradeCurrentTab]);
                        // FAITHFUL: decompile's FUN_1005f964(0x32) is EVO keymap-bit space; the
                        // physical key is 0x32^8 = 0x3A = Option, NOT Grave (reading the raw
                        // literal as Grave is the ^8 trap). TestLiveKeymapBit's MacKeycode overload
                        // re-applies ^8 (Option→bit 0x32), reproducing the decompile exactly.
                        // See OutfitShopFilter.cs / FindNextShipSlot.cs.
                        tmpShort = (short)Keymap.TestLiveKeymapBit(MacKeycode.Option);
                        if (tmpShort == 0)
                        {
                            tradeQty = 10;
                        }
                        else
                        {
                            tradeQty = 100;
                        }
                        if (cargoHeadroom < tradeQty)
                        {
                            tradeQty = (int)cargoHeadroom;
                        }
                        if (affordableQty < tradeQty)
                        {
                            tradeQty = affordableQty;
                        }
                        if (tradeQty < 0)
                        {
                            tradeQty = 0;
                        }
                        if (WorldState.TradeCurrentTab < 6)
                        {
                            GameData.Player.CargoHold[WorldState.TradeCurrentTab] =
                                 (short)(GameData.Player.CargoHold[WorldState.TradeCurrentTab] + (short)tradeQty);
                        }
                        else if (WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6] != -1)
                        {
                            var junkBuy = GameData.Junk[WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6]];
                            junkBuy.PlayerQty = (short)(junkBuy.PlayerQty + (short)tradeQty);
                        }
                        GameData.Ships[0].Credits = GameData.Ships[0].Credits -
                             tradeQty * CommodityPricing.FinalPrice[WorldState.TradeCurrentTab];
                        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, WorldState.TradeCurrentTab + 4, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        WorldState.HudStatusPanelDirty = 1;
                        TickHudRedrawScheduler.Run();
                        MacToolbox.SetPort(TradeGlobals.DialogWindow);
                    }
                }
                if (itemHit == 14)
                {
                    MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 13, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 14, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    if (WorldState.TradeCurrentTab < 6)
                    {
                        qtyHeld = GameData.Player.CargoHold[WorldState.TradeCurrentTab];
                    }
                    else if (WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6] != -1)
                    {
                        qtyHeld = GameData.Junk[WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6]].PlayerQty;
                    }
                    if (0 < qtyHeld)
                    {
                        // FAITHFUL: decompile's FUN_1005f964(0x32) is EVO keymap-bit space; the
                        // physical key is 0x32^8 = 0x3A = Option, NOT Grave (reading the raw
                        // literal as Grave is the ^8 trap). TestLiveKeymapBit's MacKeycode overload
                        // re-applies ^8 (Option→bit 0x32), reproducing the decompile exactly.
                        // See OutfitShopFilter.cs / FindNextShipSlot.cs.
                        unitSize = (short)Keymap.TestLiveKeymapBit(MacKeycode.Option);
                        if (unitSize == 0)
                        {
                            unitSize = 10;
                        }
                        else
                        {
                            unitSize = 100;
                        }
                        if (WorldState.TradeCurrentTab < 6)
                        {
                            scratch = (int)GameData.Player.CargoHold[WorldState.TradeCurrentTab];
                            if (scratch == (scratch / (int)unitSize) * (int)unitSize)
                            {
                                tradeQty = (int)unitSize;
                            }
                            else
                            {
                                scratch = (int)GameData.Player.CargoHold[WorldState.TradeCurrentTab];
                                tradeQty = scratch - (scratch / (int)unitSize) * (int)unitSize;
                            }
                        }
                        else if (WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6] != -1)
                        {
                            scratch = (int)GameData.Junk[WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6]].PlayerQty;
                            if (scratch == (scratch / (int)unitSize) * (int)unitSize)
                            {
                                tradeQty = (int)unitSize;
                            }
                            else
                            {
                                scratch = (int)GameData.Junk[WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6]].PlayerQty;
                                tradeQty = scratch - (scratch / (int)unitSize) * (int)unitSize;
                            }
                        }
                        if (WorldState.TradeCurrentTab < 6)
                        {
                            GameData.Player.CargoHold[WorldState.TradeCurrentTab] =
                                 (short)(GameData.Player.CargoHold[WorldState.TradeCurrentTab] - (short)tradeQty);
                        }
                        else if (WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6] != -1)
                        {
                            var junkSell = GameData.Junk[WeaponSlotOutfitMap.Store[WorldState.TradeCurrentTab - 6]];
                            junkSell.PlayerQty = (short)(junkSell.PlayerQty - (short)tradeQty);
                        }
                        GameData.Ships[0].Credits = GameData.Ships[0].Credits +
                             tradeQty * CommodityPricing.FinalPrice[WorldState.TradeCurrentTab];
                        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, WorldState.TradeCurrentTab + 4, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        WorldState.HudStatusPanelDirty = 1;
                        TickHudRedrawScheduler.Run();
                        MacToolbox.SetPort(TradeGlobals.DialogWindow);
                    }
                }
            } while (!done);
            for (loopIndex = 0; loopIndex < TradeGlobals.Picts.Length; loopIndex = (short)(loopIndex + 1))
            {
                MacToolbox.HPurge(TradeGlobals.Picts[loopIndex]);
                MacToolbox.ReleaseResource(TradeGlobals.Picts[loopIndex]);
            }
            MacToolbox.DisposeRoutineDescriptor(modalFilterUpp);
            MacToolbox.DisposeDialog(TradeGlobals.DialogWindow);
            RepaintGameWindow.Run();
            GameData.Ships[0].NavTargetSpob = savedSpobIndex;
            WorldState.SpawnPulseDirty = 1;
        }
        return;
    }
}
