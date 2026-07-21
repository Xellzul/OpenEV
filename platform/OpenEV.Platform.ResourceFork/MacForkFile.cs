namespace OpenEV.Platform.ResourceFork;

/// <summary>
/// Helpers for reading and writing classic Mac resource forks stored in the SheepShaver
/// Unix filesystem layout: the resource fork lives in a <c>.rsrc/&lt;name&gt;</c> sidecar;
/// Finder info lives in <c>.finf/&lt;name&gt;</c>; the data fork is an empty file at
/// the original path. Fallback read from the bare file handles raw .rsrc sidecar files
/// and the older "fork in data fork" layout.
/// </summary>
public static class MacForkFile
{
    /// <summary>
    /// Finder info bytes that mark a file as an EV Override plug-in (type 'Opïf', creator
    /// 'EsçO' + 24 reserved zeroes). ï=0x95, ç=0x8d in Mac Roman (copied from a stock plug-in).
    /// </summary>
    public static readonly byte[] EvoPluginFinderInfo =
    [
        0x4f, 0x70, 0x95, 0x66,   // type    'Opïf'
        0x45, 0x73, 0x8d, 0x4f,   // creator 'EsçO'
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
    ];

    /// <summary>
    /// Read a Mac resource fork from <paramref name="path"/>.  Looks for the SheepShaver
    /// sidecar (<c>.rsrc/&lt;name&gt;</c>) first; falls back to reading the file itself
    /// (the older "fork in the data fork" layout, or a raw .rsrc sidecar opened directly).
    /// </summary>
    public static byte[] ReadFork(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        string name = Path.GetFileName(path);
        if (dir is not null)
        {
            string sidecar = Path.Combine(dir, ".rsrc", name);
            if (File.Exists(sidecar))
            {
                byte[] s = File.ReadAllBytes(sidecar);
                if (s.Length > 0) return s;
            }
        }
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Write a Mac resource fork to the SheepShaver/Unix-filesystem layout:
    /// <list type="bullet">
    /// <item>resource fork → <c>.rsrc/&lt;name&gt;</c></item>
    /// <item>Finder info → <c>.finf/&lt;name&gt;</c> (written once; not overwritten if already present)</item>
    /// <item>empty data fork at <paramref name="path"/> itself</item>
    /// </list>
    /// This is what makes editor-saved plug-ins actually load in the game (SheepShaver's
    /// filesystem layer never looks for a resource fork in the data fork).
    /// </summary>
    /// <param name="path">Destination path for the (empty) data fork / plug-in filename.</param>
    /// <param name="forkBytes">The serialized resource fork bytes.</param>
    /// <param name="finderInfo">Finder info bytes (32 bytes). Defaults to
    /// <see cref="EvoPluginFinderInfo"/> when null.</param>
    public static void WriteFork(string path, byte[] forkBytes, byte[]? finderInfo = null)
    {
        string dir = Path.GetDirectoryName(path) ?? ".";
        string name = Path.GetFileName(path);

        string rsrcDir = Path.Combine(dir, ".rsrc");
        Directory.CreateDirectory(rsrcDir);
        File.WriteAllBytes(Path.Combine(rsrcDir, name), forkBytes);

        string finfDir = Path.Combine(dir, ".finf");
        Directory.CreateDirectory(finfDir);
        string finfPath = Path.Combine(finfDir, name);
        if (!File.Exists(finfPath) || new FileInfo(finfPath).Length == 0)
            File.WriteAllBytes(finfPath, finderInfo ?? EvoPluginFinderInfo);

        File.WriteAllBytes(path, []);   // data fork is empty for a plug-in
    }
}
