using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10055ebc (EV Override-11.c lines 35271-35384): the NAV/destination
// HUD panel (right column, rows 192..227) — "Nav System Off" / "Stellar Navigation"
// + spob name / "Hyperspace" + destination system name, coloured by whether a jump
// is currently possible.
public static class DrawTargetShipInfoPanel
{
    public static void Run()
    {
        // Managed {top@0,left@1,bottom@2,right@3} short[4] Rects (were 8-byte Mac
        // stack rects passed by address).
        short[] panelRect = new short[4];
        short[] backdropRect = new short[4];

        int labelColor = UiColors.Friendly;
        int highlightColor = UiColors.Neutral;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.SetRect(panelRect,
                           (short)(GlobalState.PortRight + -139),
                           (short)(GlobalState.PortTop + 192),
                           (short)(GlobalState.PortRight + -6),
                           (short)(GlobalState.PortTop + 227));
        MacToolbox.SetRect(backdropRect, 5, 192, 138, 227);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, backdropRect, panelRect, 0, 0);
        MacToolbox.TextFont(2020);
        MacToolbox.TextSize(14);
        if (GameData.Player.NavMode == -1)
        {
            MacToolbox.RGBForeColor((uint)labelColor);
            DrawCenteredString.Run("Nav System Off", panelRect[1], panelRect[3], (short)(panelRect[0] + 22));
        }
        if (GameData.Player.NavMode == 2)
        {
            MacToolbox.RGBForeColor((uint)labelColor);
            DrawCenteredString.Run("Stellar Navigation", panelRect[1], panelRect[3], (short)(panelRect[0] + 12));
            if (GameData.Player.NavTargetSpob == -1)
            {
                MacToolbox.RGBForeColor((uint)labelColor);
                DrawCenteredString.Run("No Destination", panelRect[1], panelRect[3], (short)(panelRect[0] + 30));
            }
            else
            {
                MacToolbox.RGBForeColor((uint)highlightColor);
                DrawCenteredString.Run(GameData.Spobs[GameData.Player.NavTargetSpob].Name,
                                       panelRect[1], panelRect[3], (short)(panelRect[0] + 30));
            }
        }
        if (GameData.Player.NavMode == 3)
        {
            if (GameData.Player.JumpWindupTimer < 1)
            {
                MacToolbox.RGBForeColor((uint)labelColor);
            }
            else
            {
                MacToolbox.RGBForeColor((uint)highlightColor);
            }
            DrawCenteredString.Run("Hyperspace", panelRect[1], panelRect[3], (short)(panelRect[0] + 12));
            if (GameData.Player.NavTargetSpob == -1)
            {
                MacToolbox.RGBForeColor((uint)labelColor);
                DrawCenteredString.Run("No Destination", panelRect[1], panelRect[3], (short)(panelRect[0] + 30));
            }
            else
            {
                // Seed point = the system centre (0.0f in both axes).
                float seedX = 0.0f, seedY = 0.0f;
                bool allLinksOutOfRange = true;
                double hyperRangeSq = (double)(int)ShipDerivedStats.EffectiveHyperRangeSquared(ShipTable.Player);
                for (short linkIndex = 0; linkIndex < SystRecord.StellarLinkCount; linkIndex = (short)(linkIndex + 1))
                {
                    if (SystTable.SpobLink(GameData.Player.CurrentSystem, linkIndex) != -1)
                    {
                        seedX = 0.0f; seedY = 0.0f;
                        hyperRangeSq = (double)(int)ShipDerivedStats.EffectiveHyperRangeSquared(ShipTable.Player);
                        double targetDistance = EvMath.FloatAbs(
                            EvMath.DistanceSquared(seedX, seedY, GameData.Ships[0].PosX, GameData.Ships[0].PosY));
                        if (targetDistance <= (double)(float)hyperRangeSq)
                        {
                            allLinksOutOfRange = false;
                            break;
                        }
                    }
                }
                if (allLinksOutOfRange || 0 < GameData.Player.JumpWindupTimer)
                {
                    MacToolbox.RGBForeColor((uint)highlightColor);
                }
                else
                {
                    MacToolbox.RGBForeColor((uint)labelColor);
                }
                short destSystemIndex = SystTable.Store[GameData.Player.CurrentSystem].HyperLink[GameData.Player.NavTargetSpob];
                if (SystTable.Store[destSystemIndex].Visited < 1)
                {
                    DrawCenteredString.Run("Unexplored System", panelRect[1], panelRect[3], (short)(panelRect[0] + 30));
                }
                else
                {
                    DrawCenteredString.Run(MacToolbox.PascalToString(SystTable.Store[destSystemIndex].Name),
                                panelRect[1], panelRect[3], (short)(panelRect[0] + 30));
                }
            }
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, panelRect, panelRect, 0, 0);
    }
}
