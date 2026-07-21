using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Misc;

// FUN_10037ee4 (EV Override-11.c lines 22952-23315) — AdvanceLoadout: the
// outfit-purchase modal dialog (GetNewDialog 1002). Item map: 1=Done,
// 2=ship info (RunGalaxyMapDialog + parent redraw), 13=secondary dialog (gated on
// any MissionStateTable Field0x00 set), 7=Buy (grid++ / Map/StatusClear/ControlBit
// specials), 4=Sell (grid--, 80% refund for stock owned at entry),
// 10/11=scroll the 4-wide grid up/down one row.
public static class AdvanceLoadout
{
    // Typed modal-filter bridge for the UPP cell 0x10081034 ("AiRoutineProcSlot"
    // misname -> FUN_1003904c).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = OutfitShopFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;   // hand the filter's itemHit back to ModalDialog (mouseDown buttons)
        return r;
    }

    public static void Run()
    {
        bool done = false;
        int routineDesc = MacToolbox.NewRoutineDescriptor(OutfitShopState.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(routineDesc, FilterAdapter);

        // Snapshot the owned-outfit counts at dialog entry: selling at-or-below the
        // entry count (pre-owned stock) refunds only 80% — see the sell branch.
        var ownedAtEntry = new short[OwnedOutfitGrid.Count];
        for (short i = 0; i < OwnedOutfitGrid.Count; i = (short)(i + 1))
        {
            ownedAtEntry[i] = OwnedOutfitGrid.Store[i];
        }

        var player = GameData.Player;
        short savedNavTargetSpob = player.NavTargetSpob;
        OutfitShopState.SelectedRow = -1;
        OutfitShopState.SelectedSlot = -1;
        OutfitShopState.FirstVisibleRow = 0;
        OutfitShopState.StatusClearBought = 0;
        OutfitShopState.MapOutfitBought = 0;
        RebuildOwnedOutfitsFromMarket.Run();
        BuildAvailableOutfitList.Run(CurrentSpob.Rec);

        OutfitShopState.DialogWindow = 0;
        int newDialog = MacToolbox.GetNewDialog(1002, 0, -1);
        OutfitShopState.DialogWindow = newDialog;
        if (OutfitShopState.DialogWindow != 0)
        {
            short hitItem = 0;               // local_74[0]
            var itemKind = new short[1];   // auStack_76
            var itemHandle = new int[1];     // auStack_184
            var itemRect = new short[4];   // auStack_6a (18-byte Rect stack buffer)
            NewDialogHook.Run(OutfitShopState.DialogWindow, 0);
            RecenterWindowIntoPlayArea.Run(OutfitShopState.DialogWindow);
            MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 5, itemKind, itemHandle, itemRect);
            LayoutShopGridAndIconStrip.Run(itemRect);
            PreloadOutfitIconStrip.Run();

            // The outfitter's 10-entry icon-strip PICT array (7000..7005 + 7028..7031).
            for (short i = 0; i < 6; i = (short)(i + 1))
            {
                OutfitShopState.Picts[i] = MacToolbox.GetPicture(i + 7000);
            }
            for (short i = 6; i < OutfitShopState.Picts.Length; i = (short)(i + 1))
            {
                OutfitShopState.Picts[i] = MacToolbox.GetPicture(i + 7022); // 7028..7031
            }

            // The original strncpy's source is a NUL data-seg byte (dumped) — this
            // just clears the desc text, faithfully.
            OutfitDescText.Text = "";

            MacToolbox.ShowWindow(OutfitShopState.DialogWindow);
            MacToolbox.SelectWindow(OutfitShopState.DialogWindow);
            MacToolbox.SetPort(OutfitShopState.DialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(OutfitShopState.DialogWindow));

            do
            {
                MacToolbox.ModalDialog(routineDesc, ref hitItem);
                if (hitItem == 1)
                {
                    done = true;
                }
                if (hitItem == 2)
                {
                    RunGalaxyMapDialog.Run();
                    player.NavTargetSpob = savedNavTargetSpob;
                    PreloadOutfitIconStrip.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(OutfitShopState.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(OutfitShopState.DialogWindow));
                }
                if (hitItem == 13)
                {
                    // Open the secondary mission dialog only if any MissionStateTable
                    // record is active.
                    short activeMissionCount = 0;
                    for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
                    {
                        if (GameData.MissionStates[i].IsActive != 0)
                        {
                            activeMissionCount = (short)(activeMissionCount + 1);
                        }
                    }
                    if (0 < activeMissionCount)
                    {
                        RunMissionInfoDialog.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                        MacToolbox.SetPort(OutfitShopState.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(OutfitShopState.DialogWindow));
                    }
                }
                if (hitItem == 7 && ShipyardState.BuyEnabled != 0)
                { // BUY
                    if (OutfitShopState.SelectedRow != -1)
                    {
                        short repeat = 1;
                        // FAITHFUL: decompile's FUN_1005f964(0x32)/(0x3f) are EVO keymap-bit
                        // space; the physical keys are 0x32^8 = Option (×5) and 0x3f^8 = Command
                        // (×10), NOT Grave — reading the raw literals as Grave/Function is the ^8
                        // trap. TestLiveKeymapBit's MacKeycode overload re-applies ^8, reproducing
                        // the decompile exactly. See OutfitShopFilter.cs / FindNextShipSlot.cs.
                        if (Keymap.TestLiveKeymapBit(MacKeycode.Option) != 0)
                        { // buy 5 (Option/Win)
                            repeat = 5;
                        }
                        if (Keymap.TestLiveKeymapBit(MacKeycode.Command) != 0)
                        { // buy x10
                            repeat = (short)(repeat * 10);
                        }
                        for (short counter = 0; counter < repeat; counter = (short)(counter + 1))
                        {
                            if (ShipyardState.BuyEnabled != 0)
                            {
                                short sel = OutfitShopState.SelectedRow;
                                var outfit = OutfitTable.Store[sel];
                                int spobByteOff = player.NavTargetSpob * 0x48;
                                int price = PriceQuantize.Run(
                                    (int)SpaceportGlobals.ShopPriceScale[0], // f1 scale float (unused by the body)
                                    outfit.Cost, (short)spobByteOff, outfit.TechLevel,
                                    GameData.Spobs[player.NavTargetSpob].TechLevel);
                                if (outfit.ModType[0] == OutfitModType.Map)
                                {
                                    FloodVisitedSysts.Run(player.CurrentSystem, outfit.ModValue[0]);
                                    OutfitShopState.MapOutfitBought = 1;
                                }
                                else if (outfit.ModType[1] == OutfitModType.Map)
                                {
                                    FloodVisitedSysts.Run(player.CurrentSystem, outfit.ModValue[1]);
                                    OutfitShopState.MapOutfitBought = 1;
                                }
                                else if (outfit.ModType[0] == OutfitModType.StatusClear ||
                                        outfit.ModType[1] == OutfitModType.StatusClear)
                                {
                                    for (short bank = 0; bank < OutfitRecord.ModBankCount; bank = (short)(bank + 1))
                                    {
                                        if (outfit.ModType[bank] == OutfitModType.StatusClear)
                                        {
                                            // FAITHFUL QUIRK: the original reuses the outer repeat
                                            // counter (sVar12) for this 1000-system scan — after a
                                            // StatusClear purchase the repeat loop always exits.
                                            for (counter = 0; counter < SystTable.Count; counter = (short)(counter + 1))
                                            {
                                                if (SystTable.Store[counter].ShownFlag != 0)
                                                {
                                                    if (outfit.ModValue[bank] == -1)
                                                    {
                                                        if (GalaxyMapGlobals.SystemStatus(counter) < 0)
                                                        {
                                                            GalaxyMapGlobals.SetSystemStatus(counter, 0);
                                                        }
                                                    }
                                                    else if (SystTable.Store[counter].Govt == outfit.ModValue[bank] &&
                                                            GalaxyMapGlobals.SystemStatus(counter) < 0)
                                                    {
                                                        GalaxyMapGlobals.SetSystemStatus(counter, 0);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    OutfitShopState.StatusClearBought = 1;
                                }
                                else if (outfit.ModType[0] == OutfitModType.ControlBit ||
                                        outfit.ModType[1] == OutfitModType.ControlBit)
                                {
                                    for (short bank = 0; bank < OutfitRecord.ModBankCount; bank = (short)(bank + 1))
                                    {
                                        if (outfit.ModType[bank] == OutfitModType.ControlBit)
                                        {
                                            if (outfit.ModValue[bank] < 1000)
                                            {
                                                ControlBits.Set(outfit.ModValue[bank], 1);
                                            }
                                            else
                                            {
                                                // >= 1000 CLEARS bit ModValue-1000 (decompile's AliasBase 0x1008efe4 spelling).
                                                ControlBits.Set(outfit.ModValue[bank] - 1000, 0);
                                            }
                                        }
                                    }
                                    OwnedOutfitGrid.Store[sel] = (short)(OwnedOutfitGrid.Store[sel] + 1);
                                }
                                else
                                {
                                    OwnedOutfitGrid.Store[sel] = (short)(OwnedOutfitGrid.Store[sel] + 1);
                                }
                                player.Credits = player.Credits - price;
                            }
                            ShipyardState.BuyEnabled = (byte)AffordabilityCheck.Run();
                        }
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 5, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 9, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        SpaceportGlobals.LoadoutChanged = 1;
                        WorldState.HudStatusPanelDirty = 1;
                        TickHudRedrawScheduler.Run();
                        MacToolbox.SetPort(OutfitShopState.DialogWindow);
                    }
                }
                if (hitItem == 4 && OutfitShopState.SellEnabled != 0)
                { // SELL
                    short repeat = 1;
                    // FAITHFUL: decompile's FUN_1005f964(0x32)/(0x3f) are EVO keymap-bit
                    // space; the physical keys are 0x32^8 = Option (×5) and 0x3f^8 = Command
                    // (×10), NOT Grave — reading the raw literals as Grave/Function is the ^8
                    // trap. TestLiveKeymapBit's MacKeycode overload re-applies ^8, reproducing
                    // the decompile exactly. See OutfitShopFilter.cs / FindNextShipSlot.cs.
                    if (Keymap.TestLiveKeymapBit(MacKeycode.Option) != 0)
                    { // sell 5 (Option/Win)
                        repeat = 5;
                    }
                    if (Keymap.TestLiveKeymapBit(MacKeycode.Command) != 0)
                    { // sell x10
                        repeat = (short)(repeat * 10);
                    }
                    for (short n = 0; n < repeat; n = (short)(n + 1))
                    {
                        if (OutfitShopState.SellEnabled != 0)
                        {
                            short sel = OutfitShopState.SelectedRow;
                            var outfit = OutfitTable.Store[sel];
                            int spobByteOff = player.NavTargetSpob * 0x48;
                            uint refund = (uint)PriceQuantize.Run(
                                (int)SpaceportGlobals.ShopPriceScale[0],
                                outfit.Cost, (short)spobByteOff, outfit.TechLevel,
                                GameData.Spobs[player.NavTargetSpob].TechLevel);
                            if (OwnedOutfitGrid.Store[sel] <= ownedAtEntry[sel])
                            {
                                // Selling at-or-below the entry count = pre-owned stock:
                                // 80% refund (toc-0x6618 double at 0x10082048, dumped = 0.8).
                                refund = (uint)((double)(int)refund * 0.8);
                            }
                            short freeMassBefore = (short)ShipDerivedStats.FreeMassSpace();
                            OwnedOutfitGrid.Store[sel] = (short)(OwnedOutfitGrid.Store[sel] - 1);
                            short freeMassAfter = (short)ShipDerivedStats.FreeMassSpace();
                            if (freeMassAfter < 0 && -1 < freeMassBefore)
                            {
                                // Removing this outfit would overload the ship (e.g. a cargo
                                // expansion with full holds) — revert and beep.
                                OwnedOutfitGrid.Store[sel] = (short)(OwnedOutfitGrid.Store[sel] + 1);
                                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                                break;
                            }
                            player.Credits = (int)((uint)player.Credits + refund);
                            if (OwnedOutfitGrid.Store[sel] < 1 &&
                                outfit.ModType[0] == OutfitModType.Weapon &&
                                player.SelectedWeaponSlot == outfit.ModValue[0])
                            {
                                player.SelectedWeaponSlot = -1; // sold the selected weapon — deselect
                                WorldState.HudWeaponPanelDirty = 1;
                            }
                            if (OwnedOutfitGrid.Store[sel] < 1 &&
                                outfit.ModType[1] == OutfitModType.Weapon &&
                                player.SelectedWeaponSlot == outfit.ModValue[1])
                            {
                                player.SelectedWeaponSlot = -1;
                                WorldState.HudWeaponPanelDirty = 1;
                            }
                        }
                        if (OutfitShopState.SelectedRow == -1)
                        {
                            OutfitShopState.SellEnabled = 0;
                        }
                        else
                        {
                            if (OwnedOutfitGrid.Store[OutfitShopState.SelectedRow] < 1)
                            {
                                OutfitShopState.SellEnabled = 0;
                            }
                            else
                            {
                                if ((OutfitTable.Store[OutfitShopState.SelectedRow].Flags & OutfFlags.CannotSell) == 0)
                                {
                                    OutfitShopState.SellEnabled = 1;
                                }
                                else
                                {
                                    OutfitShopState.SellEnabled = 0;
                                }
                            }
                        }
                    }
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 5, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 9, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    SpaceportGlobals.LoadoutChanged = 1;
                    WorldState.HudStatusPanelDirty = 1;
                    TickHudRedrawScheduler.Run();
                    MacToolbox.SetPort(OutfitShopState.DialogWindow);
                }
                if (hitItem == 10 && 3 < OutfitShopState.FirstVisibleRow)
                { // scroll up one 4-wide row
                    OutfitShopState.FirstVisibleRow = (short)(OutfitShopState.FirstVisibleRow - 4);
                    OutfitShopState.SelectedSlot = -1;
                    OutfitShopState.SelectedRow = -1;
                    OutfitDescText.Text = ""; // clear the desc text (src byte is NUL, see above)
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 5, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 6, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 8, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 9, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 10, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 11, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 13, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                }
                if (hitItem == 11)
                { // scroll down one 4-wide row
                    short availableRowCount = 0;
                    for (short row = 0; row < OutfitShopState.RowCount; row = (short)(row + 1))
                    {
                        if (OutfitShopState.AvailableRowIndex[row] != -1)
                        {
                            availableRowCount = (short)(availableRowCount + 1);
                        }
                    }
                    if (OutfitShopState.FirstVisibleRow < availableRowCount - 20)
                    {
                        OutfitShopState.FirstVisibleRow = (short)(OutfitShopState.FirstVisibleRow + 4);
                        OutfitShopState.SelectedSlot = -1;
                        OutfitShopState.SelectedRow = -1;
                        OutfitDescText.Text = "";
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 4, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 7, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 5, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 6, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 8, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 9, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 10, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 11, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        MacToolbox.GetDialogItem(OutfitShopState.DialogWindow, 13, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                    }
                }
            } while (!done);

            // Buying engine/tank downgrades can leave fuel/shield above the new max — clamp.
            short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(ShipTable.Player);
            if (fuelMax < player.Fuel)
            {
                player.Fuel = fuelMax;
                WorldState.ShieldEnergyBarDirty = 1;
            }
            short shieldMax = (short)ShipDerivedStats.EffectiveShieldMax(ShipTable.Player);
            // The shield slot holds an INT value (the original compares/stores +0x68 as int,
            // not the float this managed field is declared as — see the codebase-wide
            // (int)ship.Shield convention, e.g. ApplyShipDamage.cs).
            if (shieldMax < (int)player.Shield)
            {
                player.Shield = shieldMax;
                WorldState.PlayerShieldBarDirty = 1;
            }
            for (short i = 0; i < OwnedOutfitGrid.Count; i = (short)(i + 1))
            {
                if (0 < OwnedOutfitGrid.Store[i] && (OutfitTable.Store[i].Flags & OutfFlags.RemoveAfterPurchase) != 0)
                {
                    OwnedOutfitGrid.Store[i] = 0;
                }
            }
            RebuildMarketFromOwnedOutfits.Run();
            for (short i = 0; i < OutfitShopState.Picts.Length; i = (short)(i + 1))
            {
                MacToolbox.HPurge(OutfitShopState.Picts[i]);
                MacToolbox.ReleaseResource(OutfitShopState.Picts[i]);
            }
            MacToolbox.DisposeRoutineDescriptor(routineDesc);
            MacToolbox.DisposeDialog(OutfitShopState.DialogWindow);
            GWorldPort.SetActivePortScratch();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(GlobalState.ScratchStageRect); // anim-scratch stage Rect
            RefreshStatusPanel.Run();
            SetGamePortAndDevice.Run();
            RepaintGameWindow.Run();
            player.NavTargetSpob = savedNavTargetSpob;
            WorldState.SpawnPulseDirty = 1;
        }
        return;
    }
}
