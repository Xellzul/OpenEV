using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Core.Model;

// Named managed homes for the few gameplay world-state scalars that stayed
// here (formerly raw EvoMemory cells). The HUD dirty-flag cluster
// (0x1008f3b0..b6), GameFrameTickCounter (f72c), InstallDays (f730,
// ex-"ReinforcementTier"), TradeCurrentTab (f734) and the rest of the
// f3bx-cx flag block migrated to Core.Model.WorldState fields.
public static class WorldFlags
{
    // (B6) The prefs/detail scalars that lived here (GfxDetailFlagSlot
    // 0x1008a552, ProjectileStreaksDisabledSlot 0x1008a555, MasterVolumeSlot
    // 0x1008a556) migrated with the rest of the prefs band to Core.Model.GamePrefs
    // (.GfxDetailFlag / .ProjectileStreaksDisabled / .MasterVolume).
    // byte: "streaks active this frame" gate, recomputed by RunMainGameLoop from the
    // streaks pref + speed; read by TickProjectile/TickStreakSprite. Was the raw
    // byte DAT_1008f5cd.
    public static byte StreaksActiveFlag;

    // Screen/camera centre (signed shorts), seeded by Graphics.Model.GWorldPort.ShowGameWindow
    // (centre = 0.5 * (playWidth - 0x90 status panel) etc.) and the V2TitleAdapter
    // enter-ship fallback. Were the adjacent SHORT cells _DAT_100901fe / sRam10090200
    // (GameToc+0x7b9e / +0x7ba0).
    public static short CameraCentreX;
    public static short CameraCentreY;
}
