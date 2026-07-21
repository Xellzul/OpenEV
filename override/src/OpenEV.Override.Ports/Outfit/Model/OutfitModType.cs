namespace OpenEV.Override.Ports.Outfit.Model;

// Outfit modifier type codes (outfit record +0x04 / +0x06 per bank).
public enum OutfitModType : short
{
    Invalid = -1,
    Weapon = 1,
    Cargo = 2,
    Ammo = 3,
    Shield = 4,
    ShieldRecharge = 5,
    Armor = 6,
    Acceleration = 7,
    Speed = 8,
    Maneuver = 9,
    Jammer = 10, // ECM jammer (ModValue bits 1/2/4/8 = jam type 0..3 level 1; 0x10/0x20/0x40/0x80 = level 2)
    EscapePod = 11,
    Fuel = 12,
    DensityScanner = 13, // FUN_1005aeb4 -> ShipDerivedStats.HasDensityScanner; drives DrawRadarHud's wideBlips (box blips for stations/ships mass >= 100)
    IffRadar = 14, // FUN_1005af64 -> ShipDerivedStats.HasIffRadar; drives DrawRadarHud's colorRadar (govt/hostility-tinted blips instead of plain friendly color)
    Afterburner = 15,
    Map = 16, // marks systems visited (FloodVisitedSysts from the current system, ModValue = reach); consumed on purchase (AdvanceLoadout)
    CloakingDevice = 17, // owning one lets the player toggle the cloak (TickShipAI -> EngageCloaking); ModValue = the cloak screen-tint palette preset (Palette.PresetHue: 1 red .. 6 yellow; stock oütf 145 "UE Cloaking Device" uses 2 green)
    FuelScoop = 18, // ModValue = sign-magnitude rng period (1-in-|ModValue| chance/tick to add, or drain if negative, 1 fuel); TickShipAI still compares this ModType as a raw literal (18)
    AutoRefuel = 19, // passive fuel top-up paid from credits (TickPassiveOutfitTopup)
    AutoEject = 20,
    StatusClear = 21, // clears negative per-system status for ModValue's govt (-1 = every govt); consumed on purchase (AdvanceLoadout)
    HyperJumpDays = 22, // grants ModValue extra days per hyperjump (EffectiveHyperJumpDays); drives the per-jump-arrival TickWorldDailyEvents loop
    HyperRange = 23,
    InterferenceReduction = 24,
    Marines = 25, // ModValue x owned adds to the capture-odds crew pool (InitTradeSession, via OutfitTable.SumOutfitModValue)
    ControlBit = 26, // sets ControlBits[ModValue]; ModValue >= 1000 CLEARS bit ModValue-1000 (AdvanceLoadout)
}
