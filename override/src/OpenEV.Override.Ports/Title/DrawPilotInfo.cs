using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10043a8c (EV Override-11.c lines 28069-28212). Renders the title
// screen's right-side pilot info panel: a centered "No Pilot File Loaded"
// message, the pilot/ship/system/legal-status/combat-rating/date report, or —
// if the player ship is dying or destroyed — a "has been killed" (or "Kenny"
// easter egg) message.
public static class DrawPilotInfo
{
    public static void Run(byte repaint)
    {
        short[] arena = TitleScreenGlobals.InnerArenaRect;
        int labelColor = UiColors.Friendly;
        int valueColor = UiColors.Neutral;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.BeginUpdate(GlobalState.ActivePortPixmap);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(RenderGlobals.BackdropPortRect);
        // Stack copy of the shared backdrop rect global — the same clone-before-use
        // pattern the other backdrop callers use (AnimateRowReveal, DrawClosedButtons,
        // TitleMainLoop); keeps this local copy from aliasing the shared array.
        short[] backdropRect = (short[])TitleScreenGlobals.BackdropRect.Clone();
        if (TitleScreenGlobals.Pict8000Handle == 0)
        {
            TitleScreenGlobals.Pict8000Handle = MacToolbox.GetPicture(8000);
        }
        if (TitleScreenGlobals.Pict8000Handle == 0)
        {
            SetGamePortAndDevice.Run();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(GlobalState.PortRect);
            FatalGraphicsResourceExit.Run();
        }
        else
        {
            MacToolbox.DrawPicture(TitleScreenGlobals.Pict8000Handle, backdropRect);
        }
        short[] panelRect = new short[4];
        MacToolbox.SetRect(panelRect, (short)(arena[1] + 277), (short)(arena[0] + 266), (short)(arena[1] + 369), (short)(arena[0] + 446));
        short panelTop = panelRect[0];
        short panelLeft = panelRect[1];
        short panelBottom = panelRect[2];
        short panelRight = panelRect[3];
        // Centre line of the panel (PPC signed /2 with negative-odd correction —
        // do not simplify to a bare `>> 1`, it would floor instead of truncate).
        int mid = panelTop + panelBottom;
        short panelMidY = (short)((mid >> 1) + ((mid < 0 && (mid & 1) != 0) ? 1 : 0));
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        if (!WorldState.PilotLoaded)
        {
            MacToolbox.RGBForeColor((uint)labelColor);
            DrawCenteredString.Run("No Pilot File Loaded", panelLeft, panelRight, (short)(panelMidY - 20));
        }
        else
        {
            if (!ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[0]))
            {
                MacToolbox.MoveTo(panelLeft, panelTop + 12);
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.DrawString("Pilot Name:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 24);
                MacToolbox.RGBForeColor((uint)valueColor);
                MacToolbox.DrawString(PilotIdentity.Name);
                MacToolbox.MoveTo(panelLeft, panelTop + 39);
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.DrawString("Ship Name:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 51);
                MacToolbox.RGBForeColor((uint)valueColor);
                MacToolbox.DrawString(PilotIdentity.ShipName);
                MacToolbox.MoveTo(panelLeft, panelTop + 66);
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.DrawString("Ship Type:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 78);
                MacToolbox.RGBForeColor((uint)valueColor);
                MacToolbox.DrawString(ResourceGlobals.ShipClassName(GameData.Player.ShipClass));
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.MoveTo(panelLeft, panelTop + 93);
                MacToolbox.DrawString("Legal status in");
                MacToolbox.MoveTo(panelLeft, panelTop + 105);
                MacToolbox.DrawString(SystTable.Store[GameData.Player.CurrentSystem].Name);
                MacToolbox.DrawString(" system:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 117);
                MacToolbox.RGBForeColor((uint)valueColor);
                if (!HasVisibleStellars.Run(GameData.Player.CurrentSystem))
                {
                    // The data-seg Pascal string here is "N/A" — an earlier bridge
                    // substituted "(Unknown)", which does not match the binary.
                    MacToolbox.DrawString("N/A");
                }
                else
                {
                    ResolveSystLegalStatusCategory.Run(GameData.Player.CurrentSystem);
                }
                MacToolbox.MoveTo(panelLeft, panelTop + 132);
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.DrawString("Combat Rating:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 144);
                MacToolbox.RGBForeColor((uint)valueColor);
                DrawCombatRatingName.Run();
                MacToolbox.MoveTo(panelLeft, panelTop + 159);
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.DrawString("Current Date:");
                MacToolbox.MoveTo(panelLeft + 5, panelTop + 171);
                MacToolbox.RGBForeColor((uint)valueColor);
                MacToolbox.DrawString(FormatDateLong.Run(
                    GameDate.Current.Year, GameDate.Current.Month, GameDate.Current.Day));
            }
            else
            {
                MacToolbox.RGBForeColor((uint)labelColor);
                if (PilotIdentity.Name.StartsWith("Kenny"))
                {
                    // "They killed Kenny!" easter egg.
                    DrawCenteredString.Run("Oh my God!", panelLeft, panelRight, (short)(panelMidY - 12));
                    DrawCenteredString.Run("They killed Kenny!", panelLeft, panelRight, panelMidY);
                }
                else
                {
                    DrawCenteredString.Run(PilotIdentity.Name, panelLeft, panelRight, (short)(panelMidY - 12));
                    DrawCenteredString.Run("has been killed", panelLeft, panelRight, panelMidY);
                }
            }
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        if (repaint != 0)
        {
            // Repaint the visible screen from the backdrop GWorld.
            MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2,
                                GlobalState.ActivePortPixmap + 2,
                                RenderGlobals.BackdropPortRect,
                                GlobalState.PortRect, 0, 0);
        }
        MacToolbox.EndUpdate(GlobalState.ActivePortPixmap);
        return;
    }
}
