namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.Toolbox;

// Port of FUN_1005d3c4 (EV Override-11.c 38665-38699) — engage the cloaking device: play the
// cloak sound, find the player's cloak outfit (ModType 17) to pick its screen-tint palette
// preset, whiten the HUD colours, install the preset screen palette (the whole-display cloak
// tint), drop the player's shield, mark the HUD layers dirty, and set the IsCloaked flag.
// No-op when already cloaked.
public static class EngageCloaking
{
    public static void Run()
    {
        if (WorldState.IsCloaked)
            return;

        SndPlay.Run(SoundResourceCells.CloakEngageSnd, 8, 128, 128);

        short presetIdx = -1;
        for (short outfitIndex = 0; outfitIndex < OutfitTable.Count; outfitIndex++)
        {
            var outfit = OutfitTable.Outfits[outfitIndex];
            for (short slotIndex = 0; slotIndex < OutfitRecord.ModBankCount; slotIndex++)
            {
                if (outfit.ModType[slotIndex] == OutfitModType.CloakingDevice &&
                    OwnedOutfitGrid.Store[outfitIndex] > 0)
                {
                    presetIdx = outfit.ModValue[slotIndex];
                }
            }
        }

        if (presetIdx != -1)
        {
            Palette.SetHudColorsWhite();
            // Cell 0x10081208 is the palette preset array (Palette.PresetCTables); the raw
            // decompile pointer-indexed it, so route through the managed array with a bounds-check.
            if ((uint)presetIdx < (uint)Palette.PresetCTables.Length)
            {
                Palette.InstallScreenPalette(Palette.PresetCTables[presetIdx], 1);
                // Host bridge: the preset table is the boot-time RemapToHSL(hue) clone; the
                // host applies the same remap to the composited frame (the visible cloak
                // tint — see MacToolbox.ScreenPaletteRemap). Preset 0 has no hue: the
                // original installs an uninitialized handle there (garbage on a real Mac).
                if (Palette.PresetHue(presetIdx, out short hueR, out short hueG, out short hueB))
                    MacToolbox.ScreenPaletteRemap(hueR, hueG, hueB);
            }
        }

        WorldState.IsCloaked = true;
        var player = ShipTable.Player;
        if (player.Shield > 0)
            player.Shield = 0f;

        WorldState.WeaponSlotDirty = 1;
        WorldState.RadarRedrawDirty = 1;
        WorldState.ShieldEnergyBarDirty = 1;
        WorldState.HudWeaponPanelDirty = 1;
        WorldState.HudStatusPanelDirty = 1;
        WorldState.PlayerShieldBarDirty = 1;
        WorldState.SpawnPulseDirty = 1;
    }
}
