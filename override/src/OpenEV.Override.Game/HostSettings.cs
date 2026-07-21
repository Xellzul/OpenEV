using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.Json;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Game;

// Host-side settings, read once at startup from settings.json in the portable data
// root (beside the executable; see EvoPaths).
//
// This is Mac-INVISIBLE host substrate + a faithful RESTORATION of original behavior: the
// 1998 game's play area equalled the full monitor resolution (it required ≥ 640×480, forced
// only the 8-bit colour depth, and copied the monitor's gdRect into its window/offscreen rects;
// the HUD was anchored to the screen edges). The port had simplified that to a fixed 800×600.
// Here the user picks the virtual/play-area resolution (any WxH, or "native" = the desktop
// size), windowed-or-fullscreen, and how the play area is scaled to the window/monitor. The
// chosen size is resolved once and held fixed for the session (the original read the monitor
// once), then fed into OverrideGameHost.VirtualWidth/Height → InitMainScreenDevice + TitleMemory.Init,
// from which every downstream layout already derives at runtime.
//
// Lives in OpenEV.Override.Game (not Toolbox): it both feeds OverrideGameHost (Game) and — via
// Program.Main — sets RenderGlobals.HostDebugPanelOverride (Ports), and Toolbox may not
// reference Ports/Game.
internal sealed class HostSettings
{
    public const int MinWidth = 640;      // the original's monitor-tool minimum (FUN_1006f6d4)
    public const int MinHeight = 480;
    public const int DefaultWidth = 800;  // the port's historical fixed size
    public const int DefaultHeight = 600;

    // Fixed play-area size (ignored when NativeResolution is set). Clamped ≥ Min*.
    public int Width = DefaultWidth;
    public int Height = DefaultHeight;
    // "resolution": "native" → resolve to the desktop display size at startup.
    public bool NativeResolution;
    // Borderless fullscreen vs. a resizable window.
    public bool Fullscreen;
    // How the play area is scaled to the window/monitor when the sizes differ.
    public ScalingMode Scaling = ScalingMode.Integer;
    // For ScalingMode.Integer: the exact scale factor the user asked for (2, 2.5, 3, ...). A whole
    // number is pixel-perfect nearest-neighbour; a fractional one is rendered crisply via
    // sharp-bilinear (integer prescale + linear downscale — see OverrideGameHost.PresentScaled).
    // 0 = "auto" = the largest whole-number multiple that fits (the historical default).
    public double FixedScale;
    // The developer target-state debug panel (was the --debug CLI flag) → HostDebugPanelOverride.
    public bool Debug;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,   // the template is hand-commented (// ...)
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,               // tolerate a hand-edited "Fullscreen" etc.
    };

    public static HostSettings Load()
    {
        var s = new HostSettings();
        try
        {
            string path = SettingsPath();
            if (!File.Exists(path))
            {
                WriteDefaultTemplate(path);   // seed a commented template on first run
                return s;
            }
            string text = File.ReadAllText(path);
            HostSettingsDto? dto = JsonSerializer.Deserialize<HostSettingsDto>(text, JsonOptions);
            if (dto is not null)
            {
                if (dto.Resolution is not null) ParseResolution(dto.Resolution, s);
                if (dto.Fullscreen is bool fs) s.Fullscreen = fs;
                if (dto.Scaling is not null) ParseScaling(dto.Scaling, s);
                if (dto.Debug is bool dbg) s.Debug = dbg;
            }
        }
        catch (Exception ex)
        {
            // A malformed/locked file must never stop the game booting — keep whatever parsed
            // (defaults are sane).
            Console.WriteLine($"[settings] load failed, using defaults: {ex.Message}");
        }
        return s;
    }

    private static void ParseResolution(string val, HostSettings s)
    {
        if (val.Equals("native", StringComparison.OrdinalIgnoreCase))
        {
            s.NativeResolution = true;
            return;
        }
        int x = val.IndexOfAny(new[] { 'x', 'X' });
        if (x <= 0) return;   // unparseable → keep default
        if (int.TryParse(val.Substring(0, x).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) &&
            int.TryParse(val.Substring(x + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
        {
            s.NativeResolution = false;
            s.Width = Math.Max(MinWidth, w);
            s.Height = Math.Max(MinHeight, h);
        }
    }

    private static void ParseScaling(string val, HostSettings s)
    {
        switch (val.ToLowerInvariant())
        {
            case "integer": s.Scaling = ScalingMode.Integer; s.FixedScale = 0; return;  // auto
            case "fit": s.Scaling = ScalingMode.Fit; return;
            case "stretch": s.Scaling = ScalingMode.Stretch; return;
        }
        // A bare positive number (optionally "x"-suffixed, e.g. "2", "2x", "2.5", "1.75x") = a fixed
        // scale factor. A whole number is pixel-perfect (nearest); a fractional one renders sharp
        // (integer prescale + linear downscale, see PresentScaled) so it still looks crisp.
        string num = val.TrimEnd('x', 'X').Trim();
        if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor)
            && factor > 0 && !double.IsInfinity(factor))
        {
            s.Scaling = ScalingMode.Integer;
            s.FixedScale = factor;
        }
        // else: unrecognised → keep the current (default) scaling
    }

    // Beside the game prefs/pilots, in the portable data root (see EvoPaths).
    private static string SettingsPath()
    {
        EvoPaths.Ensure(EvoPaths.DataRoot);
        return EvoPaths.SettingsFile;
    }

    private static void WriteDefaultTemplate(string path)
    {
        // Hand-written (not JsonSerializer.Serialize) so it stays self-documenting for a user
        // hand-editing it — real JSON with "//" line comments, tolerated on read via
        // JsonCommentHandling.Skip + AllowTrailingCommas above.
        string[] lines =
        {
            "// EV Override - settings",
            "{",
            "  // --- Resolution (play-area size) ---",
            "  // The play area equals this resolution, exactly like the original game used the",
            "  // full monitor resolution - a larger value shows MORE of the world. Minimum 640x480.",
            "  // Set any \"WIDTHxHEIGHT\" you like, pick a preset, or \"native\" for your desktop size.",
            "  //   640x480    800x600    1024x768    1280x960    1280x1024    1920x1080    native",
            "  \"resolution\": \"800x600\",",
            "",
            "  // --- Fullscreen ---",
            "  // false = resizable window (opens at the resolution above)",
            "  // true  = borderless fullscreen. Use \"resolution\": \"native\" to fill the monitor 1:1,",
            "  //         or a fixed resolution above to run at that size scaled up (see 'scaling').",
            "  \"fullscreen\": false,",
            "",
            "  // --- Scaling (how the play area is scaled to the window/monitor when sizes differ) ---",
            "  // Every mode stays crisp: whole-number scales are pixel-perfect, and any fractional",
            "  // scale is rendered sharp (integer prescale + smooth downscale) - no blur, no shimmer.",
            "  //   \"integer\"     whole-number pixel scaling, largest that fits - crisp [default]",
            "  //   \"2\" (or \"2x\") a specific whole-number factor you choose (x1, x2, x3, ...) - crisp,",
            "  //                 may crop if it is larger than the window",
            "  //   \"2.5\"         any fractional factor you like (2.5, 1.75x, ...) - rendered sharp,",
            "  //                 may crop if it is larger than the window",
            "  //   \"fit\"         scale to fill, preserve aspect ratio (letterbox) - sharp",
            "  //   \"stretch\"     fill the window, ignore aspect ratio - sharp",
            "  \"scaling\": \"integer\",",
            "",
            "  // --- Developer ---",
            "  // Show the developer target-state debug panel (was the --debug command-line flag).",
            "  \"debug\": false",
            "}",
        };
        try { File.WriteAllLines(path, lines); }
        catch (Exception ex) { Console.WriteLine($"[settings] could not write template: {ex.Message}"); }
    }
}

// JSON shape for settings.json. Fields are strings/nullable so a missing key or a value the
// user typo'd (parsed leniently by ParseResolution/ParseScaling below) doesn't abort the whole
// deserialize — only Fullscreen/Debug are real JSON booleans (idiomatic JSON, no INI-style
// yes/on/1 aliases): a non-boolean there is a hard parse error, same as any other malformed
// JSON, and falls back to defaults via HostSettings.Load's try/catch.
internal sealed class HostSettingsDto
{
    public string? Resolution { get; set; }
    public bool? Fullscreen { get; set; }
    public string? Scaling { get; set; }
    public bool? Debug { get; set; }
}

// How the virtual play-area buffer is scaled onto the window / fullscreen monitor when the
// output size differs from the play-area size. Host presentation only (Mac-invisible).
internal enum ScalingMode
{
    Integer,   // a fixed scale — HostSettings.FixedScale (0 = largest whole multiple that fits);
               // whole factors are pixel-perfect, fractional ones render sharp (see PresentScaled)
    Fit,       // scale to fill, preserve aspect ratio (letterbox)
    Stretch,   // fill the output, ignore aspect ratio
}
