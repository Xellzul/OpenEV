using System;
using System.IO;
using OpenEV.Platform.Imaging;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Game;

// SoftwareFont-backed loader for the default UI font = classic Mac Geneva
// (font family ID 3), which the title menu, pilot-info panel, and most UI
// text select via TextFont(3). Fixing the font here fixes every TextFont(3)
// consumer — they all route through MacToolbox.Font + ResolveFont.
//
// EVO does NOT bundle Geneva — it's a Mac OS *system* font, absent from the
// game's resource fork. The shared SoftwareFontLoader sources it from FREE fonts
// only (a user geneva.ttf → bundled Grand9K Pixel outline → local Geneva-N.bdf
// strikes → bundled Grand9K 9px strike); no proprietary Apple face ships here.
internal static class GenevaFont
{
    private static SoftwareFont? _system;
    private static bool _initialized;

    public static bool Available => _system is not null;
    public static SoftwareFont? System => _system;
    public static string Source { get; private set; } = "none";

    private static readonly SoftwareFontLoader.Spec Face = new(
        UserOutlineFile: "geneva.ttf", UserOutlineLabel: "geneva.ttf (local Apple Geneva outline)",
        EmbeddedOutline: "Grand9K-Pixel.ttf", EmbeddedOutlineLabel: "Grand9K Pixel (bundled free outline)",
        LocalStrikeGlob: "Geneva-*.bdf", EmbeddedStrike: "Grand9K-9.bdf", EmbeddedStrikeSize: 9);

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        string fontsDir = EvoPaths.Fonts;
        (_system, Source) = SoftwareFontLoader.Load(Face, fontsDir, typeof(GenevaFont).Assembly, "GenevaFont");
    }
}
