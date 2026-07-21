using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1004fa88 (EV Override-11.c lines 32598-32793) — the ACTIVE-MISSIONS info
// dialog (DLOG 0x3f4 = 1012): lists the up-to-8 active missions with their
// descriptions. Items:
//   1 leave    5 abort the selected mission (undoing a mission-granted outfit
//     and the flag-0x40 reputation gain, then rebuilding the list; closes when
//     no missions remain)    6 show the destination on the galaxy map (gated on
//     no single-mission dialog being open).
// Opened from the hub's item 0xe (when any mission is active), the BBS and the
// other sub-dialogs' missions button.
public static class RunMissionInfoDialog
{
    // Port bridge for the modal-filter UPP (cell 0x1008112c -> FUN_10050230) —
    // typed MacEvent shape (dialog 4-rules B7).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = Mission.MissionSelectDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static void Run()
    {
        bool done = false;
        short hitItem = default;

        int filterUpp = MacToolbox.NewRoutineDescriptor(MissionInfoGlobals.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[1], 1, 0x80, 0x80);
        MissionInfoGlobals.Picts[0] = MacToolbox.GetPicture(0x1b62);
        MissionInfoGlobals.Picts[1] = MacToolbox.GetPicture(0x1b63);
        MissionInfoGlobals.Picts[2] = MacToolbox.GetPicture(0x1bb4);
        MissionInfoGlobals.Picts[3] = MacToolbox.GetPicture(0x1bb5);
        MissionInfoGlobals.DialogWindow = 0;
        MissionInfoGlobals.DialogWindow = MacToolbox.GetNewDialog(0x3f4, 0, -1);
        if (MissionInfoGlobals.DialogWindow != 0)
        {
            NewDialogHook.Run(MissionInfoGlobals.DialogWindow, 0);
            Graphics.RecenterWindowIntoPlayArea.Run(MissionInfoGlobals.DialogWindow);
            MacToolbox.ShowWindow(MissionInfoGlobals.DialogWindow);
            MacToolbox.SelectWindow(MissionInfoGlobals.DialogWindow);
            MacToolbox.SetPort(MissionInfoGlobals.DialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionInfoGlobals.DialogWindow));
            Mission.BuildMissionsListBox.Run();
            MissionInfoGlobals.SelectedRow = 0;
            TextScratch.Text = Text.LoadDescriptionText.Load(
                GameData.Missions[MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow]].MissionInfoText);
            Mission.SubstituteMissionDescTags.Run(0, MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow]);
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref hitItem);
                if (hitItem == 1)
                {
                    done = true;
                }
                if (hitItem == 6 && MissionBoardGlobals.DialogWindow == 0)
                {
                    GalaxyMapState.PreviewSystem = -1;
                    GalaxyMapGlobals.MissionsDirty = 0;
                    // NOTE (original-game quirk kept, OGB-14): this flag test indexes the
                    // mission-detail table by the RAW selected ROW, not via the row->slot map
                    // like every other read in the function.
                    if (MissionInfoGlobals.SelectedRow != -1 &&
                        (GameData.Missions[MissionInfoGlobals.SelectedRow].Flags & MisnFlags.ShowGreenArrowInBrief) != 0)
                    {
                        var selGovt = GameData.Missions[MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow]];
                        if (selGovt.TargetSpob == -1)
                        {
                            if (selGovt.ReturnSpob != -1)
                            {
                                GalaxyMapState.PreviewSystem = GameData.Spobs[selGovt.ReturnSpob].System;
                            }
                        }
                        else
                        {
                            GalaxyMapState.PreviewSystem = GameData.Spobs[selGovt.TargetSpob].System;
                        }
                    }
                    // +0x2c is NavTargetSpob, NOT NavMode (+0x2a) — don't conflate the two fields here.
                    short savedNavTarget = GameData.Ships[0].NavTargetSpob;
                    RunGalaxyMapDialog.Run();
                    if (MissionBoardGlobals.DialogWindow != 0)
                    {
                        GameData.Ships[0].NavTargetSpob = savedNavTarget;
                    }
                    if (SpaceportGlobals.DialogWindow != 0)
                    {
                        MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                        RedrawSpaceportDialog.Run();
                    }
                    GalaxyMapState.PreviewSystem = -1;
                    GalaxyMapGlobals.MissionsDirty = 1;
                    MacToolbox.SetPort(MissionInfoGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionInfoGlobals.DialogWindow));
                    Mission.RedrawMissionSelectDialog.Run();
                }
                if (hitItem == 5)
                {
                    if (MissionInfoGlobals.SelectedRow == -1)
                    {
                        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[3], 1, 0x80, 0x80);
                        DrawMissionInfoButtonRow.Run(-1);
                    }
                    else
                    {
                        var mission = GameData.Missions[MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow]];
                        // Mission-granted outfit removal: Pay < -30127 encodes
                        // -(30128 + outfitIdx); aborting clears the owned count.
                        if ((mission.Flags & MisnFlags.RemoveGrantedOutfitOnAbort) != 0 && mission.Pay < -0x75af)
                        {
                            double payAbs = EvoMath.EvMath.FloatAbs((double)(float)(double)mission.Pay);
                            short outfitIdx = (short)(int)(-30128.0f + payAbs);
                            if (-1 < outfitIdx && outfitIdx < 0x80 && 0 < OwnedOutfitGrid.Store[outfitIdx])
                            {
                                OwnedOutfitGrid.Store[outfitIdx] = 0;
                                Outfit.RebuildMarketFromOwnedOutfits.Run();
                            }
                        }
                        // Undo the flag-0x40 reputation gain across the govt's systems.
                        if ((mission.Flags & MisnFlags.RemoveReputationOnAbort) != 0 && mission.CargoType != -1)
                        {
                            for (short i = 0; i < SystTable.Count; i = (short)(i + 1))
                            {
                                if (SystTable.Store[i].Govt == mission.CargoType)
                                {
                                    GalaxyMapGlobals.SetSystemStatus(i,
                                        (short)(GalaxyMapGlobals.SystemStatus(i) + mission.CargoQty * -5));
                                }
                            }
                        }
                        Mission.AbortMission.Run(MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow]);
                        short activeMissions = 0;
                        for (short j = 0; j < MissionStateTable.Count; j = (short)(j + 1))
                        {
                            if (GameData.MissionStates[j].IsActive != 0)
                            {
                                activeMissions = (short)(activeMissions + 1);
                            }
                        }
                        if (activeMissions < 1)
                        {
                            done = true;
                        }
                        else
                        {
                            if (MissionInfoGlobals.ListHandle != 0)
                            {
                                MacToolbox.LDispose(MissionInfoGlobals.ListHandle);
                            }
                            Mission.BuildMissionsListBox.Run();
                            MissionInfoGlobals.SelectedRow = -1;
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionInfoGlobals.DialogWindow));
                            for (short listRow = 0; listRow < MacToolbox.LGetRowCount(MissionInfoGlobals.ListHandle); listRow = (short)(listRow + 1))
                            {
                                MacToolbox.LSetSelect(0, listRow << 16, MissionInfoGlobals.ListHandle);
                            }
                            for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
                            {
                                MissionInfoGlobals.RowToMissionSlot[i] = -1;
                            }
                            short row = 0;
                            for (short j = 0; j < MissionStateTable.Count; j = (short)(j + 1))
                            {
                                if (GameData.MissionStates[j].IsActive != 0)
                                {
                                    MissionInfoGlobals.RowToMissionSlot[row] = j;
                                    row = (short)(row + 1);
                                }
                            }
                        }
                        Graphics.RedrawHudStatusPanel.Run();
                        MacToolbox.SetPort(MissionInfoGlobals.DialogWindow);
                    }
                }
            } while (!done);
            if (MissionInfoGlobals.ListHandle != 0)
            {
                MacToolbox.LDispose(MissionInfoGlobals.ListHandle);
            }
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(MissionInfoGlobals.DialogWindow);
            MissionInfoGlobals.DialogWindow = 0;
            Graphics.RepaintGameWindow.Run();
            for (short i = 0; i < MissionInfoGlobals.Picts.Length; i = (short)(i + 1))
            {
                if (MissionInfoGlobals.Picts[i] != 0)
                {
                    MacToolbox.HPurge(MissionInfoGlobals.Picts[i]);
                    MacToolbox.ReleaseResource(MissionInfoGlobals.Picts[i]);
                }
            }
            Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[2], 1, 0x80, 0x80);
        }
    }
}
