namespace OpenEV.Override.Ports.Outfit;

// Named slots for the commodity / outfit pricing math globals. Two kinds:
//  (1) PEF data-seg DOUBLE constants the trade-price and resale-value formulas
//      compare against and scale by.
//  (2) DIRECT &DAT short[] commodity tables, indexed `Base + commodityIndex*2`
//      (NOT pointer slots — the address IS the array base).
public static class CommodityPricing
{
    // (1) price/value curve DOUBLE constants.
    public const double PriceCurveDivisor = 15.0;   // 0x10081c30
    public const double PriceCurveBarScale = 100.0;  // 0x10081c38
    public const double PriceTotalSlope = 0.1;    // 0x10081c40
    public const double PriceSlopeBuy = 0.5;    // 0x10081c48
    public const double PriceOuterScale = 1000.0; // 0x10081c50
    public const double PriceSlopeSell = 0.025;  // 0x10081c58
    public const double PriceLinearThreshold = 2.0;    // 0x10081c70
    public const double ValueBarScale = 0.1;    // 0x10082040
    public const double ResaleValueScale = 0.95;   // 0x10082268

    // (2) the per-commodity short tables. 0x1008f736 price-mode[6];
    // 0x1008f742 base-price[10] (6 seeded from STR# 0x2454 / fallback STR#
    // 0xfa4 at world reset; the extra 4 slots are the original f742..f756
    // gap); 0x1008f756 final-price[8] (clamped >= 5) — the player-info tab
    // index runs 0..7, and the last two entries (FinalPrice[6]/[7]) are the
    // per-spob JUNK sell/buy prices, not a 7th/8th commodity.
    public static readonly short[] PriceMode = new short[6];
    public static readonly short[] BasePrice = new short[10];
    public static readonly short[] FinalPrice = new short[8];
}
