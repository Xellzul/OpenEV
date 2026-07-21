using System;
using System.IO;
using OpenEV.Platform.Imaging;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Game;

// SoftwareFont-backed loader for the classic Mac SYSTEM font = Chicago (font
// family ID 0). The Dialog Manager draws every no-filter dialog in it —
// statText bodies, button/checkbox titles, dialog TextEdit fields, all at
// Chicago 12 — and any DrawString/TETextBox with TextFont(0) active selects it
// via MacToolbox.ResolveFont.
//
// Like Geneva, Chicago is a Mac OS system font absent from the game's resource
// fork; the shared SoftwareFontLoader sources it from FREE fonts only (a user
// chicago.ttf → bundled public-domain ChicagoFLF outline → local Chicago-N.bdf
// strikes → bundled ChicagoFLF-12 1-bit strike, which blits hard pixels like a
// real Mac screen font where the AA outline renders fuzzy at 12px). If nothing
// loads, System stays null and MacToolbox falls back to the Geneva + faux-bold
// approximation.
internal static class SystemFont
{
    private static SoftwareFont? _system;
    private static bool _initialized;

    public static bool Available => _system is not null;
    public static SoftwareFont? System => _system;
    public static string Source { get; private set; } = "none";

    private static readonly SoftwareFontLoader.Spec Face = new(
        UserOutlineFile: "chicago.ttf", UserOutlineLabel: "chicago.ttf (local Apple Chicago outline)",
        EmbeddedOutline: "ChicagoFLF.ttf", EmbeddedOutlineLabel: "ChicagoFLF (bundled free outline)",
        LocalStrikeGlob: "Chicago-*.bdf", EmbeddedStrike: "ChicagoFLF-12.bdf", EmbeddedStrikeSize: 12);

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        string fontsDir = EvoPaths.Fonts;
        (_system, Source) = SoftwareFontLoader.Load(Face, fontsDir, typeof(SystemFont).Assembly, "SystemFont");
    }
}
