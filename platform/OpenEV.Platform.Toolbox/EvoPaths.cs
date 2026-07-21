using System;
using System.IO;

namespace OpenEV.Platform.Toolbox;

public static class EvoPaths
{
    /// Resolved once: the environment is fixed for the life of the process
    /// AppContext.BaseDirectory is fixed for the life of the process.
    public static string DataRoot { get; } =
        AppContext.BaseDirectory;

    /// The original keeps pilots in a "Pilots" subfolder of the Preferences
    /// folder (FUN_1001e940 creates it from binary string toc-0x58fe); the prefs
    /// file itself stays at the root.
    public static string Pilots => Path.Combine(DataRoot, "Pilots");

    /// Where sfnt resources extracted from the game data are written, and where
    /// the host font loaders look for a user-supplied override.
    public static string Fonts => Path.Combine(DataRoot, "Fonts");

    /// Host settings file (resolution, fullscreen, scaling, debug flag).
    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    /// Create <paramref name="path"/> if absent and hand it back, so callers can
    /// keep the one-liner shape the old GetFolderPath + CreateDirectory had.
    public static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
