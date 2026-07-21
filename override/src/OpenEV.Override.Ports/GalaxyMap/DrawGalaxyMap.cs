using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.GalaxyMap;

// Port of FUN_100314d0 (EV Override-11.c 20298-21264): draw the galaxy-map dialog
public static class DrawGalaxyMap
{
    private const int NebulaCount = 4;
    private const int HyperlinkCount = 16;
    private const int StellarLinkCount = 4;
    private const int CommodityCount = 6;
    private const int ShopTypeCount = 3;

    public static void Run()
    {
        byte[] visited = new byte[GameData.Systems.Length];
        for (short systIdx = 0; systIdx < GameData.Systems.Length; systIdx++)
        {
            visited[systIdx] = 0;
        }

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.PenSize(1, 1);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(GalaxyMapState.MapDialog));

        short[] itemRect = new short[4];

        if (GalaxyMapState.ScrollInProgress == 0)
        {
            DrawTopButtons(itemRect);
        }

        bool drawMap = false;
        if (GalaxyMapState.ScrollInProgress == 0)
        {
            MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, 0, 0, itemRect);
            if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
            {
                drawMap = true;
            }
        }
        else
        {
            drawMap = true;
        }

        if (drawMap)
        {
            if (GalaxyMapState.ScrollInProgress == 0)
            {
                GWorldPort.SetActivePortSecondaryGame();
                MacToolbox.PenSize(1, 1);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, 0, 0, itemRect);
            }
            else
            {
                SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
                MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, 0, 0, itemRect);
            }

            short mapTop = itemRect[0], mapLeft = itemRect[1];
            short mapBottom = itemRect[2], mapRight = itemRect[3];
            int mapW = mapRight - mapLeft;
            int mapH = mapBottom - mapTop;
            int camX = WorldState.MapViewCentreX;
            short camY = WorldState.MapViewCentreY;

            // Map centre in screen coords (truncating division / 2, matches ASM srawi 1/addze)
            int mapCx = mapLeft + mapW / 2;
            int mapCy = mapTop + mapH / 2;
            double zoom = GalaxyMapState.Zoom;

            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(7);

            if (GalaxyMapGlobals.ZoomDetailNearThreshold < zoom) MacToolbox.TextSize(7);
            else if (GalaxyMapGlobals.ZoomDetailNearThreshold == zoom) MacToolbox.TextSize(9);
            else MacToolbox.TextSize(9);

            short[] selRect = new short[4];
            DrawNebulas(mapCx, mapCy, camX, camY, zoom, itemRect, selRect);
            DrawHyperlinksOut(mapCx, mapCy, camX, camY, zoom);
            DrawNavHistoryRoute(mapCx, mapCy, camX, camY, zoom);
            DrawChartedSystemsWeb(mapCx, mapCy, camX, camY, zoom, visited);
            DrawSystemsAndIcons(mapCx, mapCy, camX, camY, zoom, selRect);
            DrawCrosshair(mapCx, mapCy, camX, camY, zoom);
            DrawSystemLabels(mapCx, mapCy, camX, camY, zoom);

            MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            GalaxyMapState.ResetFlag = 1;

            if (GalaxyMapState.ScrollInProgress == 0)
            {
                SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.CopyBits(GlobalState.OffscreenGameGWorld + 2,
                                    RenderGlobals.BackdropGWorld + 2, itemRect, itemRect, 0, 0);
            }
            else
            {
                SetGamePortAndDevice.Run();
                MacToolbox.SetPort(GalaxyMapState.MapDialog);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.InsetRect(itemRect, 1, 1);
                MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GalaxyMapState.MapDialog + 2, itemRect, itemRect, 0, 0);
            }
        }

        if (GalaxyMapState.ScrollInProgress == 0)
        {
            DrawItem2InfoBox(itemRect);
            DrawItem6Panel();

            Render4ButtonRow.Run(-1);
            MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
            MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(GalaxyMapState.MapDialog));
            MacToolbox.ForeColor(QuickDrawColor.Black);
            SetGamePortAndDevice.Run();
            MacToolbox.SetPort(GalaxyMapState.MapDialog);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GalaxyMapState.MapDialog + 2,
                                MacToolbox.GetDialogPortRect(GalaxyMapState.MapDialog), MacToolbox.GetDialogPortRect(GalaxyMapState.MapDialog), 0, 0);
        }
    }

    private static void DrawTopButtons(short[] buttonRect)
    {
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 1, 0, 0, buttonRect);
        if (MacToolbox.RectInRgn(buttonRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[0], buttonRect);
            GalaxyMapState.ResetFlag = 1;
        }
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 4, 0, 0, buttonRect);
        if (MacToolbox.RectInRgn(buttonRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
        {
            if (GalaxyMapState.PlusEnabled == 0) MacToolbox.PaintRect(buttonRect);
            else MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[2], buttonRect);
            GalaxyMapState.ResetFlag = 1;
        }
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 5, 0, 0, buttonRect);
        if (MacToolbox.RectInRgn(buttonRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
        {
            if (GalaxyMapState.MinusEnabled == 0) MacToolbox.PaintRect(buttonRect);
            else MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[4], buttonRect);
            GalaxyMapState.ResetFlag = 1;
        }
    }

    private static void DrawNebulas(int mapCx, int mapCy, int camX, short camY, double zoom, short[] mapRect, short[] selRect)
    {
        short[] pictRect = new short[4];
        for (int idx = 0; idx < NebulaCount; idx++)
        {
            // Faithful redundant tier calc: the original recomputes this after the SectRect
            // test below, so this first result is always overwritten before it is read.
            short scaleTier;
            if (GalaxyMapGlobals.ZoomDetailNearThreshold < zoom) scaleTier = 0;
            else if (GalaxyMapGlobals.ZoomDetailNearThreshold == zoom) scaleTier = 1;
            else scaleTier = 2;

            if (GameData.MapNebulas[idx].Charted != 0)
            {
                short objLeft = (short)(mapCx + GameData.MapNebulas[idx].X / zoom - camX / zoom);
                short objTop = (short)(mapCy + GameData.MapNebulas[idx].Y / zoom - camY / zoom);
                short objRight = (short)(objLeft + GameData.MapNebulas[idx].Width / zoom);
                short objBottom = (short)(objTop + GameData.MapNebulas[idx].Height / zoom);

                pictRect[0] = objTop; pictRect[1] = objLeft;
                pictRect[2] = objBottom; pictRect[3] = objRight;

                if (MacToolbox.SectRect(pictRect, mapRect, selRect)) // selRect result never read
                {
                    if (GalaxyMapGlobals.ZoomDetailNearThreshold < zoom) scaleTier = 0;
                    else if (GalaxyMapGlobals.ZoomDetailNearThreshold == zoom) scaleTier = 1;
                    else scaleTier = 2;

                    if (GalaxyMapState.ScrollInProgress == 0)
                    {
                        SetPortAndDevice.Run(GlobalState.OffscreenGameGWorld, GlobalState.OffscreenGameGDevice);
                    }
                    else
                    {
                        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
                    }
                    MacToolbox.DrawPicture(GalaxyMapState.NebulaPicts[idx * 3 + scaleTier], pictRect);
                }
            }
        }
    }

    private static void DrawHyperlinksOut(int mapCx, int mapCy, int camX, short camY, double zoom)
    {
        int curSys = GameData.Player.CurrentSystem;
        for (int linkIdx = 0; linkIdx < HyperlinkCount; linkIdx++)
        {
            short linkSys = GameData.Systems[curSys].HyperLink[linkIdx];
            if (linkSys != -1 && GameData.Systems[linkSys].ShownFlag != 0)
            {
                if (GameData.Player.NavMode == 3 && linkIdx == GameData.Player.NavTargetSpob)
                {
                    MacToolbox.RGBForeColor((uint)UiColors.Friendly);
                }
                else
                {
                    MacToolbox.RGBForeColor((uint)UiColors.Frame);
                }
                int screenX1 = (int)(mapCx + GameData.Systems[curSys].XPos / zoom - camX / zoom);
                int screenY1 = (int)(mapCy + GameData.Systems[curSys].YPos / zoom - camY / zoom);
                MacToolbox.MoveTo(screenX1, screenY1);
                int screenX2 = (int)(mapCx + GameData.Systems[linkSys].XPos / zoom - camX / zoom);
                int screenY2 = (int)(mapCy + GameData.Systems[linkSys].YPos / zoom - camY / zoom);
                MacToolbox.LineTo(screenX2, screenY2);
            }
        }
    }

    private static void DrawNavHistoryRoute(int mapCx, int mapCy, int camX, short camY, double zoom)
    {
        for (int idx = 0; idx < GalaxyMapGlobals.NavHistoryLength - 1; idx++)
        {
            short routeA = GalaxyMapGlobals.NavHistory[idx];
            short routeB = GalaxyMapGlobals.NavHistory[idx + 1];
            if (routeA != -1 && routeB != -1)
            {
                MacToolbox.PenSize(2, 2);
                MacToolbox.RGBForeColor((uint)UiColors.Neutral);
                int screenX1 = (int)(mapCx + GameData.Systems[routeA].XPos / zoom - camX / zoom);
                int screenY1 = (int)(mapCy + GameData.Systems[routeA].YPos / zoom - camY / zoom);
                MacToolbox.MoveTo(screenX1, screenY1);
                int screenX2 = (int)(mapCx + GameData.Systems[routeB].XPos / zoom - camX / zoom);
                int screenY2 = (int)(mapCy + GameData.Systems[routeB].YPos / zoom - camY / zoom);
                MacToolbox.LineTo(screenX2, screenY2);
                MacToolbox.PenSize(1, 1);
            }
        }
    }

    private static void DrawChartedSystemsWeb(int mapCx, int mapCy, int camX, short camY, double zoom, byte[] visited)
    {
        visited[GameData.Player.CurrentSystem] = 1;
        for (short systIdx = 0; systIdx < GameData.Systems.Length; systIdx++)
        {
            var syst = GameData.Systems[systIdx];
            if (syst.ShownFlag == 0) continue;
            if (syst.Visited > 0)
            {
                for (int linkIdx = 0; linkIdx < HyperlinkCount; linkIdx++)
                {
                    short linkSys = syst.HyperLink[linkIdx];
                    if (linkSys != -1 && GameData.Systems[linkSys].ShownFlag != 0 && visited[linkSys] == 0)
                    {
                        if (systIdx == GameData.Player.CurrentSystem && GameData.Player.NavMode == 3 && linkIdx == GameData.Player.NavTargetSpob)
                        {
                            MacToolbox.RGBForeColor((uint)UiColors.Friendly);
                        }
                        else
                        {
                            MacToolbox.RGBForeColor((uint)UiColors.Frame);
                        }
                        int screenX1 = (int)(mapCx + syst.XPos / zoom - camX / zoom);
                        int screenY1 = (int)(mapCy + syst.YPos / zoom - camY / zoom);
                        MacToolbox.MoveTo(screenX1, screenY1);
                        int screenX2 = (int)(mapCx + GameData.Systems[linkSys].XPos / zoom - camX / zoom);
                        int screenY2 = (int)(mapCy + GameData.Systems[linkSys].YPos / zoom - camY / zoom);
                        MacToolbox.LineTo(screenX2, screenY2);
                    }
                }
                visited[systIdx] = 1;
            }
        }
    }

    private static void DrawSystemsAndIcons(int mapCx, int mapCy, int camX, short camY, double zoom, short[] selRect)
    {
        short[] dotRect = new short[4];
        short[] iconRect = new short[4];
        for (short systIdx = 0; systIdx < GameData.Systems.Length; systIdx++)
        {
            var syst = GameData.Systems[systIdx];
            if (syst.ShownFlag == 0) continue;
            bool hasMission = false;
            byte drawPreviewIcon = 0;

            bool isCurrentOrLinkedSystem;
            if (GameData.Player.CurrentSystem == systIdx)
            {
                isCurrentOrLinkedSystem = true;
            }
            else
            {
                isCurrentOrLinkedSystem = false;
                for (int linkIdx = 0; linkIdx < HyperlinkCount; linkIdx++)
                {
                    short linkSys = syst.HyperLink[linkIdx];
                    if (linkSys != -1 && GameData.Systems[linkSys].ShownFlag != 0 && GameData.Systems[linkSys].Visited > 0)
                    {
                        isCurrentOrLinkedSystem = true;
                        break;
                    }
                }
            }
            for (int linkIdx = 0; linkIdx < HyperlinkCount; linkIdx++)
            {
                if (systIdx == GalaxyMapState.RouteList[linkIdx] && GalaxyMapState.RouteList[linkIdx] != -1)
                {
                    hasMission = true;
                    break;
                }
            }
            if (GalaxyMapState.PreviewSystem == systIdx)
            {
                drawPreviewIcon = 1;
                if (GalaxyMapGlobals.MissionsDirty == 0)
                {
                    hasMission = false;
                }
            }
            if (isCurrentOrLinkedSystem || hasMission || drawPreviewIcon != 0)
            {
                short px = (short)(mapCx + syst.XPos / zoom - camX / zoom);
                short py = (short)(mapCy + syst.YPos / zoom - camY / zoom);

                dotRect[0] = py; dotRect[1] = px;
                dotRect[2] = py; dotRect[3] = px;
                MacToolbox.InsetRect(dotRect, -4, -4);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintOval(dotRect);
                ResolveSystMapColor.Run(systIdx);
                MacToolbox.FrameOval(dotRect);

                if (systIdx == GameData.Player.CurrentSystem)
                {
                    selRect[0] = dotRect[0]; selRect[1] = dotRect[1];
                    selRect[2] = dotRect[2]; selRect[3] = dotRect[3];
                    MacToolbox.InsetRect(selRect, 2, 2);
                    MacToolbox.ForeColor(QuickDrawColor.Cyan);
                    MacToolbox.PaintOval(selRect);
                }

                if (hasMission)
                {
                    MacToolbox.SetRect(iconRect, (short)(dotRect[1] - 16), (short)(dotRect[0] - 16), dotRect[1], dotRect[0]);
                    short pixelInset = 0;
                    if (GalaxyMapGlobals.ZoomDetailNearThreshold < zoom) pixelInset = 8;
                    else if (GalaxyMapGlobals.ZoomDetailNearThreshold == zoom) pixelInset = 8;
                    else pixelInset = 0;

                    iconRect[1] += pixelInset;
                    iconRect[0] += pixelInset;
                    if (GalaxyMapState.MissionDestinationIcon != 0)
                    {
                        MacToolbox.PlotCIcon(iconRect, GalaxyMapState.MissionDestinationIcon);
                    }
                }

                if (drawPreviewIcon != 0)
                {
                    MacToolbox.SetRect(iconRect, dotRect[3], (short)(dotRect[0] - 16), (short)(dotRect[3] + 16), dotRect[0]);
                    short pixelInset = 0;
                    if (GalaxyMapGlobals.ZoomDetailNearThreshold < zoom) pixelInset = 8;
                    else if (GalaxyMapGlobals.ZoomDetailNearThreshold == zoom) pixelInset = 8;
                    else pixelInset = 0;

                    iconRect[3] -= pixelInset;
                    iconRect[0] += pixelInset;
                    if (GalaxyMapState.PreviewTargetIcon != 0)
                    {
                        MacToolbox.PlotCIcon(iconRect, GalaxyMapState.PreviewTargetIcon);
                    }
                }
            }
        }
    }

    private static void DrawCrosshair(int mapCx, int mapCy, int camX, short camY, double zoom)
    {
        if (GalaxyMapState.CentredSystem != -1)
        {
            short px = (short)(mapCx + GameData.Systems[GalaxyMapState.CentredSystem].XPos / zoom - camX / zoom);
            short py = (short)(mapCy + GameData.Systems[GalaxyMapState.CentredSystem].YPos / zoom - camY / zoom);
            short[] dotRect = new short[4];
            dotRect[0] = py; dotRect[1] = px;
            dotRect[2] = py; dotRect[3] = px;
            MacToolbox.InsetRect(dotRect, -6, -6);
            MacToolbox.RGBForeColor((uint)UiColors.Neutral);
            MacToolbox.PenSize(1, 1);
            MacToolbox.MoveTo(dotRect[1] + 1, dotRect[0]);
            MacToolbox.LineTo(dotRect[1] + 4, dotRect[0]);
            MacToolbox.MoveTo(dotRect[1] + 1, dotRect[2]);
            MacToolbox.LineTo(dotRect[1] + 4, dotRect[2]);
            MacToolbox.MoveTo(dotRect[3] - 1, dotRect[0]);
            MacToolbox.LineTo(dotRect[3] - 4, dotRect[0]);
            MacToolbox.MoveTo(dotRect[3] - 1, dotRect[2]);
            MacToolbox.LineTo(dotRect[3] - 4, dotRect[2]);
            MacToolbox.MoveTo(dotRect[1], dotRect[0] + 1);
            MacToolbox.LineTo(dotRect[1], dotRect[0] + 4);
            MacToolbox.MoveTo(dotRect[1], dotRect[2] - 1);
            MacToolbox.LineTo(dotRect[1], dotRect[2] - 4);
            MacToolbox.MoveTo(dotRect[3], dotRect[0] + 1);
            MacToolbox.LineTo(dotRect[3], dotRect[0] + 4);
            MacToolbox.MoveTo(dotRect[3], dotRect[2] - 1);
            MacToolbox.LineTo(dotRect[3], dotRect[2] - 4);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
    }

    private static void DrawSystemLabels(int mapCx, int mapCy, int camX, short camY, double zoom)
    {
        MacToolbox.ForeColor(QuickDrawColor.White);
        foreach (var syst in GameData.Systems)
        {
            if (syst.ShownFlag != 0 && syst.Visited > 0 && zoom <= 1.75)
            {
                int screenX = (int)(7.0 + mapCx + syst.XPos / zoom - camX / zoom);
                int screenY = (int)(4.0 + mapCy + syst.YPos / zoom - camY / zoom);
                MacToolbox.MoveTo(screenX, screenY);
                MacToolbox.DrawString(syst.Name);
            }
        }
    }

    private static void DrawItem2InfoBox(short[] itemRect)
    {
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 2, 0, 0, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
        {
            short mapTop = itemRect[0], mapLeft = itemRect[1];
            short mapBottom = itemRect[2], mapRight = itemRect[3];

            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);

            short[] selRect = new short[4];
            MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 6, 0, 0, selRect);
            MacToolbox.MoveTo(selRect[1] + 1, mapTop);
            MacToolbox.LineTo(selRect[3] - 2, mapTop);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);

            short selSys = GalaxyMapState.CentredSystem;

            MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
            MacToolbox.MoveTo(mapLeft + 10, mapTop + 12);
            MacToolbox.DrawString("Ports:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(mapLeft + 45, mapTop + 12);
            if (GameData.Systems[selSys].Visited < 1)
            {
                MacToolbox.DrawString("<Unknown>");
            }
            else
            {
                int portCount = 0;
                short textX = 0;
                short drawnPortCount = 0;
                for (int linkIdx = 0; linkIdx < StellarLinkCount; linkIdx++)
                {
                    short spobIdx = GameData.Systems[selSys].StellarLink[linkIdx];
                    if (spobIdx != -1 &&
                        ((SpobFlags)GameData.Spobs[spobIdx].Flags & SpobFlags.Uninhabited) == 0 &&
                        ((SpobFlags)GameData.Spobs[spobIdx].Flags & SpobFlags.Landable) != 0)
                    {
                        portCount++;
                    }
                }
                if (portCount < 1)
                {
                    MacToolbox.DrawString("None");
                }
                else
                {
                    for (int linkIdx = 0; linkIdx < StellarLinkCount; linkIdx++)
                    {
                        short spobIdx = GameData.Systems[selSys].StellarLink[linkIdx];
                        if (spobIdx != -1 &&
                            ((SpobFlags)GameData.Spobs[spobIdx].Flags & SpobFlags.Uninhabited) == 0 &&
                            ((SpobFlags)GameData.Spobs[spobIdx].Flags & SpobFlags.Landable) != 0)
                        {
                            short nameWidth = (short)MacToolbox.StringWidth(GameData.Spobs[spobIdx].Name);
                            if (selRect[3] - 20 < mapLeft + nameWidth + textX + 45)
                            {
                                MacToolbox.MoveTo(mapLeft + 45, mapTop + 24);
                                textX = 0;
                            }
                            else
                            {
                                textX += nameWidth;
                            }
                            MacToolbox.DrawString(GameData.Spobs[spobIdx].Name);
                            drawnPortCount++;
                            if (drawnPortCount < portCount)
                            {
                                MacToolbox.DrawString(", ");
                                short commaWidth = (short)MacToolbox.StringWidth(", ");
                                textX += commaWidth;
                            }
                        }
                    }
                }
            }

            MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
            MacToolbox.MoveTo(mapLeft + 10, mapTop + 36);
            MacToolbox.DrawString("Navigation Hazards:");
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(mapLeft + 110, mapTop + 36);
            if (GameData.Systems[selSys].Visited < 1)
            {
                MacToolbox.DrawString("<Unknown>");
            }
            else
            {
                if (GameData.Systems[selSys].AsteroidCount > 0)
                {
                    if (GameData.Systems[selSys].AsteroidCount < 4) MacToolbox.DrawString("Sparse ");
                    else if (GameData.Systems[selSys].AsteroidCount < 7) MacToolbox.DrawString("Moderate ");
                    else MacToolbox.DrawString("Dense ");
                    MacToolbox.DrawString("asteroid field");
                    if (GameData.Systems[selSys].Interference > 0)
                    {
                        MacToolbox.DrawString(", ");
                    }
                }
                if (GameData.Systems[selSys].Interference > 0)
                {
                    if (GameData.Systems[selSys].Interference < 34) MacToolbox.DrawString("Light ");
                    else if (GameData.Systems[selSys].Interference < 67) MacToolbox.DrawString("Moderate ");
                    else MacToolbox.DrawString("Heavy ");
                    MacToolbox.DrawString("sensor interference");
                }
                if (GameData.Systems[selSys].AsteroidCount < 1 && GameData.Systems[selSys].Interference < 1)
                {
                    MacToolbox.DrawString("None");
                }
            }

            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            string dateStr = FormatDateNumeric.Format(GameDate.Current.Year, GameDate.Current.Month, GameDate.Current.Day);
            int dateTextWidth = MacToolbox.StringWidth(dateStr);
            MacToolbox.MoveTo(mapRight - dateTextWidth - 5, mapTop + 36);
            MacToolbox.DrawString(dateStr);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            GalaxyMapState.ResetFlag = 1;
        }
    }

    private static void DrawItem6Panel()
    {
        short[] itemRect = new short[4];
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 6, 0, 0, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(GalaxyMapState.MapDialog)))
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.MoveTo(itemRect[1] + 1, itemRect[2] - 1);
            MacToolbox.LineTo(itemRect[3] - 2, itemRect[2] - 1);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 12);
            MacToolbox.RGBForeColor((uint)UiColors.Unexplored);

            if (GameData.Player.NavMode == 3)
            {
                if (GameData.Player.NavTargetSpob == -1)
                {
                    if (GalaxyMapState.CentredSystem == GameData.Player.CurrentSystem) MacToolbox.DrawString("Current System:");
                    else MacToolbox.DrawString("Selected System:");
                }
                else MacToolbox.DrawString("Destination System:");
            }
            else if (GalaxyMapState.CentredSystem == GameData.Player.CurrentSystem)
            {
                MacToolbox.DrawString("Current System:");
            }
            else
            {
                MacToolbox.DrawString("Selected System:");
            }

            MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 26);
            MacToolbox.ForeColor(QuickDrawColor.White);

            short selSys = GalaxyMapState.CentredSystem;
            if (GameData.Systems[selSys].Visited < 1)
            {
                MacToolbox.DrawString("<Unknown>");
            }
            else
            {
                MacToolbox.DrawString(GameData.Systems[selSys].Name);
                if (!HasVisibleStellars.Run(selSys))
                {
                    MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 75);
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.DrawString("Uninhabited System");
                }
                else
                {
                    MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
                    MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 75);
                    MacToolbox.DrawString("Government:");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 88);
                    if (GameData.Systems[selSys].Govt == -1)
                    {
                        MacToolbox.DrawString("Independent");
                    }
                    else
                    {
                        MacToolbox.DrawString(GameData.Governments[GameData.Systems[selSys].Govt].Name);
                    }
                    MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 119);
                    MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
                    MacToolbox.DrawString("Legal Status:");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 132);
                    ResolveSystLegalStatusCategory.Run(selSys);
                    MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
                    MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 155);
                    MacToolbox.DrawString("Goods Traded:");
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    if (GameData.Systems[selSys].Visited < 2)
                    {
                        MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 168);
                        MacToolbox.DrawString("<Unknown>");
                    }
                    else
                    {
                        short yOffset = 0;
                        for (short goodsIdx = 0; goodsIdx < CommodityCount; goodsIdx++)
                        {
                            if (SystSellsCommodity.Run(selSys, goodsIdx) != 0)
                            {
                                MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + yOffset + 168);
                                MacToolbox.DrawString(ResourceGlobals.NamesStr4000[goodsIdx]);
                                yOffset += 12;
                            }
                        }
                        if (yOffset == 0)
                        {
                            MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 168);
                            MacToolbox.DrawString("None");
                        }
                    }

                    MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
                    MacToolbox.MoveTo(itemRect[1] + 10, itemRect[0] + 250);
                    MacToolbox.DrawString("Services:");
                    short serviceCount = 0;
                    for (short shopIdx = 0; shopIdx < ShopTypeCount; shopIdx++)
                    {
                        if (SystHasShopType.Run(selSys, shopIdx))
                        {
                            serviceCount++;
                        }
                    }
                    if (serviceCount < 1)
                    {
                        if (GameData.Systems[selSys].Visited < 2)
                        {
                            if (GameData.Systems[selSys].Visited > 0) // guard always true
                            {
                                MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 262);
                                MacToolbox.ForeColor(QuickDrawColor.White);
                                MacToolbox.DrawString("<Unknown>");
                            }
                        }
                        else
                        {
                            MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 262);
                            MacToolbox.ForeColor(QuickDrawColor.White);
                            MacToolbox.DrawString("None Available");
                        }
                    }
                    else
                    {
                        MacToolbox.ForeColor(QuickDrawColor.White);
                        if (GameData.Systems[selSys].Visited < 2)
                        {
                            MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + 263);
                            MacToolbox.DrawString("<Unknown>");
                        }
                        else
                        {
                            short yOffset = 0;
                            for (short shopIdx = 0; shopIdx < ShopTypeCount; shopIdx++)
                            {
                                if (SystHasShopType.Run(selSys, shopIdx))
                                {
                                    MacToolbox.MoveTo(itemRect[1] + 15, itemRect[0] + yOffset + 263);
                                    if (shopIdx == 0) MacToolbox.DrawString("Trading");
                                    if (shopIdx == 1) MacToolbox.DrawString("Outfitting");
                                    if (shopIdx == 2) MacToolbox.DrawString("Shipyard");
                                    yOffset += 12;
                                }
                            }
                        }
                    }
                }
            }
            GalaxyMapState.ResetFlag = 1;
        }
    }
}
