using System;
using System.Collections.Generic;

namespace OpenEV.Override.Ports.Resource;

// Named accessors for the Resource subsystem's fixed-global pointer slots.
// These are LIVE HEAP POINTERS: a boot allocator (outside this folder) NewPtr's
// each buffer and stores the pointer here; the Resource ports then read the
// pointer and deref it. Each accessor/field holds the full 32-bit pointer
// (int) or its migrated managed equivalent.
public static class ResourceGlobals
{
    // ── Documented engine globals ──────────────────────────────────────
    // The in-game render-context record now lives in the typed
    // Core.Model.GlobalState. This accessor is hardcoded to throw, catching
    // any straggler reader that should be using the typed field instead.
    public static int GlobalStateRecord => throw new NotSupportedException(
        "GlobalStateRecord (0x10080d08) migrated to Core.Model.GlobalState — read the typed "
        + "field there instead of the raw render-context pointer.");
    // Toolbox-shim lazy-init flag (was *PTR_DAT_100812a4 — a pointer cell →
    // flag byte). InitRenderWindow sets it, TeardownGlobalGWorlds clears it,
    // the render entry points lazy-init InitToolboxShimGlobals off it.
    public static byte ToolboxShimInitFlag;
    // The two built-in sprite-renderer TVector tokens (PEF-relocated code-pointer
    // cells: 0x100812b8 raw 0x1fb0 / 0x100812bc raw 0x1fbc + dataBase 0x10080660).
    // Constant data-seg values — used as dispatch tokens by the blitters and as
    // the InstallCodeFragment* fallbacks.
    public const int DefaultSpriteRenderer = 0x10082610;  // was *0x100812b8
    public const int SpriteRendererVariant = 0x1008261c;  // was *0x100812bc

    // ── Partial-resource streaming (DrawPictResource ↔ ReadPartialResourceAdvance) ──
    // Was the pointer cells 0x10081aa4 (→ BSS int read cursor) / 0x10081aa8 (→ BSS
    // parked resource Handle): DrawPictResource parks the PICT handle and (on its
    // low-memory progressive path) seeds the cursor to 10, then the custom getPic
    // bottleneck streams the resource via ReadPartialResourceAdvance.
    public static int PartialResStreamHandle;   // was *(*0x10081aa8) — the parked resource Handle
    public static int PartialResStreamCursor;   // was *(*0x10081aa4) — the streaming read offset

    // 0x10081aa0 (GameToc-0x6bc0): PEF-relocated TVector of the custom QD getPic
    // bottleneck proc handed to NewRoutineDescriptor by DrawPictResource's
    // progressive path (dead in the port — the FreeMem() stub always takes the
    // in-memory branch). Read-only code-pointer cell, named accessor.
    public const int StreamGetPicProc = 0x10082500;  // PEF-relocated TVector (was *0x10081aa0; raw 0x1ea0 + dataBase, dumped) — feeds only DrawPictResource's dead progressive path

    // ── Resource-name string cache tables (InitResourceNameStrings) ──────────
    // Managed string[] tables (were heap blocks of N × 0x100-byte Pascal strings
    // behind the ptr cells noted below; consumers read `table[idx]` instead of
    // PascalToString(ptr + idx*0x100)). Filled at boot step 26: a per-id 'STR '
    // resource wins, else the STR# list entry. Pre-filled "" so pre-boot readers
    // see the same empty string an unfilled Pascal slot decoded to.
    private static string[] CreateNames(int n)
    {
        var a = new string[n];
        Array.Fill(a, "");
        return a;
    }
    public static readonly string[] NamesStr0fa1 = CreateNames(64);   // STR# 0xfa1 commodity names (was *0x10080bc4)
    public static readonly string[] NamesStr138b = CreateNames(20);   // STR# 0x138b escape-pod names (was *0x10080b48)
    public static readonly string[] NamesStr138c = CreateNames(128);  // STR# 0x138c outfit names singular (was *0x10080cd0)
    public static readonly string[] NamesStr138d = CreateNames(128);  // STR# 0x138d outfit names plural (was *0x10080cd4)
    public static readonly string[] NamesStr0fa2 = CreateNames(64);   // STR# 0xfa2 mission-cargo short names, status-display abbreviations (was *0x10080e1c)
    public static readonly string[] NamesStr6000 = CreateNames(128);  // STR# 6000 captain names (was *0x10080e20)
    public static readonly string[] NamesStr0088 = CreateNames(9);    // STR# 0x88 (was *0x10080e2c)
    public static readonly string[] NamesStr0086 = CreateNames(18);   // STR# 0x86 legal-status names (was *0x10080e30)
    public static readonly string[] NamesStr0fa6 = CreateNames(128);  // STR# 0xfa6 junk names (was *0x10080e34)
    public static readonly string[] NamesStr0fa5 = CreateNames(128);  // STR# 0xfa5 junk-commodity short names, abbreviations (was *0x10080e38)
    public static readonly string[] NamesStr138a = CreateNames(64);   // STR# 0x138a ship-class long names (was *0x10080e3c)
    public static readonly string[] NamesStr1389 = CreateNames(64);   // STR# 0x1389 ship-class short names (was *0x10080e40)
    /// STR# 0x1389 = the ship-CLASS short names (title pilot panel "Ship Type:" /
    /// shipyard default ship names).
    public static string ShipClassName(int classIdx) => NamesStr1389[classIdx];
    public static readonly string[] NamesStr5000 = CreateNames(128);  // STR# 5000 outfit names (was *0x10080e44)
    public static readonly string[] NamesStr0fa3 = CreateNames(6);    // STR# 0xfa3 cargo type names (was *0x10080e48)
    public static readonly string[] NamesStr4000 = CreateNames(64);   // STR# 4000 commodity names (was *0x10080e4c)
    // The 0x89/0x8a slots are contiguous with the block above; InitResourceNameStrings
    // originally reached them via the WRONG base (tocBase=_toc) — correct base GameToc
    // (split-TOC: GameToc-0x7838=0x10080e28, GameToc-0x783c=0x10080e24).
    public static readonly string[] NamesStr0089 = CreateNames(16);   // STR# 0x89 (was *0x10080e28)
    public static readonly string[] NamesStr008a = CreateNames(11);   // STR# 0x8a combat-rating tiers (was *0x10080e24)
    /// STR# 0x8a = the player's combat-rating rank names (tier 0-10; player-info / pilot panels).
    public static string CombatRatingName(int tier) => NamesStr008a[tier];

    // ── Code-fragment install fragment-name string constants (GetMemFragment arg) ──
    // Used as an ADDRESS (the symbol/name the loader resolves), not a value read,
    // so a bare const. InstallCodeFragment* reach these via GameToc-0x3228/-0x3222
    // (split-TOC fix; their fallback-renderer slots are DefaultSpriteRenderer /
    // SpriteRendererVariant above, reached via GameToc-0x73a8/-0x73a4).
    public const int FragmentNameA = 0x10085438;  // InstallCodeFragmentFromHandle (GameToc-0x3228)
    public const int FragmentNameB = 0x1008543e;  // InstallCodeFragmentVariantB (GameToc-0x3222)

    // ── Misc Resource-subsystem global slots (GameToc-relative data-segment
    //    records confirmed by PEF range) ──
    // HandleWriteSrcRecord is gone — WriteHandleToFile carries the " License" literal.
    // ScanTimerTocPtr 0x100819d8 is gone with the dead catalog-search tree.

    // Styled-text TE-record list (read by UpdateAllTextEdits / DisposeAllTextEditList).
    // WAS a raw doubly-linked chain of NewPtr(0xc) nodes {+0 prev, +4 next, +8 TEHandle}
    // headed at data-seg cell 0x100823fc (GameToc-0x6264, split-TOC fix); the ORIGINAL
    // GAME's FUN_10073690 NewPtr-allocated each node and appended it here (append
    // order == the decompile's append-at-tail order), with the TEHandles staying
    // TEStyleNew/toolbox-owned.
    //
    // NOT CURRENTLY POPULATED: LoadStyledTextResource.cs's Run() (the port of
    // FUN_10073690) never adds to this list — the port's styled-TE chain is unwired
    // (TEStyleNew stubbed to 0), so that port instead routes real styled-text
    // rendering through the working MacToolbox.AddDialogStyledText mechanism.
    // StyledTeList is therefore always empty; harmless today only because
    // TEGetDestRect/TEGetInPort/TEDispose/TEUpdate are all no-op stubs — if those
    // are ever un-stubbed, this list must also be wired up or styled-text
    // update/dispose will silently do nothing.
    public static readonly List<int> StyledTeList = new();

    // 0x10085d4c (data-seg, just above PEF top): the never-read NPC-scanning-player
    // latch -> Core.Model.WorldState.NpcScanningPlayer.
}
