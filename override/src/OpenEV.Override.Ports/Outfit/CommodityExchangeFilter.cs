using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1003579c (EV Override-11.c lines 21916-22053; ASM: reference/disasm/
// _code_interstitial.asm loc_3579C — no top-level `sub_`, reached only via
// the filter-proc pointer table off_82548) — the COMMODITY EXCHANGE
// dialog's modal filter (ShowCommodityExchangeDialog, DLOG 0x3e9; the
// old "OutfitFilter" class name was a misname — this is the filter the UPP
// cell 0x1008106c / TradeGlobals.FilterProc holds). Keymap actions
// 9/28/43 fire the map (item 2) / player-info (item 16) / missions (item 17)
// buttons; Return/Enter leaves (item 1, morphing the event to mouseDown);
// Tab / up / down cycle the selected commodity tab; 'b'/'s' fire buy/sell
// (items 13/14). Clicks on the 8 commodity rows (items 4..11, gated on a
// nonzero price) select + drag-track the row highlight; other clicks fall
// through to the leave/buy/sell button row (TrackTradeButtonRow); update
// events redraw via DrawCommodityTradeDialog (= FUN_10035ddc, the commodity
// dialog redraw; renamed from the Pass-1 mislabel "DrawOutfitterDialog" —
// this is the Trade/Exchange tab, not the separate Outfitter dialog).
//
// Dialog 4-rules rewrite: typed MacEvent filter over the real EventRecord
// offsets — charCode is the low byte of message@+2 (decompile 21950 `(char)
// *(undefined4 *)(param_2 + 1)`); the Tab branch reads modifiers@+14 bit
// 0x200 (decompile 21956 `(param_2[7] & 0x200U)`, evt.Modifiers — the core
// modal loop currently always sends 0); the mouse point is evt+10 (decompile
// 21988 `param_2 + 5`, evt.WherePacked). GetMouse in the drag loop reads back
// a real packed Point.
public static class CommodityExchangeFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        var itemType = new short[1];     // auStack_4c
        var itemHandle = new int[1];     // auStack_58
        var itemRect = new short[4];     // auStack_42

        Misc.Model.Keymap.RefreshCachedKeymap();   // FUN_1005f900
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action9)) != 0)
        {
            itemHit = 2;
            return 1;
        }
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action28)) != 0)
        {
            itemHit = 16;
            return 1;
        }
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action43)) != 0)
        {
            itemHit = 17;
            return 1;
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)evt.Message;   // raw charCode (the key-table lookup comes later)
            if (keyChar == '\r' || keyChar == '\x03')
            {
                evt.WhatType = MacEventType.MouseDown;   // decompile morphs the event to mouseDown (*param_2 = 1)
                itemHit = 1;
                return 1;
            }
            // DEVIATION (faithful): CycleTradeTab only updates state + calls
            // InvalRect, which is a no-op in this port's immediate-mode renderer (the
            // original relied on the OS turning that into an updateEvt). Added an
            // explicit DrawCommodityTradeDialog.Run() below when the keyboard Tab/arrow
            // path changes the tab, matching the mouse-drag row-select path's own
            // compensation for the same gap.
            short tabBeforeKey = Core.Model.WorldState.TradeCurrentTab;
            if (keyChar == '\t')
            {
                if ((evt.Modifiers & 0x200) == 0)
                {   // shift = cycle backward
                    CycleTradeTab.Run(0);
                }
                else
                {
                    CycleTradeTab.Run(1);
                }
            }
            if (keyChar == 31)
            {   // down arrow
                CycleTradeTab.Run(0);
            }
            if (keyChar == 30)
            {   // up arrow
                CycleTradeTab.Run(1);
            }
            if (Core.Model.WorldState.TradeCurrentTab != tabBeforeKey)
            {
                DrawCommodityTradeDialog.Run();
            }
            keyChar = (byte)Misc.LookupKeyTableUnshifted.Run((uint)keyChar);
            if (keyChar == 'b')
            {
                itemHit = 13;
                return 1;
            }
            if (keyChar == 's')
            {
                itemHit = 14;
                return 1;
            }
        }
        if (evt.WhatType != MacEventType.MouseDown)
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                MacToolbox.BeginUpdate(TradeGlobals.DialogWindow);
                DrawCommodityTradeDialog.Run();
                MacToolbox.EndUpdate(TradeGlobals.DialogWindow);
                return 0;
            }
            return 0;
        }
        int localPoint = MacToolbox.GlobalToLocal(evt.WherePacked);
        short rowItem;
        int scanItem = 4;
        while (true)
        {
            rowItem = (short)scanItem;
            if (4 + CommodityPricing.FinalPrice.Length <= rowItem)
            {
                // No commodity row hit — fall through to the leave/buy/sell button row.
                short hit = (short)TrackTradeButtonRow.Run(localPoint);   // FUN_1000d2e4
                if (hit == 0)
                {
                    itemHit = 1;
                    return 1;
                }
                if (hit == 1)
                {
                    itemHit = 13;
                    return 1;
                }
                if (hit == 2)
                {
                    itemHit = 14;
                    return 1;
                }
                itemHit = -1;
                return 1;
            }
            MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, scanItem, itemType, itemHandle, itemRect);
            if (MacToolbox.PtInRect(localPoint, itemRect) &&
                CommodityPricing.FinalPrice[rowItem - 4] != 0) break;
            scanItem = scanItem + 1;
        }
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, Core.Model.WorldState.TradeCurrentTab + 4, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 13, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
        MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, 14, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
        int prevTab = (int)Core.Model.WorldState.TradeCurrentTab;
        Core.Model.WorldState.TradeCurrentTab = (short)(rowItem - 4);
        do
        {
            if (!MacToolbox.StillDown())
            {
                MacToolbox.SetPort(TradeGlobals.DialogWindow);
                MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, Core.Model.WorldState.TradeCurrentTab + 4, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                return 0;
            }
            if (Core.Model.WorldState.TradeCurrentTab != (short)prevTab)
            {
                MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, Core.Model.WorldState.TradeCurrentTab + 4, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, prevTab + 4, itemType, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                DrawCommodityTradeDialog.Run();
            }
            int mousePoint = MacToolbox.GetMouse();   // GetMouse(&local_54), read back as packed Point
            for (int dragScan = 0; (rowItem = (short)dragScan) < CommodityPricing.FinalPrice.Length; dragScan = dragScan + 1)
            {
                MacToolbox.GetDialogItem(TradeGlobals.DialogWindow, dragScan + 4, itemType, itemHandle, itemRect);
                if (MacToolbox.PtInRect(mousePoint, itemRect) &&
                    CommodityPricing.FinalPrice[rowItem] != 0)
                {
                    prevTab = (int)Core.Model.WorldState.TradeCurrentTab;
                    Core.Model.WorldState.TradeCurrentTab = rowItem;
                    break;
                }
            }
        } while (true);
    }
}
