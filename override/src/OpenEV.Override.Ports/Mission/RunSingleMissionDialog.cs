using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Mission;

// FUN_10050b7c (EV Override-11.c lines 33056-33291) — the single-mission
// OFFER dialog (DLOG 0x3f8 = 1016; shares the mission-board window + button
// PICT cells with the BBS). Layout follows the 'mïsn' flags bit 2: clear =
// accept(item 1)/refuse(item 2) row (PICTs 0x1bc4..0x1bc7), set = a single
// OK button on item 6 (PICTs 0x1b8e/0x1b8f). Filter items: 4 = show the
// destination on the galaxy map, 5 = player info, 7 = active-missions info.
// Accept runs AcceptMission; refuse re-loads the 'mïsn' resource and plays
// its res+0x58 refuse text/scene (res+0x5c = a ControlBits side effect).
public static class RunSingleMissionDialog
{
    // Bridge for the modal-filter UPP (cell 0x10081124 -> FUN_100513e4 =
    // ConfirmDialogFilter) — typed MacEvent shape.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = ConfirmDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    public static int Run(int missionIdx)
    {
        bool done = false;
        short hitItem = default;
        int acceptResult = 0;

        int filterUpp = MacToolbox.NewRoutineDescriptor(MissionBoardGlobals.OfferFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(filterUpp, FilterAdapter);
        // ORIGINAL quirk preserved (decompile uVar12): on the two early bail-outs
        // below, `result` still holds whatever it was last assigned — filterUpp
        // (mission index out of range) or the failed GetNewDialog handle (0) — a
        // leftover-register return, not a real value. Do not "fix" this to a
        // sentinel; it is bug-for-bug faithful to FUN_10050b7c.
        int result = filterUpp;
        short k = (short)missionIdx;
        if (-1 < k && k < 0x200)
        {
            if ((GameData.MissionAvail[k].Flags & 0x4) == 0)
            {
                MissionBoardGlobals.OfferAcceptRefuseLayout = 1;
                for (short i = 0; i < MissionBoardGlobals.Picts.Length; i = (short)(i + 1))
                {
                    result = MacToolbox.GetPicture(i + 0x1bc4);
                    MissionBoardGlobals.Picts[i] = result;
                }
            }
            else
            {
                MissionBoardGlobals.OfferAcceptRefuseLayout = 0;
                for (short i = 0; i < 2; i = (short)(i + 1))
                {
                    result = MacToolbox.GetPicture(i + 0x1b8e);
                    MissionBoardGlobals.Picts[i] = result;
                }
                for (short i = 2; i < MissionBoardGlobals.Picts.Length; i = (short)(i + 1))
                {
                    MissionBoardGlobals.Picts[i] = 0;
                }
            }
            MissionBoardGlobals.DialogWindow = 0;
            result = MacToolbox.GetNewDialog(0x3f8, 0, -1);
            MissionBoardGlobals.DialogWindow = result;
            if (MissionBoardGlobals.DialogWindow != 0)
            {
                NewDialogHook.Run(MissionBoardGlobals.DialogWindow, 0);                 // FUN_100583c4
                RecenterWindowIntoPlayArea.Run(MissionBoardGlobals.DialogWindow);       // FUN_100583c8
                MacToolbox.ShowWindow(MissionBoardGlobals.DialogWindow);
                MacToolbox.SelectWindow(MissionBoardGlobals.DialogWindow);
                MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                TextScratch.Text = "";
                TextScratch.Text = LoadDescriptionText.Load((short)(missionIdx + 4000));
                SubstituteMissionDescTags.Run(1, (short)missionIdx);
                do
                {
                    MacToolbox.ModalDialog(filterUpp, ref hitItem);
                    if (MissionBoardGlobals.OfferAcceptRefuseLayout == 0)
                    {
                        // Single-OK layout: both row buttons acknowledge the offer.
                        if (hitItem == 1 || hitItem == 2)
                        {
                            // BUG (OGB-42): raw event-code ordinal used as mask (ORIGINAL_GAME_BUGS.md) —
                            // only ever flushes mouseDown/mouseUp, never keyDown/keyUp/autoKey (also below).
                            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                            MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                            MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                            acceptResult = AcceptMission.Run(missionIdx);
                            TextScratch.Text = LoadDescriptionText.Load((short)(missionIdx + 4000));
                            SubstituteMissionDescTags.Run(1, (short)missionIdx);
                            RedrawSingleMissionDialog.Run();
                            done = true;
                        }
                    }
                    else
                    {
                        if (hitItem == 1)
                        {
                            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                            MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                            MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                            MacToolbox.HideWindow(MissionBoardGlobals.DialogWindow);
                            RepaintGameWindow.Run();   // FUN_1005ff4c
                            acceptResult = AcceptMission.Run(missionIdx);
                            RedrawSingleMissionDialog.Run();
                            TextScratch.Text = LoadDescriptionText.Load((short)(missionIdx + 4000));
                            SubstituteMissionDescTags.Run(1, (short)missionIdx);
                            // decompile 33157-33160: dead-store render-ctx capture (never read) — dropped.
                            RepaintGameWindow.Run();
                            MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                            done = true;
                        }
                        if (hitItem == 2)
                        {
                            int resHandle = MacToolbox.GetResource(MacResType.Mission, missionIdx + 128);   // 'mïsn'
                            MissionDefTable.ResourceHandle = resHandle;   // publish the live handle
                            if (resHandle != 0)
                            {
                                MacToolbox.HLock(resHandle);
                                // res+0x58 = refuse text/scene id; res+0x5c (only when the resource is
                                // >= 0x70 bytes) = ControlBits side-effect id.
                                short refuseTextId = MacToolbox.ReadResourceShort(resHandle, 0x58);
                                short refuseControlBit;
                                if ((uint)MacToolbox.GetHandleSize(resHandle) < 112)
                                {
                                    refuseControlBit = -1;
                                }
                                else
                                {
                                    refuseControlBit = MacToolbox.ReadResourceShort(resHandle, 0x5c);
                                }
                                MacToolbox.HUnlock(resHandle);
                                MacToolbox.HPurge(resHandle);
                                MacToolbox.ReleaseResource(resHandle);
                                if (refuseTextId != -1)
                                {
                                    MacToolbox.HideWindow(MissionBoardGlobals.DialogWindow);
                                    RepaintGameWindow.Run();
                                    if (PlayMovieById.Run(refuseTextId, 1) != 0)
                                    {   // FUN_100602d8
                                        TextScratch.Text = "";
                                        TextScratch.Text = LoadDescriptionText.Load(refuseTextId);
                                        SubstituteMissionDescTags.Run(0, (short)missionIdx);
                                        AlertText.Message = TextScratch.Trunc(TextScratch.Text, 0x3ff);
                                        DoSceneTransition.Run(0, 0);   // FUN_1003e49c
                                    }
                                    RepaintGameWindow.Run();
                                    PlayMovieById.Run(refuseTextId, 0);
                                    if (refuseControlBit < 0 || 0x1ff < refuseControlBit)
                                    {
                                        if (999 < refuseControlBit && refuseControlBit < 1512)
                                        {
                                            ControlBits.Set(refuseControlBit - 1000, 0);
                                        }
                                    }
                                    else
                                    {
                                        ControlBits.Set(refuseControlBit, 1);
                                    }
                                }
                            }
                            done = true;
                        }
                    }
                    if (hitItem == 4)
                    {
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                        MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                        GalaxyMapState.PreviewSystem = -1;
                        GalaxyMapGlobals.MissionsDirty = 1;
                        if ((GameData.MissionAvail[k].Flags & 0x100) != 0)
                        {
                            if (GameData.MissionDefs[k].TargetSpob == -1)
                            {
                                if (GameData.MissionDefs[k].ReturnSpob != -1)
                                {
                                    GalaxyMapState.PreviewSystem = GameData.Spobs[GameData.MissionDefs[k].ReturnSpob].System;
                                }
                            }
                            else
                            {
                                GalaxyMapState.PreviewSystem = GameData.Spobs[GameData.MissionDefs[k].TargetSpob].System;
                            }
                        }
                        short savedNavTarget = GameData.Ships[0].NavTargetSpob;
                        RunGalaxyMapDialog.Run();   // FUN_10030014 — the galaxy-map screen
                        GameData.Ships[0].NavTargetSpob = savedNavTarget;
                        GalaxyMapState.PreviewSystem = -1;
                        if (RenderGlobals.DrawGateFlag == 0)
                        {
                            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                            RedrawSpaceportDialog.Run();   // FUN_10037bb4
                        }
                        MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                    }
                    if (hitItem == 5)
                    {
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                        MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                        RunPlayerInfoDialog.Run();   // FUN_1003eda8
                        if (RenderGlobals.DrawGateFlag == 0)
                        {
                            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                            RedrawSpaceportDialog.Run();
                        }
                        MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                    }
                    if (hitItem == 7)
                    {
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
                        MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
                        MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
                        RunMissionInfoDialog.Run();   // FUN_1004fa88
                        TextScratch.Text = LoadDescriptionText.Load((short)(missionIdx + 4000));
                        SubstituteMissionDescTags.Run(1, (short)missionIdx);
                        if (RenderGlobals.DrawGateFlag == 0)
                        {
                            MacToolbox.SetPort(SpaceportGlobals.DialogWindow);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(SpaceportGlobals.DialogWindow));
                            RedrawSpaceportDialog.Run();
                        }
                        MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(MissionBoardGlobals.DialogWindow));
                    }
                } while (!done);
                for (short i = 0; i < MissionBoardGlobals.Picts.Length; i = (short)(i + 1))
                {
                    if (MissionBoardGlobals.Picts[i] != 0)
                    {
                        MacToolbox.HPurge(MissionBoardGlobals.Picts[i]);
                        MacToolbox.ReleaseResource(MissionBoardGlobals.Picts[i]);
                    }
                }
                MacToolbox.DisposeRoutineDescriptor(filterUpp);
                MacToolbox.DisposeDialog(MissionBoardGlobals.DialogWindow);
                MissionBoardGlobals.DialogWindow = 0;
                RepaintGameWindow.Run();
                result = acceptResult;
            }
        }
        return result;
    }
}
