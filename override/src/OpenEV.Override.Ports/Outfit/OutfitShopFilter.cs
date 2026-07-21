using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1003904c (EV Override-11.c lines 23321-23524; ASM: reference/disasm/
// _code_interstitial.asm loc_3904C — no top-level `sub_`, reached only via
// the filter-proc pointer table off_82538) — the OUTFITTER (AdvanceLoadout,
// DLOG 0x3ea) modal dialog filter, registered under OutfitShopState.FilterProc
// (UPP cell 0x10081034).
//
// Per event poll it: snapshots the Option/Command ×N-multiplier live-key flags (and
// invalidates the ×N readout, item 3, when they changed); re-derives the
// buy/sell-enabled flags from the selection; maps the map key (slot 9) to
// item 2 and the mission-info key (slot 43) to item 13; turns Return/Enter
// into item 1; resolves grid clicks into SelectedSlot/SelectedRow (+ desc
// text and PICT reload); routes the 5-button row through
// TrackOutfitButtonMouseDown; and redraws on updateEvt.
//
// Dialog 4-rules rewrite: typed MacEvent filter (raw EventRecord scratch
// gone); GetDialogItem outs are managed arrays; all toc-relative cells
// routed to their managed homes. The Return/Enter, grid-click, and
// updateEvt-redraw checks compare the full `what` short (decompile
// 23391/23398/23493 compare `*param_2`, i.e. evt.WhatType); evt.When is the
// real TickCount.
public static class OutfitShopFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int window = Model.OutfitShopState.DialogWindow;
        short result;
        int returnValue;
        var itemKind = new short[1];   // auStack_54
        var itemHandle = new int[1];     // auStack_58
        var itemRect = new short[4];   // local_4a/local_48 (+ auStack_52 for the InvalRect runs)
        var keyFlagsBackup = new byte[2];   // local_68 (decompile declares char[12]; only [0..1] used)

        for (short i = 0; i < keyFlagsBackup.Length; i = (short)(i + 1))
        {
            keyFlagsBackup[i] = Model.OutfitShopState.KeyFlagsSnapshot[i];
            Model.OutfitShopState.KeyFlagsSnapshot[i] = 0;
        }
        // FAITHFUL (not a deviation): the decompile's ×5 key is FUN_1005f964(0x32) and its
        // ×10 key is FUN_1005f964(0x3f). Those literals are EVO keymap-bit space (real-ADB
        // keycode ^ 8), so the physical keys are 0x32^8 = 0x3A = Option and 0x3f^8 = 0x37 =
        // Command — NOT Grave/Function (that is how the raw literals read only if you forget
        // the ^8, the trap that seeded the old "DEVIATION / rebound-to-Option" comment here).
        // TestLiveKeymapBit's MacKeycode overload re-applies ^8, so Option→bit 0x32 and
        // Command→bit 0x3f reproduce the decompile bit-for-bit. (Win/Alt map to Option/Command
        // on a PC via the ModifierKeyTable host substrate — a separate physical mapping, not a
        // keycode-identity change. A 2026-07-02 user request confirmed Option as the ×5 key; it
        // is also the faithful key, so nothing here deviates.) See FindNextShipSlot.cs and
        // Keymap.TestLiveKeymapBit's keycode-space note.
        if (Misc.Model.Keymap.TestLiveKeymapBit(MacKeycode.Option) != 0)
        {
            Model.OutfitShopState.KeyFlagsSnapshot[0] = 1;
        }
        if (Misc.Model.Keymap.TestLiveKeymapBit(MacKeycode.Command) != 0)
        {
            Model.OutfitShopState.KeyFlagsSnapshot[1] = 1;
        }
        // Decompile do{}while + goto LAB_1003915c: compare the two snapshot
        // bytes; on the FIRST difference invalidate the ×N readout (item 3)
        // and fall into the main body — which always runs exactly once.
        //
        // DEVIATION (faithful): on real Mac, InvalRect makes the Window Manager
        // synthesize a real updateEvt on the very next GetNextEvent poll inside
        // ModalDialog's own loop, which this filter's updateEvt branch below
        // redraws from. Our MacToolbox.InvalRect is a no-op (RunModalLoop only
        // redraws on entry or on a real click/hit), so a pure held-key change
        // with no other event would never get a redraw. Signals it the same way
        // the Prefs keybind-capture filter (HandleKeyAssignDialogEvent) does:
        // return 1 with itemHit left at 0 on a NullEvent poll, which
        // RunModalLoop's documented contract treats as "redraw, not a hit."
        bool multiplierIndicatorDirty = false;
        for (short i = 0; i < keyFlagsBackup.Length; i = (short)(i + 1))
        {
            if (keyFlagsBackup[i] != Model.OutfitShopState.KeyFlagsSnapshot[i])
            {
                MacToolbox.SetPort(window);
                MacToolbox.GetDialogItem(window, 3, itemKind, itemHandle, itemRect);
                MacToolbox.InvalRect(itemRect);
                multiplierIndicatorDirty = true;
                break;
            }
        }

        // ── Re-derive buy/sell enabled from the selection ─────────────
        if (Model.OutfitShopState.SelectedRow == -1)
        {
            Model.OutfitShopState.SellEnabled = 0;
            Model.ShipyardState.BuyEnabled = 0;
        }
        else
        {
            Model.ShipyardState.BuyEnabled = (byte)AffordabilityCheck.Run();   // FUN_1003a0ac
            if (Model.OwnedOutfitGrid.Store[Model.OutfitShopState.SelectedRow] < 1)
            {
                Model.OutfitShopState.SellEnabled = 0;
            }
            else if ((Model.OutfitTable.Store[Model.OutfitShopState.SelectedRow].Flags & OutfFlags.CannotSell) == 0)
            {
                Model.OutfitShopState.SellEnabled = 1;
            }
            else
            {
                Model.OutfitShopState.SellEnabled = 0;
            }
        }
        Misc.Model.Keymap.RefreshCachedKeymap();   // FUN_1005f900
        result = (short)Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action9));    // map key → item 2
        if (result == 0)
        {
            result = (short)Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action43));   // mission-info key → item 13
            if (result == 0)
            {
                if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) &&
                   ((byte)evt.Message == '\r' ||   // charCode = message low byte: Return
                    (byte)evt.Message == '\x03'))  // … or Enter
                {
                    evt.WhatType = MacEventType.MouseDown;   // decompile morphs the event to mouseDown (*param_2 = 1)
                    itemHit = 1;
                    returnValue = 1;
                }
                else if (evt.WhatType == MacEventType.MouseDown)
                {
                    int localPoint = MacToolbox.GlobalToLocal(evt.WherePacked);
                    MacToolbox.GetDialogItem(window, 5, itemKind, itemHandle, itemRect);
                    if (MacToolbox.PtInRect(localPoint, itemRect))
                    {
                        int gridRow = ((short)(localPoint >> 16) - itemRect[0]) / 55;   // (v − top) / cell height 55
                        short gridCol = (short)(((short)localPoint - itemRect[1]) / 84);   // (h − left) / cell width 84
                        if (-1 < gridCol)
                        {
                            if (gridCol < 4)
                            {
                                short slotRow = (short)gridRow;
                                if (-1 < slotRow && slotRow < 5)
                                {
                                    short prevSlot = Model.OutfitShopState.SelectedSlot;
                                    Model.OutfitShopState.SelectedSlot = (short)(gridCol + (short)(gridRow << 2));
                                    // FirstVisibleRow needs the ×4 + deref through
                                    // OutfitShopState.AvailableRowIndex — don't collapse this
                                    // back to a raw offset read.
                                    if (Model.OutfitShopState.AvailableRowIndex[
                                            (int)Model.OutfitShopState.SelectedSlot + (int)Model.OutfitShopState.FirstVisibleRow] == -1)
                                    {
                                        Model.OutfitShopState.SelectedRow = -1;
                                        Model.OutfitShopState.SelectedSlot = -1;
                                    }
                                    else
                                    {
                                        Model.OutfitShopState.SelectedRow = Model.OutfitShopState.AvailableRowIndex[
                                            (int)Model.OutfitShopState.SelectedSlot + (int)Model.OutfitShopState.FirstVisibleRow];
                                    }
                                    if (Model.OutfitShopState.SelectedSlot != prevSlot)
                                    {
                                        MacToolbox.GetDialogItem(window, 1, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 4, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 7, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 5, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 6, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 8, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 9, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 10, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        MacToolbox.GetDialogItem(window, 11, itemKind, itemHandle, itemRect);
                                        MacToolbox.InvalRect(itemRect);
                                        if (Model.OutfitShopState.SelectedRow == -1)
                                        {
                                            // The source data-seg byte FUN_10076178 would copy from
                                            // (0x10081fa7) is NUL (dumped): clears the desc text.
                                            Core.Model.OutfitDescText.Text = "";
                                        }
                                        else
                                        {
                                            if (Model.ShipyardState.SelectedShipPict != 0)
                                            {
                                                MacToolbox.HPurge(Model.ShipyardState.SelectedShipPict);
                                                MacToolbox.ReleaseResource(Model.ShipyardState.SelectedShipPict);
                                            }
                                            Core.Model.OutfitDescText.Text =   // FUN_100197d8
                                                Text.LoadDescriptionText.Load((short)(Model.OutfitShopState.SelectedRow + 3000));
                                            if (Model.OutfitShopState.SelectedRow < 100)
                                            {
                                                Model.ShipyardState.SelectedShipPict = MacToolbox.GetPicture(Model.OutfitShopState.SelectedRow + 6000);
                                            }
                                            else
                                            {
                                                Model.ShipyardState.SelectedShipPict = MacToolbox.GetPicture(Model.OutfitShopState.SelectedRow + 6200);
                                            }
                                        }
                                    }
                                    Model.ShipyardState.SelectedSlotB = Model.OutfitShopState.SelectedSlot;
                                    Model.ShipyardState.LastClickWhen = evt.When;
                                }
                            }
                        }
                    }
                    result = (short)TrackOutfitButtonMouseDown.Run(localPoint);   // FUN_1000c7b4
                    if (result == 0)
                    {
                        itemHit = 1;
                        returnValue = 1;
                    }
                    else if (result == 1)
                    {
                        itemHit = 7;
                        returnValue = 1;
                    }
                    else if (result == 2)
                    {
                        itemHit = 4;
                        returnValue = 1;
                    }
                    else if (result == 3)
                    {
                        itemHit = 10;
                        returnValue = 1;
                    }
                    else if (result == 4)
                    {
                        itemHit = 11;
                        returnValue = 1;
                    }
                    else
                    {
                        itemHit = -1;
                        returnValue = 1;
                    }
                }
                else if (evt.WhatType == MacEventType.UpdateEvt)
                {   // updateEvt → full redraw
                    MacToolbox.BeginUpdate(window);
                    DrawOutfitShop.Run();
                    MacToolbox.DrawDialog(window);
                    MacToolbox.EndUpdate(window);
                    returnValue = 0;
                }
                else
                {
                    // NullEvent (idle poll): request a redraw-only pass when the
                    // multiplier keys changed above (see the DEVIATION note); otherwise
                    // consume nothing, matching the decompile's plain fallthrough.
                    returnValue = multiplierIndicatorDirty ? 1 : 0;
                }
            }
            else
            {
                itemHit = 13;
                returnValue = 1;
            }
        }
        else
        {
            itemHit = 2;
            returnValue = 1;
        }
        return returnValue;
    }
}
