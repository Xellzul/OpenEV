using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_1005d5c8 (EV Override-11.c 38728-38750) — re-apply the cloak screen palette (session load
// while cloaked): scan the 128 outfits for the first one that has a CloakingDevice mod bank and
// that the player owns (count > 0), install that outfit's palette preset, and stop.
public static class ReapplyCloakPalette
{
    public static void Run()
    {
        for (short outfitIndex = 0; outfitIndex < OutfitTable.Count; outfitIndex++)
        {
            var outfit = OutfitTable.Outfits[outfitIndex];
            for (short bank = 0; bank < OutfitRecord.ModBankCount; bank++)
            {
                if (outfit.ModType[bank] != OutfitModType.CloakingDevice || OwnedOutfitGrid.Store[outfitIndex] <= 0)
                    continue;
                short presetIndex = outfit.ModValue[bank];
                if ((uint)presetIndex < (uint)Palette.PresetCTables.Length)
                {
                    Palette.InstallScreenPalette(Palette.PresetCTables[presetIndex], 1);
                    // Host bridge — same pairing as EngageCloaking's preset install.
                    if (Palette.PresetHue(presetIndex, out short hueR, out short hueG, out short hueB))
                        MacToolbox.ScreenPaletteRemap(hueR, hueG, hueB);
                }
                return;
            }
        }
    }
}
