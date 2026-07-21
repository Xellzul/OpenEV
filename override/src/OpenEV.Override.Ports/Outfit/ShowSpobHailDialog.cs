using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Outfit;

// FUN_10010f70 (EV Override-11.c lines 8762-9154) — the SPOB HAIL dialog
// (DLOG 1009 / 0x3f1): the comm dialog opened by hailing the nav-target spob
// itself (as opposed to hailing a ship, which goes through SpaceportPersonDialog).
// Covers greet / request clearance / pay a bribe while hostile, and the planet
// DOMINATION / tribute path. Opened by PlayerHailAction: rolls the tribute demand
// (rng(credits×1e-6)×1000 + 3000, ×1.5 for a proud govt, capped at credits/3,
// clamped 1000..900000), loads the comm button PICTs + the spob hail text
// (STR 7000+SpriteId or STR# 1100), then runs the modal loop — item 1 = leave,
// item 2 = greet (or request clearance / bribe while hostile), item 3 = demand
// tribute (combat rating >= 12800 dominates the spob and spawns its defense
// fleet) or release a dominated spob. Filter = TradeDockRefuelDialogFilter
// (FUN_100120cc).
//
// DEVIATION (faithful): the legacy `Run(params object?[])` absorber overload is DELETED —
//   the only caller (Misc.PlayerHailAction) passed an argument, bound the absorber, and
//   the dominate dialog NEVER OPENED.
// DEVIATION (faithful): the modal filter was never RegisterModalFilter'd before this batch
//   (the raw UPP cell 0x10080d28 was passed through unregistered) — the dialog's key
//   shortcuts / click zones / update redraw were all dead.
public static class ShowSpobHailDialog
{
    // Bridges the ref-param TradeDockRefuelDialogFilter.Run signature to the
    // (dialog, evt) -> int delegate RegisterModalFilter expects.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = TradeDockRefuelDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static void Run()
    {
        short itemHit = 0;

        int modalFilterUpp = MacToolbox.NewRoutineDescriptor(DialogScratch.DominateFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(DialogScratch.DominateFilterProc, FilterAdapter);
        bool done = false;
        if (GameData.Player.NavTargetSpob != -1)
        {
            int navSpob = GameData.Player.NavTargetSpob;
            if (((SpobFlags)GameData.Spobs[navSpob].Flags & SpobFlags.Uninhabited) == 0)
            {
                DialogScratch.SpaceportGreetIndex = (short)SeedEvoRng.Run(5);
                DialogScratch.SpaceportSelCellB = -1;
                DialogScratch.SpaceportSelCellA = -1;
                DialogScratch.DialogShipPtr = 0;
                // Tribute demand: rng(credits * 1e-6) * 1000 + 3000 (dumped ASM constant 1e-06).
                int demand = (int)(1e-06 * (double)GameData.Player.Credits);
                if ((short)demand < 1)
                {
                    demand = 1;
                }
                GameData.BribeFine = (short)SeedEvoRng.Run((short)demand) * 1000 + 3000;
                // Proud govt (HighBribeDemands): demand x1.5 (dumped ASM constant 1.5).
                if ((GameData.Spobs[navSpob].Govt != -1) &&
                   ((GameData.Governments[GameData.Spobs[navSpob].Govt].Flags & GovtFlags.HighBribeDemands) != 0))
                {
                    GameData.BribeFine = (int)((double)GameData.BribeFine * 1.5);
                }
                // Cap the demand at a third of the player's credits (dumped ASM constant 0.333).
                if (0.333 * (double)GameData.Player.Credits <
                    (double)GameData.BribeFine)
                {
                    GameData.BribeFine = (int)(0.333 * (double)GameData.Player.Credits);
                }
                GameData.BribeFine = (GameData.BribeFine / 1000) * 1000;
                if (900000 < GameData.BribeFine)
                {
                    GameData.BribeFine = 900000;
                }
                if (GameData.BribeFine < 1000)
                {
                    GameData.BribeFine = 1000;
                }
                DialogScratch.SpaceportFlag = 0;
                DialogScratch.CommButtonPicts[0] = MacToolbox.GetPicture(7034);
                DialogScratch.CommBtnPictB2Sel = MacToolbox.GetPicture(7035);
                DialogScratch.CommBtnPictB1Act = MacToolbox.GetPicture(7036);
                DialogScratch.CommBtnPictB1ActSel = MacToolbox.GetPicture(7037);
                DialogScratch.CommBtnPictB1 = MacToolbox.GetPicture(7042);
                DialogScratch.CommBtnPictB1Sel = MacToolbox.GetPicture(7043);
                DialogScratch.CommBtnPictB2Act = MacToolbox.GetPicture(7084);
                DialogScratch.CommBtnPictB2ActSel = MacToolbox.GetPicture(7085);
                DialogScratch.CommBtnPictHail0 = MacToolbox.GetPicture(7068);
                DialogScratch.CommBtnPictHail1 = MacToolbox.GetPicture(7069);
                string? hailDesc = TryLoadStr.RunString((short)(GameData.Spobs[navSpob].SpriteId + 7000));
                DialogScratch.SpaceportDescText = hailDesc
                    ?? MacToolbox.GetIndString(1100, (short)(GameData.Spobs[navSpob].SpriteId + 1));
                SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                MacToolbox.ShowCursor();
                DialogScratch.SpaceportCommDialogRecord = 0;
                DialogScratch.SpaceportCommDialogRecord = MacToolbox.GetNewDialog(1009, 0, -1);
                if (DialogScratch.SpaceportCommDialogRecord != 0)
                {
                    // Per-system status/coolness vs the spob's MinCoolness — true = the
                    // player is NOT cleared to land.
                    DialogScratch.CommHailGateFlag = (byte)(GalaxyMapGlobals.SystemStatus(GameData.Player.CurrentSystem) <
                                   GameData.Spobs[navSpob].MinCoolness ? (byte)1 : (byte)0);
                    DialogScratch.SpaceportCanBribeFlag = 0;
                    if (DialogScratch.SpaceportBribeRoll < 0)
                    {
                        DialogScratch.SpaceportBribeRoll = (short)SeedEvoRng.Run(100);
                    }
                    if (30 < DialogScratch.SpaceportBribeRoll)
                    {
                        if (GameData.Spobs[navSpob].Govt == -1)
                        {
                            DialogScratch.SpaceportCanBribeFlag = 1;
                        }
                        else if ((GameData.Governments[GameData.Spobs[navSpob].Govt].Flags & GovtFlags.PlanetsTakeBribes) != 0)
                        {
                            DialogScratch.SpaceportCanBribeFlag = 1;
                        }
                    }
                    if ((GameData.Spobs[navSpob].Govt != -1) &&
                       ((GameData.Governments[GameData.Spobs[navSpob].Govt].Flags & GovtFlags.HighBribeDemands) != 0))
                    {
                        DialogScratch.SpaceportCanBribeFlag = 1;
                    }
                    if (((SpobFlags)GameData.Spobs[navSpob].Flags & SpobFlags.Uninhabited) == 0)
                    {
                        if ((DialogScratch.CommHailGateFlag == 0) ||
                           (GameData.Spobs[navSpob].TradingEnabled != 0))
                        {
                            LoadIndexedRebellionString.Run(0);
                            // FUN_10076178/p2cstr/FUN_100761bc/c2pstr chain rebuilt as managed
                            // string concatenation (greeting + spob name + ".").
                            DialogScratch.SpaceportHailText = TextScratch.Trunc(
                                DialogScratch.SpaceportHailText + GameData.Spobs[navSpob].Name + ".", 254);
                        }
                        else
                        {
                            LoadIndexedSpobString.Run(2);
                        }
                    }
                    else
                    {
                        LoadIndexedSpobString.Run(1);
                    }
                    int pictHandle = MacToolbox.GetPicture(5500);
                    if (pictHandle != 0)
                    {
                        // picFrame Rect off the PICT header: *(int *)(*handle + 2) = {top,left},
                        // *(int *)(*handle + 6) = {bottom,right} (handle -> master ptr -> bytes).
                        int picFrameTopLeft = MacToolbox.ReadResourceInt(pictHandle, 2);
                        int picFrameBotRight = MacToolbox.ReadResourceInt(pictHandle, 6);
                        short frameLeft = (short)picFrameTopLeft;
                        short frameTop = (short)((uint)picFrameTopLeft >> 16);
                        var picRect = new short[4];
                        picRect[0] = frameTop;
                        picRect[1] = frameLeft;
                        picRect[2] = (short)((uint)picFrameBotRight >> 16);
                        picRect[3] = (short)picFrameBotRight;
                        MacToolbox.OffsetRect(picRect, (short)-frameLeft, (short)-frameTop);   // normalize to (0,0)
                        GWorldPort.SetActivePortScratch();
                        MacToolbox.ForeColor(QuickDrawColor.Black);
                        MacToolbox.DrawPicture(pictHandle, picRect);
                        SetGamePortAndDevice.Run();
                        MacToolbox.ReleaseResource(pictHandle);
                    }
                    NewDialogHook.Run(DialogScratch.SpaceportCommDialogRecord, 0);
                    RecenterWindowIntoPlayArea.Run(DialogScratch.SpaceportCommDialogRecord);
                    MacToolbox.ShowWindow(DialogScratch.SpaceportCommDialogRecord);
                    MacToolbox.SelectWindow(DialogScratch.SpaceportCommDialogRecord);
                    MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                    MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                    do
                    {
                        MacToolbox.ModalDialog(modalFilterUpp, ref itemHit);
                        if (itemHit == 1)
                        {
                            done = true;
                        }
                        if ((itemHit == 2) || (itemHit == 3))
                        {
                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                            RenderPlanetCommButtonRow.Run(-1);
                            LoadIndexedSpobString.Run(1);
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                            if (itemHit == 2)
                            {
                                if ((DialogScratch.CommHailGateFlag == 0) ||
                                   (GameData.Spobs[navSpob].TradingEnabled != 0)
                                   )
                                {
                                    if (DialogScratch.CommHailGateFlag == 0)
                                    {
                                        LoadIndexedSpobString.Run(9);
                                    }
                                    else
                                    {
                                        LoadIndexedRebellionString.Run(4);
                                    }
                                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                                }
                                else if (DialogScratch.SpaceportCanBribeFlag == 0)
                                {
                                    LoadIndexedRebellionString.Run(9);
                                    DrawShipInfoPanel.Run();
                                }
                                else
                                {
                                    LoadIndexedRebellionString.Run(8);
                                    DrawShipInfoPanel.Run();
                                    GameData.BuyShipPriceCell = GameData.BribeFine;
                                    short confirmResult = ShowBuyShipDialog.Run(0, 50);
                                    SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                                    if (GameData.Player.Credits < GameData.BuyShipPriceCell)
                                    {
                                        LoadIndexedRebellionString.Run(4);
                                        DrawShipInfoPanel.Run();
                                    }
                                    else if (confirmResult == 1)
                                    {
                                        WorldState.LandingApproachState = 751;
                                        WorldState.LandingTargetSpob = GameData.Player.NavTargetSpob;
                                        GameData.Player.AiActionTimer = 0;
                                        // FUN_1007615c/p2cstr/FUN_100761bc/c2pstr chain rebuilt as a managed chatter line.
                                        string chatter = PilotIdentity.ShipName + ", you are ";
                                        if (((SpobFlags)GameData.Spobs[navSpob].Flags & SpobFlags.Station) == 0)
                                        {
                                            chatter = chatter + "cleared to land. Commence final approach.";
                                        }
                                        else
                                        {
                                            chatter = chatter + "cleared to dock. Commence final approach.";
                                        }
                                        EnqueueChatterEvent.Run(chatter, 250, 0, 12, UiColors.ChatterText, 0, 0);
                                        GameData.Player.Credits = (GameData.Player.Credits - GameData.BuyShipPriceCell);
                                        WorldState.HudStatusPanelDirty = 1;
                                        DialogScratch.SpaceportCanBribeFlag = 0;
                                        done = true;
                                    }
                                    else
                                    {
                                        DialogScratch.SpaceportCanBribeFlag = 0;
                                        DialogScratch.SpaceportBribeRoll = 0;
                                        GameData.BribeFine = GameData.BribeFine + 1000;
                                        LoadIndexedRebellionString.Run(6);
                                        DrawShipInfoPanel.Run();
                                    }
                                }
                            }
                            if (itemHit == 3)
                            {
                                if (GameData.Spobs[navSpob].TradingEnabled == 0)
                                {
                                    DialogScratch.CommHailGateFlag = 1;
                                    if (GameData.Spobs[navSpob].MinCoolness <=
                                        GalaxyMapGlobals.SystemStatus(GameData.Player.CurrentSystem))
                                    {
                                        GalaxyMapGlobals.SetSystemStatus(GameData.Player.CurrentSystem,
                                             (short)(GameData.Spobs[navSpob].MinCoolness + -1));
                                    }
                                    FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem,
                                                             GameData.Spobs[navSpob].Govt, 3, -1);
                                    if (WorldState.PlayerCombatRating < 12800)
                                    {
                                        MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                        LoadIndexedRebellionString.Run(1);
                                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                                    }
                                    else
                                    {
                                        for (short i = 0; i < 5; i = (short)(i + 1))
                                        {
                                            FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem,
                                                                     GameData.Spobs[navSpob].Govt, 3, -1);
                                        }
                                        if ((DialogScratch.SpaceportFlag == 0) &&
                                            (GameData.Spobs[navSpob].Tribute < 1) &&
                                            !ShipDerivedStats.AnyShipDefendingSpob(GameData.Player.NavTargetSpob))
                                        {
                                            if (((SpobFlags)GameData.Spobs[navSpob].Flags & SpobFlags.Station) == 0)
                                            {
                                                DialogScratch.SpaceportHailText = MacToolbox.GetIndString(3002, 26);
                                            }
                                            else
                                            {
                                                DialogScratch.SpaceportHailText = MacToolbox.GetIndString(3002, 27);
                                            }
                                            WorldState.LandingApproachState = 0;
                                            WorldState.LandingTargetSpob = -1;
                                            // Mission slot 510 available flag (PersRecord offset verified: 510*0x1c0+0x19e == 0x37e1e).
                                            GameData.Pers[510].AvailableFlag = 1;
                                            GameData.Spobs[navSpob].TradingEnabled = 1;
                                            GameData.Spobs[navSpob].TributeAccrualTicks = 0;
                                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                                        }
                                        else
                                        {
                                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                            LoadIndexedRebellionString.Run(2);
                                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                                            if (!ShipDerivedStats.AnyShipDefendingSpob(GameData.Player.NavTargetSpob))
                                            {
                                                short defendersToSpawn;
                                                if (GameData.Spobs[navSpob].TributeMax < 1001)
                                                {
                                                    defendersToSpawn = GameData.Spobs[navSpob].Tribute;
                                                    GameData.Spobs[navSpob].Tribute = 0;
                                                }
                                                else
                                                {
                                                    GameData.Spobs[navSpob].Tribute =
                                                         (short)(GameData.Spobs[navSpob].Tribute -
                                                         GameData.Spobs[navSpob].TributeMax % 10);
                                                    defendersToSpawn = (short)(GameData.Spobs[navSpob].TributeMax % 10);
                                                    if (GameData.Spobs[navSpob].Tribute < 0)
                                                    {
                                                        defendersToSpawn = (short)(defendersToSpawn + GameData.Spobs[navSpob].Tribute);
                                                        GameData.Spobs[navSpob].Tribute = 0;
                                                    }
                                                }
                                                for (short i = 0; i < defendersToSpawn; i = (short)(i + 1))
                                                {
                                                    SpawnGovtDefender.Run(GameData.Player.NavTargetSpob);
                                                }
                                            }
                                        }
                                    }
                                    DialogScratch.SpaceportFlag = 1;
                                }
                                else
                                {
                                    if (((SpobFlags)GameData.Spobs[navSpob].Flags & SpobFlags.Station) == 0)
                                    {
                                        DialogScratch.SpaceportHailText = MacToolbox.GetIndString(3002, 36);
                                    }
                                    else
                                    {
                                        DialogScratch.SpaceportHailText = MacToolbox.GetIndString(3002, 37);
                                    }
                                    WorldState.LandingApproachState = 0;
                                    WorldState.LandingTargetSpob = -1;
                                    GameData.Spobs[navSpob].TradingEnabled = 0;
                                    if (GameData.Spobs[navSpob].TributeMax < 1001)
                                    {
                                        GameData.Spobs[navSpob].Tribute = GameData.Spobs[navSpob].TributeMax;
                                    }
                                    else if (GameData.Spobs[navSpob].TributeMax < 10001)
                                    {
                                        GameData.Spobs[navSpob].Tribute =
                                             (short)(GameData.Spobs[navSpob].TributeMax / 10 + -100);
                                    }
                                    else
                                    {
                                        GameData.Spobs[navSpob].Tribute =
                                             (short)(GameData.Spobs[navSpob].TributeMax / 10 + -1000);
                                    }
                                    if (GameData.Spobs[navSpob].MinCoolness <=
                                        GalaxyMapGlobals.SystemStatus(GameData.Player.CurrentSystem))
                                    {
                                        GalaxyMapGlobals.SetSystemStatus(GameData.Player.CurrentSystem,
                                             (short)(GameData.Spobs[navSpob].MinCoolness + -1));
                                    }
                                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                                }
                            }
                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                            var itemType = new short[1];
                            var itemHandle = new int[1];
                            var itemRect = new short[4];
                            MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 4, itemType, itemHandle, itemRect);
                            MacToolbox.InvalRect(itemRect);
                            MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 6, itemType, itemHandle, itemRect);
                            MacToolbox.InvalRect(itemRect);
                        }
                    } while (!done);
                    MacToolbox.HideCursor();
                    foreach (int pict in DialogScratch.CommButtonPicts)
                    {
                        if (pict != 0)
                        {
                            MacToolbox.HPurge(pict);
                            MacToolbox.ReleaseResource(pict);
                        }
                    }
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                    // (decompile folds a dead portRect half-HEIGHT calc (local_62, never read) and a
                    // WIDTH calc into a fake 2nd DisposeRoutineDescriptor arg — both dropped.)
                    MacToolbox.DisposeRoutineDescriptor(modalFilterUpp);
                    MacToolbox.DisposeDialog(DialogScratch.SpaceportCommDialogRecord);
                    GWorldPort.SetActivePortScratch();
                    MacToolbox.ForeColor(QuickDrawColor.Black);
                    MacToolbox.PaintRect(new short[] { GlobalState.WindowBoundsTop, GlobalState.WindowBoundsLeft, GlobalState.WindowBoundsBottom, GlobalState.WindowBoundsRight });
                    SetGamePortAndDevice.Run();
                    RepaintGameWindow.Run();
                    RefreshStatusPanel.Run();
                    DispatchPendingChatter.Run(0);
                }
            }
            else
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
            }
        }
        return;
    }
}
