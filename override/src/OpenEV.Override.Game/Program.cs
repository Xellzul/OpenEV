using System;
using System.IO;
using System.Linq;

namespace OpenEV.Override.Game;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Host settings file (settings.json, beside the executable): virtual/play-area
        // resolution, fullscreen, scaling, and the debug flag. Read once here; the resolution
        // is resolved and applied inside OverrideGameHost.Run(). Auto-seeds a commented template
        // on first run.

        // Push the portable data root into Platform.EvoData, which cannot reference
        // Platform.Toolbox (where EvoPaths lives) to resolve it itself.
        OpenEV.Platform.EvoData.OverrideDataLoader.SfntExtractDir =
            OpenEV.Platform.Toolbox.EvoPaths.Fonts;

        HostSettings settings = HostSettings.Load();

        // debug / --target-debug-panel: APPROVED DEVIATION host affordance — force the developer
        // target-state readout on. That panel is DEAD/unreachable in the shipping original (its
        // enable is a permanently-0 uninitialised BSS byte; see DEV_DEBUG_CODE.md). Mac-invisible:
        // decided here in the host, never observed inside the ported program. Sourced from the
        // settings file's `debug =`, with the legacy CLI flag kept as an override.
        if (settings.Debug || args.Any(a => a is "--debug" or "--target-debug-panel"))
            OpenEV.Override.Ports.Graphics.Model.RenderGlobals.HostDebugPanelOverride = true;

        string? gameDir = ResolveGameDir();

        using var game = new OverrideGameHost(gameDir, settings);
        game.Run();
    }

    private static string? ResolveGameDir()
    {
        // The one and only location: "EV Override" alongside the binary. Host plumbing — the Mac
        // app used the Resource Manager; the build stages this folder into the output directory
        // (see OpenEV.Override.Game.csproj). null when it is absent (the game logs "(not found)").
        string dir = Path.Combine(AppContext.BaseDirectory, "EV Override");
        return Directory.Exists(dir) ? dir : null;
    }
}
