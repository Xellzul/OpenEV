using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10036e74 (EV Override-11.c lines 22468-22730) — the spaceport HUB dialog
// (DLOG 1000): the planet-landed screen with the spob picture, description and
// the 7-button tab bar (leave / refuel / trade / outfitter / shipyard /
// mission BBS / bar). Items:
//   12 leave   2 player-info   3 redraw game screen   14 active-missions info
//   4 refuel   5 ambient snd   7 trade (Exchange)      8 outfitter (Outfitter)
//   9 shipyard (Shipyard)      10 mission BBS (*BbsEnabled)  11 bar (Bar)
// All the spaceport ptr-cell globals live in SpaceportGlobals.
public static class RunSpaceportDialog
{
    // Port bridge for the modal-filter UPP: the original kept FUN_100377d8's
    // PEF-relocated address in cell 0x10081058 and ModalDialog called it for
    // every event; the port's ModalDialog dispatches the delegate registered under
    // the proc key (key/update capture — mouse hits go through the native
    // DITL hit-test). The filter's item-hit out lands in evt.ItemHit
    // (FilterAdapter uses a local itemHit and writes it back via evt.ItemHit).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = SpaceportFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static void Run(short spobIndex)
    {
        bool done = false;
        short hitItem = default;
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        int filterUpp = MacToolbox.NewRoutineDescriptor(SpaceportGlobals.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        GalaxyMapState.TradeKeyLock = 1;
        Systems.Model.CurrentSpob.Index = spobIndex;
        SpaceportGlobals.ShipPurchased = 0;
        SpaceportGlobals.LoadoutChanged = 0;
        // strncpy-then-overwrite collapsed to one assignment (final value identical).
        SpaceportGlobals.Description = Text.LoadDescriptionText.Load((short)(spobIndex + 128));
        for (short index = 0; index < SpaceportGlobals.ShopPriceScale.Length; index = (short)(index + 1))
        {
            SpaceportGlobals.ShopPriceScale[index] = SpaceportGlobals.DefaultShopPriceScale;
        }
        var spob = Core.Model.GameData.Spobs[spobIndex];
        if (spob.CustomPicId < 128)
        {
            SpaceportGlobals.SpobPictId = (short)(spob.SpriteId + 10000);
        }
        else
        {
            SpaceportGlobals.SpobPictId = spob.CustomPicId;
        }
        if (spob.CustomSoundId < 128)
        {
            SpaceportGlobals.AmbientSndHandle = 0;
        }
        else
        {
            SpaceportGlobals.AmbientSndHandle = Sound.LoadSndResource.Run(spob.CustomSoundId);
            SpaceportGlobals.AmbientTimer = 0;
        }
        SpaceportGlobals.SpobPictHandle = 0;
        SpaceportGlobals.SpobPictHandle = MacToolbox.GetPicture(SpaceportGlobals.SpobPictId);
        for (short index = 0; index < 10; index = (short)(index + 1))
        {
            SpaceportGlobals.TabPicts[index] = MacToolbox.GetPicture(index + 7014);
        }
        SpaceportGlobals.TabPicts[10] = MacToolbox.GetPicture(7032);
        SpaceportGlobals.TabPicts[11] = MacToolbox.GetPicture(7033);
        SpaceportGlobals.TabPicts[12] = MacToolbox.GetPicture(7090);
        SpaceportGlobals.TabPicts[13] = MacToolbox.GetPicture(7091);
        SpaceportGlobals.DialogWindow = 0;
        SpaceportGlobals.DialogWindow = MacToolbox.GetNewDialog(1000, 0, -1);
        if (SpaceportGlobals.DialogWindow != 0)
        {
            NewDialogHook.Run(SpaceportGlobals.DialogWindow, 0);
            Graphics.RecenterWindowIntoPlayArea.Run(SpaceportGlobals.DialogWindow);
            MacToolbox.ShowWindow(SpaceportGlobals.DialogWindow);
            MacToolbox.SelectWindow(SpaceportGlobals.DialogWindow);
            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
            if ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Uninhabited) == 0)
            {
                SpaceportGlobals.MissionBbsEnabled = 1;
            }
            else
            {
                SpaceportGlobals.MissionBbsEnabled = 0;
            }
            Graphics.Model.GWorldPort.SetActivePortSecondaryGame();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(Core.Model.GlobalState.PrimaryStageRect);
            Graphics.SetGamePortAndDevice.Run();
            Graphics.RepaintGameWindow.Run();
            Graphics.DrawRadarHud.Run(1);
            Graphics.RedrawHudStatusPanel.Run();
            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
            RedrawSpaceportDialog.Run();
            Misc.TickGovtEncounters.Run();
            Misc.RecomputeWorldVisibility.Run();
            Graphics.RedrawHudStatusPanel.Run();
            RedrawSpaceportDialog.Run();
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref hitItem);
                if (hitItem == 12)
                {
                    done = true;
                }
                if (hitItem == 2)
                {
                    RunPlayerInfoDialog.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if (hitItem == 3)
                {
                    short savedNavTarget = Core.Model.GameData.Ships[0].NavTargetSpob;
                    RunGalaxyMapDialog.Run();
                    Core.Model.GameData.Ships[0].NavTargetSpob = savedNavTarget;
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if (hitItem == 14)
                {
                    short activeMissions = 0;
                    for (short index = 0; index < Mission.Model.MissionStateTable.Count; index = (short)(index + 1))
                    {
                        if (Core.Model.GameData.MissionStates[index].IsActive != 0)
                        {
                            activeMissions = (short)(activeMissions + 1);
                        }
                    }
                    if (0 < activeMissions)
                    {
                        RunMissionInfoDialog.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                    }
                }
                if (hitItem == 4)
                {
                    if ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Uninhabited) == 0)
                    {
                        short fuelMax = (short)Ship.ShipDerivedStats.EffectiveFuelMax(Ship.Model.ShipTable.Player);
                        short refuel = (short)(int)((float)fuelMax - Core.Model.GameData.Ships[0].Fuel);
                        if (Systems.Model.CurrentSpob.Rec.TradingEnabled == 0)   // TradingEnabled == 0: fuel costs credits
                        {
                            if (Core.Model.GameData.Ships[0].Credits < refuel)
                            {
                                refuel = (short)Core.Model.GameData.Ships[0].Credits;
                            }
                            Core.Model.GameData.Ships[0].Credits = Core.Model.GameData.Ships[0].Credits - refuel;
                        }
                        Core.Model.GameData.Ships[0].Fuel = Core.Model.GameData.Ships[0].Fuel + (float)refuel;
                        Core.Model.WorldState.ShieldEnergyBarDirty = 1;
                        Core.Model.WorldState.HudStatusPanelDirty = 1;
                        Graphics.TickHudRedrawScheduler.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    }
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if ((hitItem == 5) && (SpaceportGlobals.AmbientSndHandle != 0))
                {
                    Sound.SndPlay.Run(SpaceportGlobals.AmbientSndHandle, 10, 128, 128);
                }
                if ((hitItem == 7) && ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Exchange) != 0))
                {
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    Graphics.DrawSpaceportTabBar.Run(-8);
                    Outfit.ShowCommodityExchangeDialog.Run();
                    Core.Model.WorldState.HudStatusPanelDirty = 1;
                    Graphics.TickHudRedrawScheduler.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if ((hitItem == 8) && ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Outfitter) != 0))
                {
                    MacToolbox.GetDialogItem(SpaceportGlobals.DialogWindow, 8, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    RedrawSpaceportDialog.Run();
                    Misc.AdvanceLoadout.Run();
                    Core.Model.WorldState.HudStatusPanelDirty = 1;
                    Graphics.TickHudRedrawScheduler.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if ((hitItem == 9) && ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Shipyard) != 0))
                {
                    MacToolbox.GetDialogItem(SpaceportGlobals.DialogWindow, 9, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    RedrawSpaceportDialog.Run();
                    Outfit.Model.ShipyardState.EscortMode = 0;
                    RunShipyardDialog.Run();
                    Core.Model.WorldState.HudStatusPanelDirty = 1;
                    Graphics.TickHudRedrawScheduler.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                }
                if ((hitItem == 10) && (SpaceportGlobals.MissionBbsEnabled != 0))
                {
                    short savedNavTarget = Core.Model.GameData.Ships[0].NavTargetSpob;
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.GetDialogItem(SpaceportGlobals.DialogWindow, 10, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    RedrawSpaceportDialog.Run();
                    RunMissionBbsDialog.Run((char)0);
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                    Core.Model.GameData.Ships[0].NavTargetSpob = savedNavTarget;
                    Core.Model.WorldState.HudStatusPanelDirty = 1;
                    Core.Model.WorldState.SpawnPulseDirty = 1;
                    Graphics.TickHudRedrawScheduler.Run();
                }
                if (hitItem == 11)
                {
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.GetDialogItem(SpaceportGlobals.DialogWindow, 11, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    RedrawSpaceportDialog.Run();
                    if ((Systems.Model.CurrentSpob.Rec.Flags & (int)SpobFlags.Bar) != 0)
                    {
                        RunSpaceportBarDialog.Run();
                        Graphics.RedrawHudStatusPanel.Run();
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(SpaceportGlobals.DialogWindow + 0x10);
                    }
                }
            } while (!done);
            for (short index = 0; index < SpaceportGlobals.TabPicts.Length; index = (short)(index + 1))
            {
                if (SpaceportGlobals.TabPicts[index] != 0)
                {
                    MacToolbox.HPurge(SpaceportGlobals.TabPicts[index]);
                    MacToolbox.ReleaseResource(SpaceportGlobals.TabPicts[index]);
                }
            }
            if (SpaceportGlobals.SpobPictHandle != 0)
            {
                MacToolbox.HPurge(SpaceportGlobals.SpobPictHandle);
                MacToolbox.ReleaseResource(SpaceportGlobals.SpobPictHandle);
            }
            if (SpaceportGlobals.AmbientSndHandle != 0)
            {
                Sound.FlushMixQueueEntries.Run(SpaceportGlobals.AmbientSndHandle);
                MacToolbox.DisposePtr(SpaceportGlobals.AmbientSndHandle);
            }
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(SpaceportGlobals.DialogWindow);
            SpaceportGlobals.DialogWindow = 0;
            if (SpaceportGlobals.LoadoutChanged != 0)
            {
                Combat.TickWorldDailyEvents.Run();
            }
            if (SpaceportGlobals.ShipPurchased != 0)
            {
                for (short index = 0; index < 4; index = (short)(index + 1))
                {
                    Combat.TickWorldDailyEvents.Run();
                }
            }
            if ((SpaceportGlobals.LoadoutChanged != 0) || (SpaceportGlobals.ShipPurchased != 0))
            {
                Core.Model.GameData.Ships[0].TargetSlot = -1;
                Core.Model.WorldState.HudWeaponPanelDirty = 1;
            }
            GalaxyMapState.TradeKeyLock = 0;
            Graphics.RepaintGameWindow.Run();
        }
    }
}
