using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1003b444 (EV Override-11.c lines 24284-24510; ASM: reference/disasm/
// _code_interstitial.asm loc_3B444 — no top-level `sub_`, reached only via
// the filter-proc pointer table off_82530) — the SHIPYARD dialog's
// modal filter. Every event re-derives the buy-button enable (buy-ship mode:
// credits >= quantized price - trade-in resale; escort mode: credits >= 10% of
// the quantized price); Return/Enter leaves (item 7); the map/info/missions
// keymap actions fire items 3/4/11; grid clicks map the 4x5 cell (84 x 55
// px) onto ShipyardState.AvailableRowIndex[cell + FirstVisibleRow], reload the
// selected ship's desc + PICT (cheat flag -> desc 2900 / PICT 5200), and a
// double-click (same slot within 16 ticks) opens the ship-specs dialog;
// other clicks track the 5-button row; update events redraw. Registered under
// Model.ShipyardState.FilterProc.
//
// Dialog 4-rules rewrite: typed MacEvent filter over the real EventRecord
// offsets — the Return/Enter→leave check reads message@+2 (decompile 24374
// `(char)*(undefined4 *)(param_2 + 1) == '\r'`, evt.Message), and the
// double-click window reads when@+6 (decompile 24450 `(uint)(*(int *)
// (param_2 + 3) - **(int **)(local_ac + -0x7650)) < 0x10`, evt.When). (Note:
// ModalDialog forwards mouseDown (what=1) — so the grid + button-row click
// mapping is live — and returns a positive null-event itemHit; it also
// forwards keyDown/autoKey (what=3/5) before its own typed-char → default-item
// fallback, so the Return/Enter→leave and keymap-action branches are live too.)
public static class ShipyardFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        var itemType = new short[1];     // auStack_70
        var itemHandle = new int[1];     // auStack_7c
        var itemRect = new short[4];     // local_64/62/60/5e

        // Per-event buy-enable re-derive.
        if (Model.ShipyardState.EscortMode == 0)
        {
            if (Model.ShipyardState.SelectedRow == -1)
            {
                Model.ShipyardState.BuyEnabled = 0;
            }
            else
            {
                int spobOffset = Core.Model.GameData.Player.NavTargetSpob * 0x48;   // spob record stride 0x48
                short spobTech = Core.Model.GameData.Spobs[Core.Model.GameData.Player.NavTargetSpob].TechLevel;
                int price = Misc.PriceQuantize.Run((int)Dialog.Model.SpaceportGlobals.ShopPriceScale[1],
                                Core.Model.GameData.ShipClasses[Model.ShipyardState.SelectedRow].Cost, (short)spobOffset,
                                Core.Model.GameData.ShipClasses[Model.ShipyardState.SelectedRow].TechLevel, spobTech);
                int resale = (int)ComputeShipResaleValue.Run();   // FUN_1005e948
                resale = Misc.PriceQuantize.Run((int)Dialog.Model.SpaceportGlobals.ShopPriceScale[0], resale, (short)spobOffset,
                                Core.Model.GameData.ShipClasses[Core.Model.GameData.Player.ShipClass].TechLevel, spobTech);
                resale = Misc.PriceQuantize.Run((int)Dialog.Model.SpaceportGlobals.ShopPriceScale[1], resale, (short)spobOffset,
                                Core.Model.GameData.ShipClasses[Core.Model.GameData.Player.ShipClass].TechLevel, spobTech);
                int cost = price - resale;
                if (cost < 0)
                {
                    cost = 0;
                }
                Model.ShipyardState.BuyEnabled = (byte)(Core.Model.GameData.Player.Credits < cost ? 0 : 1);
            }
        }
        else if (Model.ShipyardState.SelectedRow == -1)
        {
            Model.ShipyardState.BuyEnabled = 0;
        }
        else
        {
            int spobOffset = Core.Model.GameData.Player.NavTargetSpob * 0x48;   // spob record stride 0x48
            int price = Misc.PriceQuantize.Run((int)Dialog.Model.SpaceportGlobals.ShopPriceScale[1],
                            Core.Model.GameData.ShipClasses[Model.ShipyardState.SelectedRow].Cost, (short)spobOffset,
                            Core.Model.GameData.ShipClasses[Model.ShipyardState.SelectedRow].TechLevel,
                            Core.Model.GameData.Spobs[Core.Model.GameData.Player.NavTargetSpob].TechLevel);
            // Hiring an escort costs 10% of the ship's price (decompile's signed
            // int->double idiom here is == (double)price).
            int hireCost = (int)(0.1 * price);
            Model.ShipyardState.BuyEnabled = (byte)(Core.Model.GameData.Player.Credits < hireCost ? 0 : 1);
        }
        if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) &&
            ((byte)evt.Message == '\r' ||   // charCode = message low byte: Return
             (byte)evt.Message == '\x03'))  // … or Enter
        {
            evt.WhatType = MacEventType.MouseDown;   // decompile morphs the event to mouseDown (*param_2 = 1)
            itemHit = 7;
            return 1;
        }
        Misc.Model.Keymap.RefreshCachedKeymap();   // FUN_1005f900
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action9)) != 0)
        {
            itemHit = 3;
            return 1;
        }
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action28)) != 0)
        {
            itemHit = 4;
            return 1;
        }
        if (Misc.Model.Keymap.TestCachedKeymapBit((int)Misc.Model.Keymap.Slot(KeyAction.Action43)) != 0)
        {
            itemHit = 11;
            return 1;
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            // Dead key-table lookup kept from the original (result unused).
            Misc.LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 5, itemType, itemHandle, itemRect);
            if (MacToolbox.PtInRect(mousePoint, itemRect))
            {
                // Grid cell: rows of 55 px (point V - rect.top), cols of 84 px.
                int row = ((short)(mousePoint >> 16) - itemRect[0]) / 55;
                short col = (short)(((short)mousePoint - itemRect[1]) / 84);
                if (-1 < col && col < 4 && -1 < (short)row && (short)row < 5)
                {
                    short prevSlot = Model.ShipyardState.SelectedSlot;
                    Model.ShipyardState.SelectedSlot = (short)(col + (row << 2));
                    short availIdx = Model.ShipyardState.AvailableRowIndex[
                        Model.ShipyardState.SelectedSlot + Model.ShipyardState.FirstVisibleRow];
                    if (availIdx == -1)
                    {
                        Model.ShipyardState.SelectedSlot = -1;
                        Model.ShipyardState.SelectedRow = -1;
                    }
                    else
                    {
                        Model.ShipyardState.SelectedRow = availIdx;
                    }
                    if (Model.ShipyardState.SelectedSlot != prevSlot)
                    {
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 1, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 5, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 6, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 8, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 9, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(Model.ShipyardState.DialogWindow, 10, itemType, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        if (Model.ShipyardState.SelectedSlot == -1)
                        {
                            Core.Model.OutfitDescText.Text = "";   // toc-0x66b9 = 0x10081fa7, a NUL (dumped)
                        }
                        else
                        {
                            if (Model.ShipyardState.SelectedShipPict != 0)
                            {
                                MacToolbox.HPurge(Model.ShipyardState.SelectedShipPict);
                                MacToolbox.ReleaseResource(Model.ShipyardState.SelectedShipPict);
                            }
                            if (!Title.Model.TitleScreenGlobals.CheatSoundPlayed)
                            {
                                if (Model.ShipyardState.EscortMode == 0)
                                {
                                    Core.Model.OutfitDescText.Text = Text.LoadDescriptionText.Load((short)(Model.ShipyardState.SelectedRow + 2000));
                                }
                                else
                                {
                                    Core.Model.OutfitDescText.Text = Text.LoadDescriptionText.Load((short)(Model.ShipyardState.SelectedRow + 2100));
                                }
                                Model.ShipyardState.SelectedShipPict = MacToolbox.GetPicture(Model.ShipyardState.SelectedRow + 5000);
                            }
                            else
                            {
                                // The shipyard cheat ship: desc 2900 + PICT 5200.
                                Core.Model.OutfitDescText.Text = Text.LoadDescriptionText.Load(2900);
                                Model.ShipyardState.SelectedShipPict = MacToolbox.GetPicture(5200);
                            }
                        }
                    }
                    // Double-click on the already-selected slot within 16 ticks opens
                    // the ship-specs dialog (evt.When is real TickCount now).
                    if (Model.ShipyardState.SelectedSlot == Model.ShipyardState.SelectedSlotB &&
                        Model.ShipyardState.SelectedSlot != -1 &&
                        (uint)(evt.When - Model.ShipyardState.LastClickWhen) < 16)
                    {
                        RedrawShipyardDialog.Run();
                        Dialog.RunShipSpecsDialog.Run(Model.ShipyardState.SelectedRow);   // FUN_1003c5f0
                        MacToolbox.SetPort(Model.ShipyardState.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(Model.ShipyardState.DialogWindow));
                    }
                    Model.ShipyardState.SelectedSlotB = Model.ShipyardState.SelectedSlot;
                    Model.ShipyardState.LastClickWhen = evt.When;
                }
            }
            short hit = (short)TrackShipyardButtonHit.Run(mousePoint);
            switch (hit)
            {
                case 0: itemHit = 7; break;
                case 1: itemHit = 1; break;
                case 2: itemHit = 10; break;
                case 3: itemHit = 12; break;
                case 4: itemHit = 13; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.BeginUpdate(Model.ShipyardState.DialogWindow);
            RedrawShipyardDialog.Run();
            MacToolbox.EndUpdate(Model.ShipyardState.DialogWindow);
        }
        return 0;
    }
}
