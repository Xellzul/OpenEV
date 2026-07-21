using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10047814 (EV Override-11.c lines 29834-30067) — the MISSION BBS dialog
// (DLOG 0x3ee = 1006): the mission board listing the available 'bär' missions
// for the current spob (inBar selects which MissionAvailGrid half drives the
// list). Items:
//   1 accept the selected mission (AcceptMission; closes on success)
//   7 leave    6 show destination on the galaxy map    10 active-missions
//   info    9 player info
// Bails with an alert when all 8 mission slots are taken or nothing is
// available here. Opened from the hub's item 10.
public static class RunMissionBbsDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10081148 -> FUN_1004cad0) —
    // typed MacEvent shape (dialog 4-rules B7).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = Mission.MissionBbsDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    private static short SelectedPers()
        => MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][SpaceportGlobals.BbsSelectedRow];

    private static void RebuildSelectedMissionText()
    {
        TextScratch.Text = Text.LoadDescriptionText.Load((short)(SelectedPers() + 4000));
        Mission.SubstituteMissionDescTags.Run(1, SelectedPers());
    }

    public static void Run(char inBar)
    {
        char done = (char)0;
        short hitItem = default;

        int filterUpp = MacToolbox.NewRoutineDescriptor(MissionBoardGlobals.BbsFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        if (inBar == 0)
        {
            SpaceportGlobals.InBarFlag = 0;
        }
        else
        {
            SpaceportGlobals.InBarFlag = 1;
        }
        short freeSlots = 0;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            if (GameData.MissionStates[i].IsActive == 0)
            {
                freeSlots = (short)(freeSlots + 1);
            }
        }
        if (freeSlots < 1)
        {
            // The "8" here is a hardcoded literal in the original, not freeSlots-derived —
            // keep it hardcoded even though it happens to match MissionStateTable.Count.
            AlertText.Message = "You’re already on 8 missions - you’ll have to abort or finish one before you can accept another.";
            DoSceneTransition.Run(0, 0);
            Graphics.RepaintGameWindow.Run();
            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
            return;
        }
        if (SpaceportGlobals.BbsLastSpob != GameData.Ships[0].NavTargetSpob ||
            SpaceportGlobals.BbsLastSpob < 0)
        {
            for (short i = 0; i < MissionAvailTable.Count; i = (short)(i + 1))
            {
                GameData.RandomOdds[i] = (short)(Misc.SeedEvoRng.Run(100) + 1);
            }
            Mission.RefreshMissionAvailabilityTables.Run();
        }
        SpaceportGlobals.BbsLastSpob = GameData.Ships[0].NavTargetSpob;
        GalaxyMapState.PreviewSystem = -1;
        GalaxyMapGlobals.MissionsDirty = 1;
        short available = 0;
        for (short i = 0; i < MissionAvailTable.Count; i = (short)(i + 1))
        {
            if (MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][i] != -1)
            {
                available = (short)(available + 1);
            }
        }
        if (available < 1)
        {
            AlertText.Message = "There are no missions available here.";
            DoSceneTransition.Run(0, 0);
            Graphics.RepaintGameWindow.Run();
            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
            return;
        }
        MissionBoardGlobals.Picts[0] = MacToolbox.GetPicture(0x1b8a);
        MissionBoardGlobals.Picts[1] = MacToolbox.GetPicture(0x1b8b);
        MissionBoardGlobals.Picts[2] = MacToolbox.GetPicture(0x1b60);
        MissionBoardGlobals.Picts[3] = MacToolbox.GetPicture(0x1b61);
        MissionBoardGlobals.DialogWindow = 0;
        MissionBoardGlobals.DialogWindow = MacToolbox.GetNewDialog(0x3ee, 0, -1);  // behind = -1 (frontmost)
        if (MissionBoardGlobals.DialogWindow != 0)
        {
            NewDialogHook.Run(MissionBoardGlobals.DialogWindow, 0);
            Graphics.RecenterWindowIntoPlayArea.Run(MissionBoardGlobals.DialogWindow);
            MacToolbox.ShowWindow(MissionBoardGlobals.DialogWindow);
            MacToolbox.SelectWindow(MissionBoardGlobals.DialogWindow);
            MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
            Mission.BuildMissionBbsList.Run();
            TextScratch.Text = "";
            if (SpaceportGlobals.BbsSelectedRow != -1)
            {
                RebuildSelectedMissionText();
            }
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref hitItem);
                if (hitItem == 1)
                {
                    // OGB-42 (ORIGINAL_GAME_BUGS.md): raw event-code ordinal used as mask — only
                    // ever flushes mouseDown/mouseUp, never keyDown/keyUp/autoKey (also below).
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                    MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                    if (SpaceportGlobals.BbsSelectedRow != -1)
                    {
                        done = (char)Mission.AcceptMission.Run(SelectedPers());
                    }
                    if (SpaceportGlobals.BbsSelectedRow != -1)
                    {
                        RebuildSelectedMissionText();
                    }
                    Mission.RedrawMissionBbsDialog.Run();
                }
                if (hitItem == 7)
                {
                    done = (char)1;
                }
                if (hitItem == 6)
                {
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                    MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                    GalaxyMapState.PreviewSystem = -1;
                    GalaxyMapGlobals.MissionsDirty = 1;
                    if (SpaceportGlobals.BbsSelectedRow != -1 &&
                        ((MisnFlags)GameData.MissionAvail[SelectedPers()].Flags & MisnFlags.ShowGreenArrowInBrief) != 0)
                    {
                        if (GameData.MissionDefs[SelectedPers()].TargetSpob == -1)
                        {
                            if (GameData.MissionDefs[SelectedPers()].ReturnSpob != -1)
                            {
                                GalaxyMapState.PreviewSystem =
                                    GameData.Spobs[GameData.MissionDefs[SelectedPers()].ReturnSpob].System;
                            }
                        }
                        else
                        {
                            GalaxyMapState.PreviewSystem =
                                GameData.Spobs[GameData.MissionDefs[SelectedPers()].TargetSpob].System;
                        }
                    }
                    short savedNavTarget = GameData.Ships[0].NavTargetSpob;
                    RunGalaxyMapDialog.Run();
                    GalaxyMapState.PreviewSystem = -1;
                    GameData.Ships[0].NavTargetSpob = savedNavTarget;
                    WorldState.SpawnPulseDirty = 1;
                    Graphics.TickHudRedrawScheduler.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                }
                if (hitItem == 10)
                {
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                    MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                    short count = 0;
                    for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
                    {
                        if (GameData.MissionStates[i].IsActive != 0)
                        {
                            count = (short)(count + 1);
                        }
                    }
                    if (0 < count)
                    {
                        RunMissionInfoDialog.Run();
                    }
                    if (SpaceportGlobals.BbsSelectedRow != -1)
                    {
                        RebuildSelectedMissionText();
                    }
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                }
                if (hitItem == 9)
                {
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                    MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                    MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                    RunPlayerInfoDialog.Run();
                    MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                    RedrawSpaceportDialog.Run();
                    MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                }
            } while (done == 0);
            for (short i = 0; i < MissionBoardGlobals.Picts.Length; i = (short)(i + 1))
            {
                if (MissionBoardGlobals.Picts[i] != 0)
                {
                    MacToolbox.HPurge(MissionBoardGlobals.Picts[i]);
                    MacToolbox.ReleaseResource(MissionBoardGlobals.Picts[i]);
                }
            }
            if (MissionBoardGlobals.BbsListHandle != 0)
            {
                MacToolbox.LDispose(MissionBoardGlobals.BbsListHandle);
            }
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(MissionBoardGlobals.DialogWindow);
            MissionBoardGlobals.DialogWindow = 0;
            Graphics.RepaintGameWindow.Run();
        }
    }
}
