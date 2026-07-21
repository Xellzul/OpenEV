using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10057b74 (EV Override-11.c lines 35945-36049): redraw the in-game HUD status
// panel — the carried-cargo list, the free-mass line, the "Special:" mission/junk item, and
// the credits line — into the backdrop panel, then composite the strip to screen.
public static class RedrawHudStatusPanel
{
    public static void Run()
    {
        int labelColor = UiColors.Friendly;
        int valueColor = UiColors.Neutral;
        string[] cargoNames = ResourceGlobals.NamesStr0fa3;   // STR# 0xfa3 cargo type names
        string[] junkNamesShort = ResourceGlobals.NamesStr0fa5;   // STR# 0xfa5 junk-commodity short names
        string[] missionCargoNamesShort = ResourceGlobals.NamesStr0fa2;  // STR# 0xfa2 mission-cargo short names

        var srcRect = new short[4];   // panel source rect
        var panelRect = new short[4];   // panel dest rect (src AND dst of the final blit)

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(2020);
        MacToolbox.TextSize(14);
        MacToolbox.SetRect(panelRect, (short)(GlobalState.PortRight - 138), (short)(GlobalState.PortTop + 387),
                         (short)(GlobalState.PortRight - 5), (short)(GlobalState.PortTop + 475));
        MacToolbox.SetRect(srcRect, 6, 387, 139, 475);
        MacToolbox.CopyBits(RenderGlobals.StatusPanelBgGWorld + 2, RenderGlobals.BackdropGWorld + 2, srcRect, panelRect, 0, 0);

        for (int row = 0; row < ShipRecord.CargoHoldCount; row++)
        {
            if (0 < GameData.Ships[0].CargoHold[row])
            {
                MacToolbox.RGBForeColor((uint)labelColor);
                MacToolbox.MoveTo(panelRect[1] + 3, panelRect[0] + (row + 1) * 14 - 2);
                MacToolbox.DrawString(cargoNames[row]);
                MacToolbox.RGBForeColor((uint)valueColor);
                MacToolbox.MoveTo(panelRect[1] + 35, panelRect[0] + (row + 1) * 14 - 2);
                MacToolbox.DrawString(((int)GameData.Ships[0].CargoHold[row]).ToString());
            }
        }
        MacToolbox.MoveTo(panelRect[1] + 67, panelRect[0] + 12);
        MacToolbox.RGBForeColor((uint)labelColor);
        MacToolbox.DrawString("Free:");
        MacToolbox.MoveTo(panelRect[1] + 100, panelRect[0] + 12);
        MacToolbox.RGBForeColor((uint)valueColor);
        // Cast each mass to short before subtracting, matching the decompile's 16-bit truncation.
        short totalMass = (short)TotalMassWithEscorts.Run();
        short massCarried = (short)ShipDerivedStats.TotalMassCarried(ShipTable.Player);
        short freeMass = (short)(totalMass - massCarried);
        if (freeMass < 0)
            freeMass = 0;
        MacToolbox.DrawString(((int)freeMass).ToString());

        short specialCount = 0;
        for (short mission = 0; mission < MissionTable.Count; mission++)
        {
            if (GameData.MissionStates[mission].IsActive != 0 &&
                GameData.Missions[mission].CargoPickedUp != 0 &&
                GameData.Missions[mission].CargoStringIndex != -1)
            {
                specialCount++;
            }
        }
        for (short junk = 0; junk < JunkTable.Count; junk++)
        {
            if (0 < GameData.Junk[junk].PlayerQty)
            {
                specialCount++;
            }
        }
        if (0 < specialCount)
        {
            MacToolbox.MoveTo(panelRect[1] + 67, panelRect[0] + 30);
            MacToolbox.RGBForeColor((uint)labelColor);
            MacToolbox.DrawString("Special:");
            MacToolbox.MoveTo(panelRect[1] + 77, panelRect[0] + 46);
            MacToolbox.RGBForeColor((uint)valueColor);
            if (specialCount == 1)
            {
                for (short mission = 0; mission < MissionTable.Count; mission++)
                {
                    if (GameData.MissionStates[mission].IsActive != 0 &&
                        GameData.Missions[mission].CargoPickedUp != 0 &&
                        GameData.Missions[mission].CargoStringIndex != -1)
                    {
                        MacToolbox.DrawString(missionCargoNamesShort[GameData.Missions[mission].CargoStringIndex]);
                        break;
                    }
                }
                for (short junk = 0; junk < JunkTable.Count; junk++)
                {
                    if (0 < GameData.Junk[junk].PlayerQty)
                    {
                        MacToolbox.DrawString(junkNamesShort[junk]);
                        break;
                    }
                }
            }
            else
            {
                MacToolbox.DrawString("Multiple");
            }
        }
        MacToolbox.MoveTo(panelRect[1] + 67, panelRect[0] + 66);
        MacToolbox.RGBForeColor((uint)labelColor);
        MacToolbox.DrawString("Credits:");
        MacToolbox.MoveTo(panelRect[1] + 77, panelRect[0] + 82);
        MacToolbox.RGBForeColor((uint)valueColor);
        FormatCredits.Run(GameData.Ships[0].Credits);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, panelRect, panelRect, 0, 0);
    }
}
