using System;
using System.IO;
using System.Linq;
using OpenEV.Platform.Imaging;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Game;

// FontStashSharp-backed loader for the game's one custom face: "Sillycon"
// (Mac FOND 2020 / sfnt 9295, font family ID 2020 = TextFont 0x7e4).
// OverrideDataLoader (src/) extracts it to <exe dir>/Fonts/Geneva_9295.ttf
// — that filename is a historical misnomer (the file is internally "Sillycon Plain",
// not Geneva). This loader owns it for its legitimate ID-0x7e4 callsites; Geneva
// (ID 3) is handled separately by GenevaFont.
internal static class SillyconFont
{
    private static SoftwareFont? _system;
    private static bool _initialized;

    public static bool Available => _system is not null;
    public static SoftwareFont? System => _system;
    public static string Source { get; private set; } = "none";

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            string fontDir = EvoPaths.Fonts;
            if (!Directory.Exists(fontDir)) return;
            // The extracted sfnt 9295 (still named "Geneva_9295.ttf"). Match the id
            // EXACTLY — a broad "Geneva*.ttf" glob is case-insensitive on Windows and
            // also caught a user-supplied geneva.ttf (the real Apple Geneva outline
            // GenevaFont prefers), which sorted first and silently replaced the
            // whole in-game HUD face with Geneva.
            string exact = Path.Combine(fontDir, "Geneva_9295.ttf");
            string? ttfPath = File.Exists(exact) ? exact
                : Directory.GetFiles(fontDir, "*_9295.ttf").FirstOrDefault();
            if (ttfPath is null) return;
            // Mac OS rasterized this outline unsmoothed (crisp 1-bit HUD text); match it.
            byte[] sfnt = File.ReadAllBytes(ttfPath);
            _system = new SoftwareFont(sfnt) { Monochrome = true };

            // 14px 1-bit strike — the ONLY size the game draws this face at (every TextFont(0x7e4)
            // site pairs with TextSize(14)). The classic Mac scaler ran the sfnt's hinting programs,
            // grid-fitting stems to crisp 1px; stb has no hinting interpreter and rasterizes ~19%
            // heavier, so a hinted strike is required.
            //
            // APPROVED DEVIATION (2026-07-19, user): rasterize the strike at RUNTIME from the user's
            // own extracted sfnt by running its hinting bytecode through FreeType, instead of shipping
            // an offline-baked bitmap. FreeType's grid-fit matches the Mac (SheepShaver) on 82/96
            // glyphs at 14ppem; the other 14 (punctuation + K/k/v, incl. the comma) drift ~1px, so
            // this is observably ~15% less faithful than the retired Sillycon-14.bdf — the user
            // accepted that in exchange for shipping no game-font-derived asset. Advances come from
            // the sfnt's own hdmx, so StringWidth is unaffected. Falls back to the stb Monochrome
            // outline above if the native FreeType is ever unavailable (no platform loses Sillycon).
            var strike = FreeTypeStrikeRasterizer.TryRasterize(sfnt, 14, ascent: 11, descent: 3);
            if (strike is not null) { _system.AddStrike(strike); Source = "FreeType 14px (runtime bytecode-hinted)"; }
            else { Source = "stb Monochrome outline (FreeType unavailable)"; Console.WriteLine("SillyconFont: FreeType unavailable, using stb outline fallback"); }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SillyconFont init failed: {ex.Message}");
            _system = null;
        }
    }
}
