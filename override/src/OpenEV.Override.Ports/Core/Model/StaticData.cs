using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Core.Model;

// Strongly-typed facade over the game's static string data — the typed front end
// the ports should read instead of raw addresses / GetIndString juggling.
//
// Two backing sources:
//   • Data-segment CONSTANT strings (labels, prompts) — inlined here as C# const
//     literals dumped from the PEF data segment (addr comments = the dump source).
//   • STR# / 'STR ' RESOURCE tables (ship long names, …) — loaded from the
//     resource fork; materialised here into typed C# arrays, lazily on first use.
//
// Each member documents its origin (the decompile address / resource id) so the
// magic numbers live in exactly one place. Widen this as more strings migrate.
public static class StaticData
{
    // ── Data-segment constant strings ────────────────────────────────────────
    // C-strings the original copied/concatenated via FUN_1007615c (strcpy) /
    // FUN_100761bc (strcat) — dumped from the
    // PEF data segment to literals (addr comments = the dump source).
    public const string ChristenPrefix = "Now, please christen your brand-new ";  // @0x10084517 (GameToc-0x4149, dumped)
    public const string Colon = ": ";  // @0x100820ae (GameToc-0x65b2, dumped) — colon + SPACE (the old ":" doc comment missed the space)
    public const string MonitorToolOpenError = "Sorry, an error occured while trying to open the Monitor Tool.";  // @0x1008492f (GameToc-0x3d31, dumped; "occured" sic)
    public const string IdLabel = "ID = ";  // @0x1008496e (GameToc-0x3cf2, dumped)

    // ── Long ship names, indexed by ship class ───────────────────────────────
    // The descriptive "…shipyards S-685 shuttlecraft" name shown in the christen
    // prompt + shipyard (the decompile's *(toc-0x7824) table). NOT a data-segment
    // constant — it's a resource: a per-ship 'STR ' (id 3700+class) when present,
    // else STR# 0x138a entry (class+1), exactly as FUN_10019880 fills the table.
    // Lazy because it needs the resource fork open.
    private const int ShipClassCount = 0x40;
    private static string[]? _shipLongNames;
    public static string[] ShipLongNames
    {
        get
        {
            if (_shipLongNames is null) LoadShipLongNames();
            return _shipLongNames!;
        }
    }

    private static void LoadShipLongNames()
    {
        var names = new string[ShipClassCount];
        for (int cls = 0; cls < names.Length; cls++)
        {
            // Per-ship 'STR ' resource (3700 + class) wins; STR# 0x138a is the fallback.
            names[cls] = Resource.TryLoadStr.RunString((short)(cls + 0xe74))
                         ?? MacToolbox.GetIndString(0x138a, (short)(cls + 1));
        }
        _shipLongNames = names;
    }

    // The eight fatal-alert / error UI strings (STR# 25000 entries 1-8), the managed form of the
    // Mac's eight 256-byte data-seg cells &DAT_1008554c..&DAT_10085c4c. Seeded with the binary's
    // compiled-in defaults; InitGalaxyMapWindow overwrites all eight from STR# 25000 at game-window
    // setup (CopyEightPascalStringBlocks), so a plug-in's STR# 25000 override reaches the alert
    // sites exactly as in the original. Read by index at five fatal/error sites (the *Index consts
    // below); cells 0-2 ("OK"/"Yes"/"No") have no reader in the shipping binary.
    public const int FatalAlertButtonIndex = 3;      // OOM/fatal single-button title ("Quit")
    public const int OutOfMemoryMessageIndex = 4;
    public const int NoScreenErrorIndex = 5;         // DecodePictResource: "Internal error. (No screen?)"
    public const int BackdropLoadFailedIndex = 6;
    public const int NoWindowErrorIndex = 7;         // SetScrollViewPosition: "Internal error. (No window?)"

    public static string[] UiErrorStrings { get; set; } =
    {
        "OK",
        "Yes",
        "No",
        "Quit",
        "Out of memory! Try increasing the memory in 'Get info'.",
        "Internal error. (No screen?)",
        "Couldn't load the backdrop PICT!",
        "Internal error. (No window?)",
    };
}
