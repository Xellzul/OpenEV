using System;
using System.Collections.Generic;
using System.IO;
using OpenEV.Platform.Imaging;

namespace OpenEV.Override.Game;

// SoftwareFont-backed Times loader for the game. Classic Mac Times is font family
// ID 20 (the credits roll calls TextFont(0x14) — FUN_10041ba0). EVO does NOT
// bundle Times, so ID 20 resolves to the system Times / a serif substitute.
// If none is present, Init leaves System null and MacToolbox.ResolveFont falls
// back to the bundled face.
internal static class TimesFont
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
            var candidates = new List<string>();
            string winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrEmpty(winFonts))
                candidates.Add(Path.Combine(winFonts, "times.ttf"));   // Times New Roman (Regular)
            // macOS serif equivalents.
            candidates.Add("/System/Library/Fonts/Supplemental/Times New Roman.ttf");
            candidates.Add("/Library/Fonts/Times New Roman.ttf");
            candidates.Add("/System/Library/Fonts/Times.ttc");
            // Linux serif equivalents.
            candidates.Add("/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf");
            candidates.Add("/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf");

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                // MacPointSizes: the Mac scaler renders point size as pixels-per-em, so
                // Times 24 (the credits/About roll) has a 24px em — FSS's default
                // hhea-height scaling drew it ~11% smaller than the SheepShaver capture
                // (~21.7px em). No Times bitmap strikes exist, so this face always
                // renders through the outline path this flag corrects.
                _system = new SoftwareFont(File.ReadAllBytes(path)) { MacPointSizes = true };
                Source = path;
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TimesFont init failed: {ex.Message}");
            _system = null;
        }
    }
}
