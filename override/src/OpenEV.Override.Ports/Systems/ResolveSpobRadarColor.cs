using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Systems;

// FUN_1005b8b4 (EV Override-11.c lines 37785-37832) — set the QuickDraw fore-colour for a spob's
// radar dot: not landable -> dialog-fore colour; uninhabited -> blue; otherwise, while not
// currently trading, yellow (no government, or the system's legal status clears the spob's
// coolness requirement), red/orange while restricted, else green once trading is active.
public static class ResolveSpobRadarColor
{
    public static void Run(int spobIndex)
    {
        var spob = Core.Model.GameData.Spobs[spobIndex];
        if ((spob.Flags & (int)SpobFlags.Landable) == 0)
        {
            MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        }
        else if ((spob.Flags & (int)SpobFlags.Uninhabited) == 0)
        {
            if (spob.TradingEnabled == 0)
            {
                if (spob.Govt == -1)
                {
                    MacToolbox.ForeColor(QuickDrawColor.Yellow);
                }
                // NOTE (faithful): original decompile bug — duplicate of the branch above,
                // unreachable (EV Override-11.c line 37803, same test as line 37800).
                else if (spob.Govt == -1)
                {
                    MacToolbox.ForeColor(QuickDrawColor.Yellow);
                }
                else if (GalaxyMapGlobals.SystemStatus(spob.System) < spob.MinCoolness)
                {
                    if (GalaxyMapGlobals.SystemStatus(spob.System) < 0)
                    {
                        MacToolbox.ForeColor(QuickDrawColor.Red);
                    }
                    else
                    {
                        MacToolbox.RGBForeColor(UiColorConstants.RestrictedNavWarning);
                    }
                }
                else
                {
                    MacToolbox.ForeColor(QuickDrawColor.Yellow);
                }
            }
            else
            {
                MacToolbox.ForeColor(QuickDrawColor.Green);
            }
        }
        else
        {
            MacToolbox.ForeColor(QuickDrawColor.Blue);
        }
    }
}
