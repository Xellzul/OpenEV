// Port of FUN_1003a500 (EV Override-11.c lines 23900-24283).

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Title;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// NOTE: count (was unaff_r25 in the decompile) is always overwritten before its next real read at
// each hitItem branch that uses it (11, 13, the final EscortMode InsetRect calc) — the `= default`
// top-of-function init is dead, never a live read.
public static class RunShipyardDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080ffc -> FUN_1003b444) —
    // typed MacEvent shape (dialog 4-rules B5).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = ShipyardFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;   // hand the filter's itemHit back to ModalDialog (mouseDown buttons)
        return r;
    }

    public static void Run()
    {
        float valueFloat;
        bool done;
        uint diff;
        int routineDesc;
        int scratch;
        int picHandle;
        short randRoll;
        byte confirm;
        int count = default;
        short index;
        short innerIndex;
        short savedShip;
        short selectedClass;
        short halfHeightNeg = default;   // local_7a — negative half-height for the EscortMode centering InsetRect
        short hitItem = default;
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        done = false;
        routineDesc = MacToolbox.NewRoutineDescriptor(ShipyardState.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(ShipyardState.FilterProc, FilterAdapter);
        SetGamePortAndDevice.Run();
        ShipyardState.SelectedRow = -1;
        ShipyardState.SelectedSlot = -1;
        ShipyardState.SelectedSlotB = -1;   // *(toc-0x764c)
        ShipyardState.FirstVisibleRow = 0;
        savedShip = GameData.Ships[0].NavTargetSpob;
        ShipyardState.DialogWindow = 0;
        ShipyardState.DialogWindow = MacToolbox.GetNewDialog(0x3ec, 0, -1);   // (WindowPtr)-1 = frontmost
        if (ShipyardState.DialogWindow != 0)
        {
            NewDialogHook.Run(ShipyardState.DialogWindow, 0);
            RecenterWindowIntoPlayArea.Run(ShipyardState.DialogWindow);
            MacToolbox.GetDialogItem(ShipyardState.DialogWindow, 5, itemType, itemHandle, itemRect);
            LayoutShopGridAndIconStrip.Run(itemRect);
            BuildAvailableShipList.Run(new SpobRec(CurrentSpob.Base));
            PreloadShipyardIconStrip.Run();
            scratch = 0;
            while (true)
            {
                index = (short)scratch;
                if (3 < index) break;
                // 0x10080be4 (GameToc-0x7a7c): -> short escort-shipyard mode flag (0 = buy-ship shipyard, nonzero = buy-escort mode; same cell ShipyardFilter/BuildAvailableShipList read raw).
                if (ShipyardState.EscortMode == 0)
                {
                    picHandle = MacToolbox.GetPicture(scratch + 0x1b5e);
                    ShipyardState.Picts[index] = picHandle;
                }
                else
                {
                    picHandle = MacToolbox.GetPicture(scratch + 0x1ba0);
                    ShipyardState.Picts[index] = picHandle;
                }
                scratch = scratch + 1;
            }
            ShipyardState.Picts[4] = MacToolbox.GetPicture(0x1b62);
            ShipyardState.Picts[5] = MacToolbox.GetPicture(0x1b63);
            ShipyardState.Picts[6] = MacToolbox.GetPicture(0x1bcc);
            ShipyardState.Picts[7] = MacToolbox.GetPicture(0x1bcd);
            for (scratch = 0; (short)scratch < 4; scratch = scratch + 1)
            {
                ShipyardState.Picts[(short)scratch + 8] = MacToolbox.GetPicture(scratch + 0x1b74);
            }
            OutfitDescText.Text = "";   // clear the desc text (src toc-0x66b9 = NUL, dumped)
            MacToolbox.ShowWindow(ShipyardState.DialogWindow);
            MacToolbox.SelectWindow(ShipyardState.DialogWindow);
            MacToolbox.SetPort(ShipyardState.DialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
            do
            {
                MacToolbox.ModalDialog(routineDesc, ref hitItem);
                if (hitItem == 1)
                {
                    if (ShipyardState.SelectedRow == -1)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        index = ShipyardState.SelectedRow;
                        count = (int)index;
                        if (ShipyardState.BuyEnabled == 0)
                        {
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                        }
                        else if (ShipyardState.EscortMode == 0)
                        {
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                            // "Please name your new " (toc-0x474c) + <long name (STR# 0x138a)> + ": " (toc-0x66b0);
                            // default = <short name (STR# 0x1389), 0x13 cap> + " " (toc-0x66ad) + 3 random digits.
                            string namePrompt = "Please name your new "
                                + TextScratch.Trunc(ResourceGlobals.NamesStr138a[index], 200)
                                + ": ";
                            string defaultName = TextScratch.Trunc(ResourceGlobals.NamesStr1389[index], 19) + " ";
                            for (innerIndex = 0; innerIndex < 3; innerIndex = (short)(innerIndex + 1))
                            {
                                randRoll = (short)(SeedEvoRng.Run(9));
                                defaultName += (randRoll + 1).ToString();
                            }
                            confirm = (byte)(AlertModal_ThreeButton.Run(namePrompt, defaultName, 20));
                            RepaintGameWindow.Run();
                            MacToolbox.SetPort(ShipyardState.DialogWindow);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                            if (confirm != 0)
                            {
                                SpaceportGlobals.ShipPurchased = 1; // *(toc-0x7610): ship-purchase-committed flag
                                picHandle = (int)(ComputeShipResaleValue.Run());
                                scratch = GameData.Ships[0].NavTargetSpob * 0x48 + 0xc;
                                // NOTE: decompile 1st arg is (double)**(float**)(toc-0x7618), read via ReadFloat (not
                                // ReadInt-on-float, which would treat the int bits as the value). That double 1st
                                // arg is a dead FPR artifact — FUN_100413ac takes 4 int args (price = the 2nd positional);
                                // the 5-arg PriceQuantize.Run overload drops that dead leading FP arg and forwards the GPR args shifted-by-one to the real 4-arg quantizer (Misc/PriceQuantize.cs, FUN_100413ac lines 26810-26839), returning real quantized prices.
                                picHandle = PriceQuantize.Run((int)(double)SpaceportGlobals.ShopPriceScale[0], picHandle, (short)(scratch),
                                                      GameData.ShipClasses[GameData.Ships[0].ShipClass].TechLevel,
                                                      (int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].TechLevel);
                                scratch = GameData.Ships[0].NavTargetSpob * 0x48 + 0xc;
                                scratch = PriceQuantize.Run((int)(double)SpaceportGlobals.ShopPriceScale[1], picHandle,
                                                 (short)(scratch), GameData.ShipClasses[GameData.Ships[0].ShipClass].TechLevel,
                                                 (int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].TechLevel);
                                // NOTE: decompile writes _DAT_1008a4f8[0x18] = byte +0x60 = Credits (not Fuel,
                                // which also sits at a +0x18 float-index — the two fields alias at different strides).
                                GameData.Ships[0].Credits = GameData.Ships[0].Credits + scratch;
                                GameData.Ships[0].ShipClass = index;
                                ShipClassRecord newClass = GameData.ShipClasses[index];
                                scratch = PriceQuantize.Run((int)(double)SpaceportGlobals.ShopPriceScale[1],
                                                 newClass.Cost,
                                                 (short)(GameData.Ships[0].NavTargetSpob * 0x48),
                                                 newClass.TechLevel,
                                                 (int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].TechLevel);
                                // NOTE: same Credits-not-Fuel field mapping as the resale credit above.
                                GameData.Ships[0].Credits = GameData.Ships[0].Credits - scratch;
                                // New ship's name (bounded Pascal copy, 32 bytes) -> the managed ship-name string.
                                PilotIdentity.ShipName = StripLeadingThe.Run(
                                    PilotIdentity.CapturedNameEntry.Length > 31
                                        ? PilotIdentity.CapturedNameEntry.Substring(0, 31)
                                        : PilotIdentity.CapturedNameEntry);
                                for (index = 1; index < 36; index = (short)(index + 1))
                                {
                                    if (GameData.Ships[index].IsActive != 0)
                                    {
                                        if (GameData.Ships[index].OwnerSlot == 0)
                                        {
                                            if (GameData.Ships[index].AiBehaviorType == ShipAiType.NavalFighter)
                                            {
                                                GameData.Ships[index].AiBehaviorType = ShipAiType.Escort;
                                                GameData.Ships[index].IsCarriedFighter = 1;
                                            }
                                        }
                                    }
                                }
                                for (index = 0; index < ShipRecord.WeaponSlotCount; index = (short)(index + 1))
                                {
                                    done = false;
                                    innerIndex = 0;
                                    while (true)
                                    {
                                        if (OutfitTable.Count - 1 < innerIndex) break;
                                        if ((OutfitTable.Store[innerIndex].ModType[0] == OutfitModType.Weapon) &&
                                           (index == OutfitTable.Store[innerIndex].ModValue[0]))
                                        {
                                            if (OutfitTable.Store[innerIndex].PersistentFlagSet != 0)
                                            {
                                                done = true;
                                                break;
                                            }
                                        }
                                        innerIndex = (short)(innerIndex + 1);
                                    }
                                    if (!done)
                                    {
                                        GameData.Ships[0].WeaponSlotType[index] = 0;
                                        GameData.Ships[0].WeaponSlotAmmo[index] = 0;
                                    }
                                }
                                for (index = 0; index < OutfitTable.Count; index = (short)(index + 1))
                                {
                                    if (OutfitTable.Store[index].PersistentFlagSet == 0)
                                    {
                                        OwnedOutfitGrid.Store[index] = 0;
                                    }
                                }
                                for (index = 0; index < 4; index = (short)(index + 1))
                                {
                                    if (0 < newClass.DefaultItemsCount[index])
                                    {
                                        // decompile: scratch = DefaultItems[index] * 2 (byte offset into the short grid); managed grid is element-indexed.
                                        scratch = newClass.DefaultItems[index];
                                        OwnedOutfitGrid.Store[scratch] =
                                             (short)(OwnedOutfitGrid.Store[scratch] + newClass.DefaultItemsCount[index]);
                                    }
                                }
                                RebuildMarketFromOwnedOutfits.Run();
                                // decompile: _DAT_1008a4f8[0x1a] (= ship +0x68 Shield) = *(float*)(class + 0x3a). Class +0x3a is the
                                // int-valued Shield field; numeric assign matches SpawnFleet's convention for that field.
                                GameData.Ships[0].Shield = newClass.Shield;
                                // NOTE: decompile is the plain i2d idiom on class +0x4 (BaseFuel short): Fuel = (float)(double)(int)BaseFuel.
                                GameData.Ships[0].Fuel = newClass.BaseFuel;
                                for (index = 0; index < ShipRecord.WeaponSlotCount; index = (short)(index + 1))
                                {
                                    GameData.Ships[0].WeaponSlotType[index] = (short)(GameData.Ships[0].WeaponSlotType[index] +
                                         newClass.DefaultWeaponType[index]);
                                    GameData.Ships[0].WeaponSlotAmmo[index] = (short)(GameData.Ships[0].WeaponSlotAmmo[index] +
                                         newClass.DefaultWeaponAmmo[index]);
                                }
                                RedistributeCargoAmongShips.Run(0);
                                GameData.Ships[0].TargetSlot = -1;
                                GameData.Ships[0].SelectedWeaponSlot = -1;
                                WorldState.HudWeaponPanelDirty = 1;
                                WorldState.SpawnPulseDirty = 1;
                                WorldState.WeaponSlotDirty = 1;
                                // NOTE: decompile is the i2d idiom on the current spob's XPos/YPos shorts (the X form's fneg-on-low-word is
                                // the decompile's rendering of the xoris 0x8000 sign-flip, not a real negation — the Y form is the clean variant;
                                // same collapse as TickShipAI:457).
                                GameData.Ships[0].PosX = (float)(int)CurrentSpob.Rec.XPos;
                                GameData.Ships[0].PosY = (float)(int)CurrentSpob.Rec.YPos;
                                WorldState.HudStatusPanelDirty = 1;
                                TickHudRedrawScheduler.Run();
                                MacToolbox.SetPort(ShipyardState.DialogWindow);
                                done = true;
                            }
                            MacToolbox.SetPort(ShipyardState.DialogWindow);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                        }
                        else if (ShipyardState.EscortMode != 0)
                        {
                            selectedClass = ShipyardState.SelectedRow;
                            // NOTE: 1st arg is decompile (double)*(float*)(slot+4), read via the managed float scale
                            // (not ReadInt-on-float); class Cost/TechLevel come from the typed ShipClasses table.
                            scratch = PriceQuantize.Run((int)(double)SpaceportGlobals.ShopPriceScale[1],
                                                 GameData.ShipClasses[selectedClass].Cost,
                                                 (short)(GameData.Ships[0].NavTargetSpob * 0x48),
                                                 GameData.ShipClasses[selectedClass].TechLevel,
                                                 (int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].TechLevel);
                            // credits -= escortPriceScale * price, via the float-rounded i2d chain.
                            // NOTE: decompile _DAT_1008a4f8[0x18] = byte +0x60 = Credits (not the Fuel field, which
                            // also sits at a +0x18 float-index — the two fields alias at different strides).
                            valueFloat = (float)(int)-(CommodityPricing.ValueBarScale *
                                              (double)scratch -
                                             (double)GameData.Ships[0].Credits);
                            GameData.Ships[0].Credits = (int)valueFloat;
                            WorldState.HudStatusPanelDirty = 1;
                            SpawnPlayerWingman.Run(selectedClass, GameData.Ships[0].NavTargetSpob);
                            done = true;
                        }
                    }
                }
                if (hitItem == 3)
                {
                    savedShip = GameData.Ships[0].NavTargetSpob;
                    RunGalaxyMapDialog.Run();
                    GameData.Ships[0].NavTargetSpob = savedShip;
                    PreloadShipyardIconStrip.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(ShipyardState.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                }
                if (hitItem == 4)
                {
                    RunPlayerInfoDialog.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(ShipyardState.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                }
                if (hitItem == 11)
                {
                    count = 0;
                    index = 0;
                    while (true)
                    {
                        if (7 < index) break;
                        if (GameData.MissionStates[index].IsActive != 0)
                        {
                            count = count + 1;
                        }
                        index = (short)(index + 1);
                    }
                    if (0 < (short)count)
                    {
                        RunMissionInfoDialog.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                        MacToolbox.SetPort(ShipyardState.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                    }
                }
                if ((hitItem == 10) && (ShipyardState.SelectedRow != -1))
                {
                    RedrawShipyardDialog.Run();
                    RunShipSpecsDialog.Run((int)ShipyardState.SelectedRow);
                    MacToolbox.SetPort(ShipyardState.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                }
                if ((hitItem == 12) && (3 < ShipyardState.FirstVisibleRow))
                {
                    ShipyardState.FirstVisibleRow = (short)(ShipyardState.FirstVisibleRow + -4);
                    ShipyardState.SelectedSlot = -1;
                    ShipyardState.SelectedRow = -1;
                    OutfitDescText.Text = "";   // clear the desc text (src toc-0x66b9 = NUL, dumped)
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                }
                if (hitItem == 13)
                {
                    count = 0;
                    // Scans only the first half of the 128-slot AvailableRowIndex grid
                    // (matches the decompile's fixed 0x40 bound, not the array's own Count).
                    for (index = 0; index < ShipyardState.Count / 2; index = (short)(index + 1))
                    {
                        // NOTE: the decompile reads the avail-row BSS at 0x1008f87a; in this port the
                        // equivalent data lives in the managed AvailableRowIndex array.
                        if (ShipyardState.AvailableRowIndex[index] != -1)
                        {
                            count = count + 1;
                        }
                    }
                    if ((int)ShipyardState.FirstVisibleRow < (short)count + -20)
                    {
                        ShipyardState.FirstVisibleRow = (short)(ShipyardState.FirstVisibleRow + 4);
                        ShipyardState.SelectedSlot = -1;
                        ShipyardState.SelectedRow = -1;
                        OutfitDescText.Text = "";   // clear the desc text (src toc-0x66b9 = NUL, dumped)
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow));
                    }
                }
                if (hitItem == 7)
                {
                    done = true;
                }
            } while (!done);
            for (index = 0; index < 12; index = (short)(index + 1))
            {
                if (ShipyardState.Picts[index] != 0)
                {
                    MacToolbox.HPurge(ShipyardState.Picts[index]);
                    MacToolbox.ReleaseResource(ShipyardState.Picts[index]);
                }
            }
            if (ShipyardState.EscortMode != 0)
            {
                var shipyardRect = MacToolbox.GetDialogPortRect(ShipyardState.DialogWindow);
                diff = (uint)((int)shipyardRect[3] - (int)shipyardRect[1]);
                count = (int)(-(((int)diff >> 1) + (uint)(((int)diff < 0 && (diff & 1) != 0) ? 1 : 0)));
                diff = (uint)((int)shipyardRect[2] - (int)shipyardRect[0]);
                halfHeightNeg = (short)(-((short)((int)diff >> 1) + (ushort)(((int)diff < 0 && (diff & 1) != 0) ? 1 : 0)));
            }
            MacToolbox.DisposeRoutineDescriptor(routineDesc);
            MacToolbox.DisposeDialog(ShipyardState.DialogWindow);
            TitleScreenGlobals.CheatSoundPlayed = false; // **(toc-0x766c): re-arm the title cheat-chord one-shot
            if (ShipyardState.EscortMode != 0)
            {
                // *(ctx-0x7958)+0xc/e/10/12 = GlobalState.Port{Top,Left,Bottom,Right} —
                // the play-area centre point, grown back to the dialog's size below.
                var centreRect = new short[4];
                centreRect[1] = (short)((GlobalState.PortLeft + GlobalState.PortRight) / 2);
                centreRect[0] = (short)((GlobalState.PortTop + GlobalState.PortBottom) / 2);
                centreRect[2] = centreRect[0];
                centreRect[3] = centreRect[1];
                MacToolbox.InsetRect(centreRect, (short)(count + -1), (short)(halfHeightNeg + -1));
                SetGamePortAndDevice.Run();
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(centreRect);
                RefreshStatusPanel.Run();
                DispatchPendingChatter.Run(0);
            }
            GWorldPort.SetActivePortScratch();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(GlobalState.ScratchStageRect);   // *(ctx-0x7958)+0x44
            RefreshStatusPanel.Run();
            SetGamePortAndDevice.Run();
            RepaintGameWindow.Run();
            GameData.Ships[0].NavTargetSpob = savedShip;
            WorldState.SpawnPulseDirty = 1;
        }
        return;
    }
}
