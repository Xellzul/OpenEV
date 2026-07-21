namespace OpenEV.Override.Ports.Graphics.Model;

// Named packed-0xRRGGBB constants for RGBForeColor call sites and UiColors/BootProgress seed
// values that aren't already backed by a named field. Companion to QuickDrawColor (the indexed
// ForeColor palette) and UiColors (the mutable runtime HUD/dialog colour fields this seeds).
public static class UiColorConstants
{
    // FUN_1005d1a8-adjacent nav-restriction warning — decompile stack RGBColor
    // {0xffff,0x6666,0} (orange). Shared by the galaxy-map system-color resolver, the spaceport
    // radar-dot resolver, and the target-ship info panel's "Forbidden" label.
    public const uint RestrictedNavWarning = 0xff6600;

    // DrawCommodityTradeDialog's selected-row fill — decompile stack RGBColor {0,0x6666,0} (dark
    // green). Numerically equal to HudColorActiveRadar below but a distinct decompile identity
    // (a one-off stack literal, not the HUD radar-colour cell) — kept as its own named constant.
    public const uint CommodityStockFillGreen = 0x006600;

    // Palette.SetHudColorsActive (FUN_1005d358) — Mac 16-bit greens, packed high-byte.
    public const int HudColorActiveRadar    = 0x006600;
    public const int HudColorActiveFriendly = 0x009900;
    public const int HudColorActiveNeutral  = 0x00ff00;

    // Palette.SetHudColorsWhite (FUN_1005d2f4) — cloak-engaged HUD colour override.
    public const int HudColorCloakWhite = 0xffffff;

    // Palette.InitHudColors (FUN_10052a3c) — Mac defaults for the HUD/dialog/galaxy colour
    // globals (16-bit RGBColor -> packed 0xRRGGBB high byte).
    public const int HudColorAuxGreenSeed    = 0x002200;
    public const int HudColorFriendlySeed    = 0x009900;
    public const int HudColorRadarSeed       = 0x006600;
    public const int HudColorNeutralSeed     = 0x00ff00;
    public const int HudColorFrameSeed       = 0x404040;
    public const int HudColorDialogForeSeed  = 0x808080;
    public const int HudColorUnexploredSeed  = 0xc0c0c0;
    public const int HudColorOutfitFrameSeed = 0x000042;
    public const int HudColorChatterTextSeed = 0xffffff;   // white, else chatter is black-on-black

    // AnimateBootProgressBar — the boot progress bar's fill/mid/frame colours (decompile
    // comment: fill {0,0xffff,0}, mid {0,40000,0}, frame {25000,25000,25000} grey).
    public const int BootBarFillColor  = 0x00ff00;
    public const int BootBarMidColor   = 0x009c00;
    public const int BootBarFrameColor = 0x616161;
}
