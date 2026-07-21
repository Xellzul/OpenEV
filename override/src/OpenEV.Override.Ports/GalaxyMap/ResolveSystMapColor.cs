using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_1005b9f4 (EV Override-11.c lines 37833-37907)
// set the QuickDraw fore-colour for system `systemIndex` on the galaxy
// map. Unexplored-on-map (Visited < 1) -> frame colour; explored but no visible
// stellars -> "unknown" colour; otherwise blue, upgraded to a nav/legal
// colour from the system's landable spaceports vs. the player's per-system status,
// and finally the spaceport colour if any spaceport is present.
public static class ResolveSystMapColor
{
    private const int ScannedSpobLinks = 3;   // FUN_1005b9f4 scans only the first 3 of the 4 StellarLink slots

    public static void Run(short systemIndex)
    {
        var syst = SystTable.Store[systemIndex];
        if (syst.Visited < 1)
        {
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            return;
        }

        if (!HasVisibleStellars.Run(systemIndex))
        {
            MacToolbox.RGBForeColor((uint)UiColors.Unexplored);
            return;
        }

        MacToolbox.ForeColor(QuickDrawColor.Blue);   // blueColor — default explored
        short systStatus = GalaxyMapGlobals.SystemStatus(systemIndex);
        short spaceportCount = 0;
        short nonTradingSpobCount = 0;   // ASM r24 — original write-only counter, incremented but never read (the decompile elided it)
        short navState = 0;

        for (int slot = 0; slot < ScannedSpobLinks; slot++)
        {
            short spobIdx = SystTable.SpobLink(systemIndex, slot);
            if (spobIdx != -1)
            {
                var spob = GameData.Spobs[spobIdx];
                if (spob.Visible != 0 &&
                    (spob.Flags & (int)SpobFlags.Uninhabited) == 0 &&
                    (spob.Flags & (int)SpobFlags.Landable) != 0)
                {
                    if (systStatus < spob.MinCoolness)
                    {
                        if (systStatus >= 0 && navState == 0)
                            navState = 1;
                        if (systStatus < 0)
                            navState = 2;
                    }

                    if (spob.TradingEnabled != 0)
                        spaceportCount++;
                    else
                        nonTradingSpobCount++;   // original dead store (r24, never read)
                }
            }

            if (navState == 1)
                MacToolbox.RGBForeColor(UiColorConstants.RestrictedNavWarning);
            if (navState == 2)
                MacToolbox.ForeColor(QuickDrawColor.Red);            // redColor — hostile
        }

        if (spaceportCount > 0)
        {
            MacToolbox.RGBForeColor((uint)UiColors.Neutral);
        }
    }
}
