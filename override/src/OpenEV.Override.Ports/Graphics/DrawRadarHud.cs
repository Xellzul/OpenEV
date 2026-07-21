using OpenEV.Override.Ports.Ship;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100554ac (EV Override-11.c lines 34972-35270): draw the in-game radar HUD — the
// spob blips, ship blips (coloured by hostility/target), the player dot, and the nav arrow —
// into the backdrop GWorld, then composite to screen. Interference/jamming blanks or
// randomises it.
public static class DrawRadarHud
{
    public static void Run(byte mapMode)
    {
        var player = ShipTable.Player;

        int friendlyColor = (int)UiColors.Friendly;
        int neutralColor = (int)UiColors.Neutral;
        int hostileColor = (int)UiColors.Unexplored;
        bool colorRadar = ShipDerivedStats.HasIffRadar(player);
        bool wideBlips = ShipDerivedStats.HasDensityScanner(player);

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);

        // Rects are {top, left, bottom, right}; SetRect takes (rect, left, top, right, bottom).
        short[] radarRect = new short[4];
        MacToolbox.SetRect(radarRect,
                           (short)(GlobalState.PortRight - 139),
                           (short)(GlobalState.PortTop + 4),
                           (short)(GlobalState.PortRight - 6),
                           (short)(GlobalState.PortTop + 138));
        short[] srcRect = new short[4];
        MacToolbox.SetRect(srcRect, 5, 4, 138, 138);

        uint centreX = 0;
        // centreY stays 0 on the jammed path (matches the decompile's uninitialized unaff_r23).
        uint centreY = 0;

        int reduction = ShipDerivedStats.InterferenceReduction();
        short interference = SystTable.Store[player.CurrentSystem].Interference;
        short roll = (short)SeedEvoRng.Run(100);
        if (interference - reduction < roll + 1)
        {
            // Radar visible: paint the dish, compute its centre, plot the blips.
            MacToolbox.ForeColor(QuickDrawColor.Black);
            if (!colorRadar)
                MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2,
                                    srcRect, radarRect, 0, 0);
            else
                MacToolbox.PaintRect(radarRect);

            centreX = (uint)(0.5 * (radarRect[1] + radarRect[3]));
            centreY = (uint)(0.5 * (radarRect[0] + radarRect[2]));

            if (mapMode == 0)
            {
                short[] blip = new short[4];

                // Spob blips: up to 4 stellar objects linked from the current system.
                for (int link = 0; link < SystRecord.StellarLinkCount; link++)
                {
                    int spob = (int)SystTable.SpobLink(player.CurrentSystem, link);
                    if (spob == -1)
                        continue;
                    var rec = GameData.Spobs[spob];

                    // Subtract the player's FLOAT worldX/worldY from the spob's short coords.
                    // The (int)(uint)(int) chain truncates-then-reinterprets to match the PPC
                    // float->uint conversion; .NET's direct (uint)(float) SATURATES negatives to
                    // 0, so do not collapse the middle (int).
                    int dx = (int)(uint)(int)((float)(int)rec.XPos - player.PosX);
                    int dy = (int)(uint)(int)((float)(int)rec.YPos - player.PosY);
                    SetBlipPoint(blip, RadarBlipCoord(dx, centreX),
                                       RadarBlipCoord(dy, centreY));

                    if (!colorRadar)
                        MacToolbox.RGBForeColor((uint)friendlyColor);
                    else
                        ResolveSpobRadarColor.Run(spob);

                    if (((SpobFlags)rec.Flags & SpobFlags.Station) == 0)
                    {
                        short spriteW = (short)MacRectWidth.Run(
                            PlanetSpriteRecordTable.Store[rec.SpriteId]);
                        short inset = (short)(spriteW < 64 ? -1 : -2);
                        MacToolbox.InsetRect(blip, inset, inset);
                        MacToolbox.FrameOval(blip);
                    }
                    else if (!wideBlips)
                    {
                        MacToolbox.MoveTo(blip[1], blip[0]);
                        MacToolbox.Line(0, 0);
                    }
                    else
                    {
                        MacToolbox.InsetRect(blip, (short)-1, (short)-1);
                        MacToolbox.FrameRect(blip);
                    }
                }

                // Ship blips: slots 1..35, only those alive and in the player's system.
                for (int slot = 1; slot < ShipTable.Count; slot++)
                {
                    var ship = ShipTable.Ships[slot];
                    if (ship.IsActive == 0)
                        continue;
                    if (player.CurrentSystem != ship.CurrentSystem)
                        continue;

                    int dx = (int)(uint)(int)(ship.PosX - player.PosX);
                    int dy = (int)(uint)(int)(ship.PosY - player.PosY);
                    SetBlipPoint(blip, RadarBlipCoord(dx, centreX),
                                       RadarBlipCoord(dy, centreY));

                    if (player.TargetSlot == slot)
                    {
                        if (RenderGlobals.RadarHudAnimTick < 16)
                        {
                            if (!colorRadar)
                                MacToolbox.RGBForeColor((uint)friendlyColor);
                            else
                                ResolveRadarDotColor.Run(ship);
                        }
                        else if (!colorRadar)
                            MacToolbox.RGBForeColor((uint)neutralColor);
                        else
                            MacToolbox.RGBForeColor((uint)hostileColor);
                    }
                    else if (!colorRadar)
                        MacToolbox.RGBForeColor((uint)friendlyColor);
                    else
                        ResolveRadarDotColor.Run(ship);

                    if (!wideBlips || GameData.ShipClasses[ship.ShipClass].Mass < 100)
                    {
                        MacToolbox.MoveTo(blip[1], blip[0]);
                        MacToolbox.Line(0, 0);
                    }
                    else
                    {
                        MacToolbox.InsetRect(blip, (short)-1, (short)-1);
                        MacToolbox.FrameRect(blip);
                    }
                }
            }
        }
        else
        {
            // Radar jammed: random static-colour fill (the managed RadarJamColorTable).
            uint jamIndex = (uint)(short)SeedEvoRng.Run(10);
            centreX = jamIndex;
            int jamColor = RenderGlobals.RadarJamColorTable[(int)jamIndex];
            if (jamColor != 0)
                MacToolbox.FillCRect(radarRect, jamColor);
        }

        // Player dot, always at the radar centre.
        if (!colorRadar)
            MacToolbox.RGBForeColor((uint)neutralColor);
        else
            ResolveRadarDotColor.Run(player);
        MacToolbox.MoveTo((int)centreX, (int)centreY);
        MacToolbox.Line(0, 0);

        DrawNavArrow(centreX, centreY, friendlyColor);

        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2,
                            radarRect, radarRect, 0, 0);
    }

    // Blip scale: toc-0x64a8, dbl 0.03125 (2^-5).
    private const double BlipScale = 0.03125;

    // World->radar transform: scale the world delta and offset by the radar centre. Matches the
    // four identical coordinate computations in the decompile (spob X/Y, ship X/Y).
    private static int RadarBlipCoord(int worldDelta, uint centre)
        => (int)(BlipScale * worldDelta + (int)centre);

    // Collapse a blip to a degenerate rect (a single point) at (h, v) so the InsetRect/Frame*
    // calls grow it from that point.
    private static void SetBlipPoint(short[] blip, int h, int v)
    {
        blip[1] = (short)h; // left
        blip[0] = (short)v; // top
        blip[2] = blip[0];  // bottom == top
        blip[3] = blip[1];  // right  == left
    }

    // Nav arrow: when not target-flashing and the player is far from the system centre, draw an
    // arrow from the radar centre back toward (0,0).
    private static void DrawNavArrow(uint centreX, uint centreY, int friendlyColor)
    {
        var player = ShipTable.Player;
        if (RenderGlobals.RadarHudAnimTick >= 16 || player.NavMode == 3)
            return;

        int destinations = 0;
        for (int link = 0; link < SystRecord.StellarLinkCount; link++)
            if (SystTable.SpobLink(player.CurrentSystem, link) != -1)
                destinations++;
        if (destinations <= 0)
            return;

        // Seed point = the system centre (toc-0x64b0 = 0.0f).
        float p1x = 0.0f, p1y = 0.0f;
        double range = EvMath.DistanceSquared(p1x, p1y, player.PosX, player.PosY);
        if (range <= 7000000.0)
            return;

        int heading = EvMath.HeadingBetween(player.PosX, player.PosY, p1x, p1y);
        int headingShort = (int)(short)heading;

        // Arrow tip = radar centre, in screen space.
        float tipX = (float)(int)centreX;
        float tipY = (float)(int)centreY;
        p1x = tipX; p1y = tipY;
        float p2x = tipX, p2y = tipY;

        // Shaft from P1 (25px out) to P2 (50px out) along the heading.
        EvMath.OffsetByHeading(25.0, headingShort, ref p1x, ref p1y);
        EvMath.OffsetByHeading(50.0, headingShort, ref p2x, ref p2y);
        MacToolbox.RGBForeColor((uint)friendlyColor);
        MacToolbox.MoveTo((int)p1x, (int)p1y);
        MacToolbox.LineTo((int)p2x, (int)p2y);

        // First barb: from P2, offset by heading + 135.
        p1x = p2x; p1y = p2y;
        EvMath.OffsetByHeading(6.0, headingShort + 135, ref p1x, ref p1y);
        MacToolbox.MoveTo((int)p2x, (int)p2y);
        MacToolbox.LineTo((int)p1x, (int)p1y);

        // Second barb: from P2, offset by heading - 135.
        p1x = p2x; p1y = p2y;
        EvMath.OffsetByHeading(6.0, headingShort - 135, ref p1x, ref p1y);
        MacToolbox.MoveTo((int)p2x, (int)p2y);
        MacToolbox.LineTo((int)p1x, (int)p1y);

        MacToolbox.ForeColor(QuickDrawColor.Black);
    }
}
