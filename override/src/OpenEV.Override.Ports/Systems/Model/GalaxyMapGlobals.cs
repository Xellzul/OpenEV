namespace OpenEV.Override.Ports.Systems.Model;

// Semantic accessors for the galaxy-map / navigation / system-connectivity
// runtime globals that the Galaxy logic functions reach by raw address. Each
// is documented with its decompile symbol and whether it is a POINTER slot
// (holds a heap base; deref with ReadInt first) or a DIRECT value cell.
//
// Naming these doubles as a bug-finder: several early ports dropped the
// pointer indirection on the flood-fill slots (read `0xADDR + idx` instead of
// `*(_DAT_ADDR) + idx`); routing through the typed Base getter restores the
// decompile's dereference.
public static class GalaxyMapGlobals
{
    // ── system-connectivity flood-fill state (FUN_1005cdc8 / FUN_1005c654) ──

    // PTR slot → base of the per-system "visited" byte[] flag array.
    // Decompile: `iVar1 = _DAT_10081214; *(char *)(_DAT_10081214 + sysIdx)`.
    // An earlier port dropped the deref in MarkConnectedSystemsRecursive /
    // PropagateSystemKillImpact (read `0x10081214 + idx` directly = bug).
    public const int VisitedSystemFlagsSlot = 0x10081214;
    // Managed: the flood-fill visited byte per system (was the BSS byte[1000]
    // behind the relocated ptr cell).
    public static readonly byte[] VisitedSystemFlags = new byte[1000];

    // PTR slot → the flood-fill depth/limit short cell.
    // Decompile derefs: `*_DAT_10081210 = depth` / `depth <= *_DAT_10081210`.
    // An earlier port read/wrote 0x10081210 as the cell itself (dropped deref = bug).
    public const int FloodDepthCursorSlot = 0x10081210;
    public static short FloodDepthCursor;   // managed (was the BSS short behind the ptr cell)

    // ── system status / nav-history pointers (already deref'd correctly) ──

    // MANAGED: the player's per-system legal-status/"coolness" table — formerly
    // the heap short[1000] behind PTR_DAT_10080b54 (`*(short *)(ptr + systIdx*2)`).
    // Pilot-SAVED (PilotRec.KillsBySyst region); ptr slot + BSS target retired
    // (OriginalGameStateTotalBytes). All readers/writers go through
    // SystemStatus/SetSystemStatus below.
    public const int SystemStatusTableSlot = 0x10080b54;   // ptr slot (doc only)
    public static readonly short[] SystemStatusStore = new short[1000];

    // The plotted nav/autopilot route list — 32 shorts of syst ids, -1 = empty;
    // [0] = the route origin (player's system), [1..] = the plotted chain that
    // EngageAutopilotToHistoryTarget walks. Formerly the heap buffer behind
    // PTR_DAT_10080ed0 (slot + BSS target 0x100e01b2 now retired).
    public const int NavHistoryLength = 32;
    public static readonly short[] NavHistory = new short[NavHistoryLength];

    // ── galaxy-map zoom / color ──
    // (the map view-centre short pair 0x100901fa/fc lives in WorldFlags
    //  alongside the rest of the 0x100901xx camera-centre cluster.)

    // PTR slot → the galaxy-map zoom/scale DOUBLE (`*_DAT_10080fc8` compared
    // against the zoom clamp). Read/written through the pointer with
    // ReadDouble/WriteDouble (RunGalaxyMapDialog's zoom buttons multiply it in place).
    public const int ZoomScalePtrSlot = 0x10080fc8;

    // PEF data-seg DOUBLE zoom constants for the galaxy-map dialog (RunGalaxyMapDialog) —
    // values dumped from the PEF (tools/dump_dataseg.py); the former raw slots:
    //   0x10081f90 (toc-0x66d0) reset threshold: entry zoom == this → reset to 1.0 (toc-0x66d8)
    //   0x10081f80 (toc-0x66e0) '+' per-click multiplier (DITL item 4)
    //   0x10081f78 (toc-0x66e8) '-' per-click multiplier (DITL item 5)
    public const double ZoomResetThreshold = 0.0;       // dumped 00 00 00 00 00 00 00 00
    public const double ZoomInFactor = 1.333333;  // dumped 3f f5 55 54 fb da d7 52
    public const double ZoomOutFactor = 0.75;      // dumped 3f e8 00 00 00 00 00 00
    //   0x10081f48 (toc-0x6718) _DAT_10081f48: '+' disabled at/above this zoom
    public const double ZoomMaxThreshold = 2.0;       // dumped 40 00 00 00 00 00 00 00

    // The 16-short route list the map dialog builds from the govt tables
    // (mission/strand destination systems; -1 terminated). _DAT_10080fac.
    public const int RouteListArraySlot = 0x10080fac;

    // ProcPtr for the galaxy-map ModalDialog filter. Formerly read from the cell
    // _DAT_10080fcc; its relocated value (raw 00 00 1e a0, BySectD + dataBase 0x10080660)
    // is the TVector at 0x10082500 = {FUN_10034420 (GalaxyMapModalFilter), toc} - a C#
    // literal now, the cell is no longer read.
    public const int MapModalFilterProc = 0x10082500;

    // PTR cell → byte flag read by DrawGalaxyMap's route-icon logic (toc-0x76d4):
    // when the DialogResultSlot system matches, a zero flag suppresses its
    // mission-destination icon in favour of the route icon. Semantics unconfirmed.
    // Managed: the galaxy-map / mission-list "needs redraw" byte (was behind
    // ptr cell 0x10080f8c, a.k.a. LoadBarPersonResources' MissionReloadFlag).
    public static byte MissionsDirty;
    public const int MissionsDirtyPtrSlot = 0x10080f8c;

    // Name-label constants (PEF data-seg doubles): labels draw only at zoom <= 1.75,
    // offset (+7, +4) px from the system dot.
    public const int NameLabelMaxZoomSlot = 0x10081f68; // tocBase-0x66f8 (1.75)
    public const int NameLabelDxSlot = 0x10081f60; // tocBase-0x6700 (7.0)
    public const int NameLabelDySlot = 0x10081f58; // tocBase-0x6708 (4.0)

    // 0x1008a520 (the old "GovtNameTablePtrSlot") → Systems.Model.GovtTable.Store
    // (managed 'gövt' definitions; the name is Store[govt].Name).

    // PTR slot → the trade-good category name strings (toc-0x7814; 0x100-stride
    // Pascal entries, indexed by good category 0..5).
    public const int TradeGoodNameTablePtrSlot = 0x10080e4c;

    // These colour cells migrated to managed packed-colour fields.
    // Use Graphics.Model.UiColors.Frame / .Unexplored / .Neutral.
    public const int FrameRgbColorSlot = 0x10080d30;
    public const int UnexploredSystemColorSlot = 0x10080d00;
    public const int SpaceportSystemColorSlot = 0x10080d2c; // == UiColors.Neutral

    // (Removed: the FrameRgbColor/UnexploredSystemColor/SpaceportSystemColor duplicate literals.
    // They held wrong values (0x000000/0x000000/0x80ff80) under a false TODO claiming the seeder
    // FUN_10052b38 was unported. The real seeder is FUN_10052a3c = Palette.InitHudColors (wired at
    // boot), which fills the live cells 0x10080d30/0x10080d00/0x10080d2c == Graphics.Model.UiColors
    // Frame(0x404040)/Unexplored(0xc0c0c0)/Neutral(0x00ff00). ResolveSystMapColor now
    // reads those seeded UiColors directly, matching the decompile's RGBForeColor(cell) reads.)

    // The player's per-system kill/legal-status record — managed SystemStatusStore
    // above (mainmenu 4-rules batch 2; all former raw walkers swept through here).
    public static short SystemStatus(int systIndex) => SystemStatusStore[systIndex];
    public static void SetSystemStatus(int systIndex, short value) => SystemStatusStore[systIndex] = value;

    // ── nebula (map background) render state (CacheMapNebulaBackgrounds / FUN_10034088) ──

    // The zoom-detail thresholds: the galaxy-map/HUD zoom scale is compared
    // against these to choose the planet-icon PICT detail column (near=0 /
    // mid=1 / far=2). Formerly PEF data-seg doubles at 0x10081f88 (toc-0x66d8)
    // and 0x10081f50 (toc-0x6710) — values dumped from the PEF.
    public const double ZoomDetailNearThreshold = 1.0;
    public const double ZoomDetailFarThreshold = 0.5;

    // PTR slots → the per-icon PICT-handle grid and the icon Rect[] array.
    public const int NebulaPictGridSlot = 0x10080fc4;
    public const int NebulaRectArraySlot = 0x10080f88;
}
