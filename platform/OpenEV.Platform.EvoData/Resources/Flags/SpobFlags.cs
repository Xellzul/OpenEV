namespace OpenEV.Platform.EvoData.Resources.Flags;

[System.Flags]
public enum SpobFlags : uint
{
    None = 0,
    Landable = 0x00000001,
    Exchange = 0x00000002,
    Outfitter = 0x00000004,
    Shipyard = 0x00000008,
    Station = 0x00000010,
    Uninhabited = 0x00000020,
    Bar = 0x00000040,

    PriceLowEquipment  = 0x00000100,
    PriceMedEquipment  = 0x00000200,
    PriceHighEquipment = 0x00000400,

    PriceLowMetal      = 0x00001000,
    PriceMedMetal      = 0x00002000,
    PriceHighMetal     = 0x00004000,

    PriceLowLuxury     = 0x00010000,
    PriceMedLuxury     = 0x00020000,
    PriceHighLuxury    = 0x00040000,

    PriceLowMedical    = 0x00100000,
    PriceMedMedical    = 0x00200000,
    PriceHighMedical   = 0x00400000,

    PriceLowIndustrial  = 0x01000000,
    PriceMedIndustrial  = 0x02000000,
    PriceHighIndustrial = 0x04000000,

    PriceLowFood       = 0x10000000,
    PriceMedFood       = 0x20000000,
    PriceHighFood      = 0x40000000,
}
