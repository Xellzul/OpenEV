using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_10054158 (EV Override-11.c lines 34383-34415).
public static class ResetCommodityPriceLimits
{
    public static void Run(byte resetCounter)
    {
        if (resetCounter != 0)
        {
            WorldState.PlayerCombatRating = 0;
        }

        for (short index = 0; index < SystTable.Count; index = (short)(index + 1))
        {
            short syGovt = SystTable.Store[index].Govt;
            short minPrice = syGovt < 0 ? (short)0 : GameData.Governments[syGovt].InitialRecord;
            if (GalaxyMapGlobals.SystemStatus(index) < minPrice)
            {
                GalaxyMapGlobals.SetSystemStatus(index, minPrice);
            }
        }

        foreach (var spob in GameData.Spobs)
        {
            spob.TradingEnabled = 0;
        }
    }
}
