using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005f9d0 (EV Override-11.c lines 39807-39886) — looks up one of the six standard
// commodities' price mode within a spob's packed Flags word. Each commodity occupies a
// 3-bit field within a 4-bit-aligned slot (commodityIndex 0..5 = Food, Industrial, Medical,
// Luxury, Metal, Equipment, at bits 28-30 down to 8-10); the set bit gives the mode: 1 =
// cheap here, 2 = base price, 4 = expensive, 0 = not sold. Feeds CommodityPricing.PriceMode[]
// (the commodity-exchange dialog) and gates outfit/bar availability checks (SystSellsCommodity,
// BuildBarDescription).
public static class CommodityPriceMode
{
    public static int Run(short commodityIndex, uint spobFlags)
    {
        var flags = (SpobFlags)spobFlags;
        return commodityIndex switch
        {
            0 => GetMode(flags, SpobFlags.PriceLowFood, SpobFlags.PriceMedFood, SpobFlags.PriceHighFood),
            1 => GetMode(flags, SpobFlags.PriceLowIndustrial, SpobFlags.PriceMedIndustrial, SpobFlags.PriceHighIndustrial),
            2 => GetMode(flags, SpobFlags.PriceLowMedical, SpobFlags.PriceMedMedical, SpobFlags.PriceHighMedical),
            3 => GetMode(flags, SpobFlags.PriceLowLuxury, SpobFlags.PriceMedLuxury, SpobFlags.PriceHighLuxury),
            4 => GetMode(flags, SpobFlags.PriceLowMetal, SpobFlags.PriceMedMetal, SpobFlags.PriceHighMetal),
            5 => GetMode(flags, SpobFlags.PriceLowEquipment, SpobFlags.PriceMedEquipment, SpobFlags.PriceHighEquipment),
            _ => 0
        };
    }

    private static int GetMode(SpobFlags flags, SpobFlags low, SpobFlags med, SpobFlags high)
    {
        if ((flags & low) != 0) return 1;
        if ((flags & med) != 0) return 2;
        if ((flags & high) != 0) return 4;
        return 0;
    }
}
