using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_10030014 (EV Override-11.c lines 19809-20287)
// The galaxy-map dialog (DLOG 2000, opened by the Map key). Builds the mission route
// list, loads the zoom-detail PICTs, then runs the ModalDialog loop: items 4/5 = zoom
// in/out, 8 = clear route, 3 = map click (system select / route-plot / drag-pan),
// 1 = done. On exit repaints the game window and re-engages autopilot.
public static class RunGalaxyMapDialog
{
    private const int NebulaGridCols = 4;
    private const int NebulaGridRows = 3;
    private const int ZoomButtonCount = 2;
    private const int ActionButtonLimit = 6;
    private const int ReleasedButtonCount = 6;
    private const int FirstZoomInvalidatedItem = 2;
    private const int LastZoomInvalidatedItem = 6;

    // Bridge the map ModalFilter ProcPtr to its port (GalaxyMapModalFilter = FUN_10034420).
    // Run reports its chosen item through the ref param; hand it back via evt.ItemHit so the
    // ModalDialog loop sees it (the same handback every sibling filter adapter does).
    private static int MapModalFilter(int dlg, MacEvent evt)
    {
        short itemHit = 0;
        int result = GalaxyMapModalFilter.Run(dlg, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return result;
    }

    public static void Run()
    {
        int modalFilter = MacToolbox.NewRoutineDescriptor(GalaxyMapGlobals.MapModalFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(modalFilter, MapModalFilter);
        // Host: a prior open that threw before its cleanup could have left the game-scene flush
        // paused; clear it so a re-open always starts from a known state.
        MacToolbox.SuspendGameSceneFlush = false;
        GalaxyMapState.MapDialog = 0;
        GalaxyMapState.MapDialog = MacToolbox.GetNewDialog(2000, 0, -1);
        if (GalaxyMapState.MapDialog == 0)
            return;

        // Faithful offscreen handling: the map draws every system into the offscreen game GWorld and
        // CopyBits only the map sub-rect. On Mac the blocked game loop's per-frame offscreen->screen
        // copy (RepaintGameWindow) doesn't run, so off-rect systems never reach screen; the port's host
        // copies every frame, so pause that flush for the map's lifetime (cleared on the exit repaint).
        MacToolbox.SuspendGameSceneFlush = true;
        var player = GameData.Player;
        short savedNavTargetSpob = player.NavTargetSpob;
        // Entry zoom at the reset threshold (0.0) -> reset the live zoom to the default (1.0).
        if (GalaxyMapGlobals.ZoomResetThreshold == GalaxyMapState.Zoom)
            GalaxyMapState.Zoom = GalaxyMapGlobals.ZoomDetailNearThreshold;
        CompactNavHistoryHead.Run();
        GalaxyMapState.RouteActive = 0;
        for (short i = 1; i < GalaxyMapGlobals.NavHistory.Length; i++)
        {
            if (GalaxyMapGlobals.NavHistory[i] != -1)
                GalaxyMapState.RouteActive = 1;
        }
        for (short col = 0; col < NebulaGridCols; col++)
        {
            for (short row = 0; row < NebulaGridRows; row++)
                GalaxyMapState.NebulaPicts[col * NebulaGridRows + row] = MacToolbox.GetPicture(col * NebulaGridRows + row + 9500);
        }
        for (short i = 0; i < ZoomButtonCount; i++)
            GalaxyMapState.ButtonPics[i] = MacToolbox.GetPicture(i + 7012);
        for (short i = ZoomButtonCount; i < ActionButtonLimit; i++)
            GalaxyMapState.ButtonPics[i] = MacToolbox.GetPicture(i + 7022);
        GalaxyMapState.ButtonPics[6] = MacToolbox.GetPicture(7118);
        GalaxyMapState.ButtonPics[7] = MacToolbox.GetPicture(7119);
        if (GalaxyMapState.HandCursor == 0)
            GalaxyMapState.HandCursor = MacToolbox.GetCursor(128);
        GalaxyMapState.VestigialFlag76a4 = 0;
        GalaxyMapState.VestigialRgn76a8 = 0;
        GalaxyMapState.ScrollInProgress = 0;
        GalaxyMapState.UpdateRgn = MacToolbox.NewRgn();
        for (short i = 0; i < GalaxyMapState.RouteList.Length; i++)
            GalaxyMapState.RouteList[i] = -1;

        // Build the route list from the 8 active mission records.
        short routeCount = 0;
        for (short mi = 0; mi < GameData.Missions.Length; mi++)
        {
            var missionState = GameData.MissionStates[mi];
            if (missionState.IsActive == 0)
                continue;
            var mission = GameData.Missions[mi];

            short routeSystem = -1;
            if (mission.TargetSpob > -1 && GameData.Spobs[mission.TargetSpob].Visible != 0)
                routeSystem = GameData.Spobs[mission.TargetSpob].System;
            if (mission.ReturnSpob > -1 && mission.TargetSpob != mission.ReturnSpob &&
                missionState.ArrivedAtTarget != 0 && GameData.Spobs[mission.ReturnSpob].Visible != 0)
                routeSystem = GameData.Spobs[mission.ReturnSpob].System;
            if (routeSystem != -1 && (mission.Flags & MisnFlags.HideRedMapArrows) == 0)
            {
                GalaxyMapState.RouteList[routeCount] = routeSystem;
                routeCount++;
            }
            if ((mission.Flags & MisnFlags.ShowDestSystemOnMap) != 0 &&
                (mission.Flags & MisnFlags.HideRedMapArrows) == 0 &&
                mission.SpawnCount > 0 && mission.DestSystem > -1 &&
                GameData.Spobs[mission.DestSystem].Visible != 0)
            {
                GalaxyMapState.RouteList[routeCount] = mission.DestSystem;
                routeCount++;
            }
        }
        if (GalaxyMapState.MissionDestinationIcon == 0)
            GalaxyMapState.MissionDestinationIcon = MacToolbox.GetCIcon(15000);
        if (GalaxyMapState.PreviewTargetIcon == 0)
            GalaxyMapState.PreviewTargetIcon = MacToolbox.GetCIcon(15001);
        if (player.NavMode == 3 && player.NavTargetSpob != -1 &&
            GalaxyMapState.TradeKeyLock == 0)
        {
            GalaxyMapState.CentredSystem =
                GameData.Systems[player.CurrentSystem].HyperLink[player.NavTargetSpob];
        }
        else
        {
            GalaxyMapState.CentredSystem = player.CurrentSystem;
        }
        CacheMapNebulaBackgrounds.Run();
        NewDialogHook.Run(GalaxyMapState.MapDialog, 0);
        RecenterWindowIntoPlayArea.Run(GalaxyMapState.MapDialog);
        MacToolbox.ShowWindow(GalaxyMapState.MapDialog);
        MacToolbox.SelectWindow(GalaxyMapState.MapDialog);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(GalaxyMapState.MapDialog);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
        MacToolbox.InvalRect(MacToolbox.GetPortRectShorts(GalaxyMapState.MapDialog));
        DrawGalaxyMap.Run();
        junkcode.FUN_100314cc();   // original empty stub

        short[] hitRect = new short[4];
        short[] itemRect = new short[4];
        short itemHit = 0;
        bool dialogDone = false;
        do
        {
            MacToolbox.ModalDialog(modalFilter, ref itemHit);
            if (itemHit == 1)
                dialogDone = true;
            if (itemHit == 7)
                MacToolbox.SetCursor(0);
            if (itemHit == 4 && GalaxyMapState.PlusEnabled != 0)
            {
                GalaxyMapState.Zoom *= GalaxyMapGlobals.ZoomInFactor;
                for (short item = FirstZoomInvalidatedItem; item <= LastZoomInvalidatedItem; item++)
                {
                    MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, item, null, null, itemRect);
                    MacToolbox.InvalRect(itemRect);
                }
                CacheMapNebulaBackgrounds.Run();
            }
            if (itemHit == 5 && GalaxyMapState.MinusEnabled != 0)
            {
                GalaxyMapState.Zoom *= GalaxyMapGlobals.ZoomOutFactor;
                for (short item = FirstZoomInvalidatedItem; item <= LastZoomInvalidatedItem; item++)
                {
                    MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, item, null, null, itemRect);
                    MacToolbox.InvalRect(itemRect);
                }
                CacheMapNebulaBackgrounds.Run();
            }
            if (itemHit == 8)
            {
                for (short i = 0; i < GalaxyMapGlobals.NavHistory.Length; i++)
                    GalaxyMapGlobals.NavHistory[i] = -1;
                GalaxyMapState.RouteActive = 0;
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, null, null, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 8, null, null, itemRect);
                MacToolbox.InvalRect(itemRect);
                DrawGalaxyMap.Run();
            }
            if (itemHit == 3)
            {
                int mousePt = MacToolbox.GetMouse();
                int prevPt = mousePt;
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, null, null, itemRect);
                short mapW = (short)(itemRect[3] - itemRect[1]);
                short mapH = (short)(itemRect[2] - itemRect[0]);
                short candidate = -1;
                short selectedSystem = -1;
                for (short sys = 0; sys < GameData.Systems.Length; sys++)
                {
                    selectedSystem = candidate;
                    short systX = GameData.Systems[sys].XPos;
                    short systY = GameData.Systems[sys].YPos;
                    double zoom = GalaxyMapState.Zoom;
                    int coord = (int)((itemRect[1] + mapW / 2)
                                      + systX / zoom
                                      - WorldState.MapViewCentreX / zoom);
                    hitRect[1] = (short)coord;
                    coord = (int)((itemRect[0] + mapH / 2)
                                  + systY / zoom
                                  - WorldState.MapViewCentreY / zoom);
                    hitRect[0] = (short)coord;
                    hitRect[2] = hitRect[0];
                    hitRect[3] = hitRect[1];
                    MacToolbox.InsetRect(hitRect, -10, -10);
                    if (GameData.Systems[sys].ShownFlag != 0 && MacToolbox.PtInRect(mousePt, hitRect))
                    {
                        bool inRouteList = false;
                        for (short r = 0; r < GalaxyMapState.RouteList.Length; r++)
                        {
                            if (sys == GalaxyMapState.RouteList[r] && GalaxyMapState.RouteList[r] != -1)
                            {
                                inRouteList = true;
                                break;
                            }
                        }
                        selectedSystem = sys;
                        if (GameData.Systems[sys].Visited > 0 || inRouteList ||
                            GalaxyMapState.PreviewSystem == sys)
                            break;
                        // Scan the clicked system's hyperlinks for one that is charted (Visited >= 1)
                        // and shown: found -> select the clicked system; none -> revert to the previous
                        // candidate (the ASM leaves r26 unchanged when no valid link is found).
                        selectedSystem = candidate;
                        for (short hl = 0; hl < SystRecord.HyperLinkCount; hl++)
                        {
                            short linkSys = GameData.Systems[sys].HyperLink[hl];
                            if (linkSys != -1 && GameData.Systems[linkSys].Visited >= 1 &&
                                GameData.Systems[linkSys].ShownFlag != 0)
                            {
                                selectedSystem = sys;
                                break;
                            }
                        }
                    }
                    candidate = selectedSystem;
                }
                if (selectedSystem == -1)
                {
                    if (MacToolbox.StillDown())
                    {
                        int dragOriginPt = MacToolbox.GetMouse();
                        short dragOriginX = (short)dragOriginPt;
                        short dragOriginY = (short)(dragOriginPt >> 16);
                        prevPt = dragOriginPt;
                        short savedViewCentreX = WorldState.MapViewCentreX;
                        short savedViewCentreY = WorldState.MapViewCentreY;
                        while (MacToolbox.StillDown())
                        {
                            prevPt = mousePt;
                            mousePt = MacToolbox.GetMouse();
                            short mouseX = (short)mousePt;
                            short mouseY = (short)(mousePt >> 16);
                            double zoom = GalaxyMapState.Zoom;
                            int newCentreX = (int)-(zoom * (double)(mouseX - dragOriginX)
                                               - (double)savedViewCentreX);
                            int newCentreY = (int)-(zoom * (double)(mouseY - dragOriginY)
                                               - (double)savedViewCentreY);
                            WorldState.MapViewCentreX = (short)newCentreX;
                            WorldState.MapViewCentreY = (short)newCentreY;
                            ScrollGalaxyMapArea.Run((short)(mouseX - (short)prevPt),
                                       (short)(mouseY - (short)(prevPt >> 16)));
                        }
                    }
                }
                else
                {
                    MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 2, null, null, hitRect);
                    MacToolbox.InvalRect(hitRect);
                    MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, null, null, hitRect);
                    MacToolbox.InvalRect(hitRect);
                    MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 6, null, null, hitRect);
                    MacToolbox.InvalRect(hitRect);
                    // Shift-click chains the jump route.
                    short shiftDown = (short)Keymap.TestLiveKeymapBit(MacKeycode.Shift);
                    if (shiftDown == 0)
                    {
                        GalaxyMapState.CentredSystem = selectedSystem;
                        short navLinkIndex = -1;
                        var playerSyst = GameData.Systems[player.CurrentSystem];
                        for (short i = 0; i < SystRecord.HyperLinkCount; i++)
                        {
                            if (selectedSystem == playerSyst.HyperLink[i])
                            {
                                navLinkIndex = i;
                                break;
                            }
                        }
                        if (navLinkIndex != -1 && GalaxyMapState.TradeKeyLock == 0)
                        {
                            player.NavMode = 3;
                            player.NavTargetSpob = navLinkIndex;
                            WorldState.SpawnPulseDirty = 1;
                            TickHudRedrawScheduler.Run();
                            MacToolbox.SetPort(GalaxyMapState.MapDialog);
                        }
                    }
                    else
                    {
                        CompactNavHistoryHead.Run();
                        // First free (-1) slot in the nav-history route, scanning from 1 (slot 0
                        // holds the route's origin system); -1 if the route is full.
                        short freeSlot = -1;
                        for (short i = 1; i < GalaxyMapGlobals.NavHistory.Length; i++)
                        {
                            if (GalaxyMapGlobals.NavHistory[i] == -1)
                            {
                                freeSlot = i;
                                break;
                            }
                        }
                        bool accepted = false;
                        if (freeSlot != -1)
                        {
                            if (freeSlot < 2)
                            {
                                var playerSyst = GameData.Systems[player.CurrentSystem];
                                for (short i = 0; i < SystRecord.HyperLinkCount; i++)
                                {
                                     if (selectedSystem == playerSyst.HyperLink[i])
                                         accepted = true;
                                }
                            }
                            else
                            {
                                // Selected must link off the route's current tail; accepted unless both
                                // tail and link are uncharted.
                                for (short i = 0; i < SystRecord.HyperLinkCount; i++)
                                {
                                    short tailSys = GalaxyMapGlobals.NavHistory[freeSlot - 1];
                                    short tailLink = GameData.Systems[tailSys].HyperLink[i];
                                    if (selectedSystem == tailLink)
                                    {
                                        accepted = true;
                                        if (GameData.Systems[tailSys].Visited < 1 &&
                                            GameData.Systems[tailLink].Visited < 1)
                                            accepted = false;
                                    }
                                }
                            }
                        }
                        if (freeSlot > 0 && selectedSystem == GalaxyMapGlobals.NavHistory[freeSlot - 1])
                        {
                            GalaxyMapGlobals.NavHistory[freeSlot - 1] = -1;
                            freeSlot = -1;
                        }
                        if (accepted)
                        {
                            if (freeSlot != -1)
                            {
                                if (GalaxyMapGlobals.NavHistory[0] == -1)
                                    GalaxyMapGlobals.NavHistory[0] = player.CurrentSystem;
                                GalaxyMapGlobals.NavHistory[freeSlot] = selectedSystem;
                                GalaxyMapState.RouteActive = 1;
                            }
                            MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, null, null, hitRect);
                            MacToolbox.InvalRect(hitRect);
                            MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 8, null, null, hitRect);
                            MacToolbox.InvalRect(hitRect);
                            Render4ButtonRow.Run(-1);
                            if (SpaceportGlobals.DialogWindow == 0)
                                EngageAutopilotToHistoryTarget.Run();
                        }
                    }
                }
            }
        } while (!dialogDone);

        // The original releases only picts 0..5; ButtonPics[6]/[7] (PICTs 7118/7119, loaded
        // unconditionally at entry) are faithfully never released — do not hoist the 6 to
        // ButtonPics.Length (8), that would "fix" the original's leak.
        for (short i = 0; i < ReleasedButtonCount; i++)
        {
            if (GalaxyMapState.ButtonPics[i] != 0)
            {
                MacToolbox.HPurge(GalaxyMapState.ButtonPics[i]);
                MacToolbox.ReleaseResource(GalaxyMapState.ButtonPics[i]);
            }
        }
        for (short i = 0; i < NebulaGridCols; i++)
        {
            for (short j = 0; j < NebulaGridRows; j++)
            {
                if (GalaxyMapState.NebulaPicts[i * NebulaGridRows + j] != 0)
                {
                    MacToolbox.HPurge(GalaxyMapState.NebulaPicts[i * NebulaGridRows + j]);
                    MacToolbox.ReleaseResource(GalaxyMapState.NebulaPicts[i * NebulaGridRows + j]);
                }
            }
        }
        // Dialog window portRect: half-width / half-height (truncating /2).
        short[] dlgRect = MacToolbox.GetPortRectShorts(GalaxyMapState.MapDialog);
        int dlgSpanW = dlgRect[3] - dlgRect[1];
        short halfDlgW = (short)(dlgSpanW / 2);
        int dlgSpanH = dlgRect[2] - dlgRect[0];
        short halfDlgH = (short)(dlgSpanH / 2);
        if (GalaxyMapState.VestigialRgn76a8 != 0)
            MacToolbox.DisposeRgn(GalaxyMapState.VestigialRgn76a8);
        if (GalaxyMapState.UpdateRgn != 0)
            MacToolbox.DisposeRgn(GalaxyMapState.UpdateRgn);
        MacToolbox.DisposeRoutineDescriptor(modalFilter);
        MacToolbox.DisposeDialog(GalaxyMapState.MapDialog);
        GWorldPort.SetActivePortSecondaryGame();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PrimaryStageRect);
        GWorldPort.SetActivePortScratch();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.ScratchStageRect);
        SetGamePortAndDevice.Run();
        int portSpanW = GlobalState.PortRight - GlobalState.PortLeft;
        int portHalfW = portSpanW / 2;
        int portSpanH = GlobalState.PortBottom - GlobalState.PortTop;
        short portHalfH = (short)(portSpanH / 2);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.SetRect(hitRect, (short)(portHalfW - halfDlgW), (short)(portHalfH - halfDlgH),
                           (short)(portHalfW + halfDlgW), (short)(portHalfH + halfDlgH));
        int portBotRightPacked = GlobalState.PortBotRightPacked;
        itemRect[2] = (short)(portBotRightPacked >> 16);
        itemRect[3] = (short)portBotRightPacked;
        itemRect[0] = (short)((uint)GlobalState.PortTopLeftPacked >> 16);
        itemRect[1] = (short)((short)portBotRightPacked - 144);
        if (MacToolbox.SectRect(hitRect, itemRect, itemRect))
            RefreshStatusPanel.Run();
        RepaintGameWindow.Run();
        // The game window is repainted from the offscreen; resume the per-frame flush.
        MacToolbox.SuspendGameSceneFlush = false;
        MacToolbox.SetCursor(0);
        if (SpaceportGlobals.DialogWindow == 0)
            EngageAutopilotToHistoryTarget.Run();
        else
            player.NavTargetSpob = savedNavTargetSpob;
    }
}
