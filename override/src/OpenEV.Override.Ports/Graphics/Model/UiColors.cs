namespace OpenEV.Override.Ports.Graphics.Model;

// Managed home for the HUD / dialog / galaxy UI colour globals.
//
// In this port a "colour" is a PACKED 0xRRGGBB int: MacToolbox.RGBForeColor
// was reimplemented to take a packed value, and every reader does
// RGBForeColor((uint)colour). The Mac 16-bit-per-channel RGBColor model was
// dropped. These used to be raw EvoMemory BSS cells shared across Dialog /
// Galaxy / HUD and read at ~30 sites; they are now managed fields, the raw
// cells (listed below) kept only as address documentation.
//
// Default 0 (black) — matches the un-seeded BSS. TitleMemory seeds Friendly /
// Neutral at the title screen; SetHudColors*/InitHudColors (in Palette) set the
// rest. The Mac decompile wrote these as 16-bit RGBColor records; the seed
// constants below are the packed (high-byte) equivalents.
public static class UiColors
{
    public static int DialogFore;   // 0x10080bdc — UI foreground (dialog text / frames)
    public static int Unexplored;   // 0x10080d00 — galaxy unexplored / hostile / accent
    public static int Neutral;      // 0x10080d2c — armour / neutral / spaceport system
    public static int Frame;        // 0x10080d30 — galaxy / radar frame
    public static int Friendly;     // 0x10080f90 — shield / friendly
    public static int Radar;        // 0x1008119c — radar accent; read by DrawShieldEnergyBar's energy fill
    public static int OutfitFrame;  // 0x10081000 — outfit / commodity dialog frame
    public static int AuxGreen;     // 0x100811a0 — Mac seed 0x002200; read by RefreshStatusPanel
    // In-flight chatter/news text colour. The decompile passed PTR_DAT_10080b84
    // (-> RGBColor record at 0x100df524) as EnqueueChatterEvent's 5th arg and
    // RGBForeColor deref'd it; the port's packed-value model collapses that to this
    // field. 0 (black) until the HUD palette port seeds it.
    public static int ChatterText;  // *0x10080b84 -> 0x100df524
}
