using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1005d69c (EV Override-11.c lines 38756-38792).
public static class SystSellsCommodity
{
    public static int Run(short systemIndex, int commodityIndex)
    {
        foreach (short spobIdx in SystTable.Store[systemIndex].StellarLink)
        {
            if (spobIdx != -1 &&
                GameData.Spobs[spobIdx].Visible != 0 &&
                ((SpobFlags)GameData.Spobs[spobIdx].Flags & SpobFlags.Uninhabited) == 0)
            {
                var spobFlags = (SpobFlags)GameData.Spobs[spobIdx].Flags;
                if ((spobFlags & SpobFlags.Landable) != 0 && (spobFlags & SpobFlags.Exchange) != 0 &&
                    (short)CommodityPriceMode.Run((short)commodityIndex, (uint)spobFlags) != 0)
                {
                    return 1;
                }
            }
        }

        return 0;
    }
}
