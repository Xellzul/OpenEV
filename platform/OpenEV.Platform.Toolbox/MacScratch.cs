using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenEV.Platform.Toolbox;

// Named scratch address constants standing in for Mac stack locals used by the
// title-screen FUN_xxx ports. On the Mac these were stack-allocated (e.g.
// `auStack_44 [8]`, an 8-byte Rect in FUN_10046a88); the port hands each a stable
// address token the Toolbox shims (PaintRect, DrawString, CopyBits, ...) key their
// managed staging off of. See Alloc() below for the general-purpose token registry.
//
// The prefs Rect scratch slots below sit around 0x10200500 (clear of the title's
// record storage in TitleMemory, 0x10200000-0x102002ff); PrefsTextStr's Str255
// runs on past 0x102005ff. The GWorld pixmap pointers are separate Mac TOC cells
// (0x1008f6xx), outside this range.
public static class MacScratch
{
    // FunPrefsDialog (prefs modal) scratch rects.
    /// Rect for the dialog's outer panel chrome (DLOG 4001 bounds in
    /// virtual coords). Used by the panel paint + frame.
    public const int PrefsPanelRect       = 0x10200500;
    /// Rect for PICT 132 ("Keys" background) — item 34 in DITL 4001
    /// translated to virtual coords.
    public const int PrefsItem34Rect      = 0x10200520;
    /// Rect for a generic Picture-kind DITL item (volume arrows etc.)
    /// being rendered this frame.
    public const int PrefsPictureRect     = 0x10200560;
    /// Rect for a Button-kind DITL item's frame this frame.
    public const int PrefsButtonRect      = 0x10200580;
    /// Rect for the 3-pixel default-button outer outline (Inside
    /// Macintosh: Dialog Manager convention) on the OK button.
    public const int PrefsDefaultOutline  = 0x10200590;
    /// Rect for a Checkbox-kind DITL item's 12×12 tick box.
    public const int PrefsCheckboxBox     = 0x102005a0;
    /// Pascal string scratch for any text rendering inside the prefs
    /// dialog (button labels, checkbox labels, volume display, etc.).
    public const int PrefsTextStr         = 0x102005c0;

    // Mac scratch-GWorld pixmap pointers (for CopyBits).
    /// Pixmap pointer for the title's backdrop scratch GWorld. Mac
    /// decompile reference: `iRam1008f6ec + 2` (the +2 skips the
    /// pixmap handle indirection). FUN_10046a88 / FUN_10046da0 source
    /// from this to CopyBits the title backdrop (PICT 8000) over
    /// freshly-revealed button rects.
    public const int BackdropScratchPixmap = 0x1008f6ee;

    /// Pixmap pointer for the title's animation scratch GWorld. Mac
    /// decompile reference: `*(int*)(piVar1 + 0xe) + 2` (piVar1 = main
    /// port record; +0x38 = anim scratch handle; +2 = pixmap). Holds
    /// the currently-animating row-reveal PICT (8300..8316). Address
    /// chosen to mirror the Mac TOC layout (BackdropScratch + 0x12).
    public const int AnimScratchPixmap     = 0x1008f700;

    // General call-site scratch allocator.
    //
    // Most graduated FUN_xxx ports declare locals that were Mac stack
    // buffers — `auStack_28 [8]` (a Rect), `Str255` name buffers,
    // `FSSpec` records, `DateTimeRec`s — and pass their ADDRESS to a
    // Toolbox shim (FSMakeFSSpec, GetIndString, GetTime, CopyBits, ...).
    // The earlier transcription rendered these as `int auStack_28 = 0;` with no backing,
    // so every such buffer pointed at address 0. When two coexist in one
    // function (e.g. ApplyDefaultPrefsToMemory's FSSpec + name string)
    // they ALIASED at 0 and corrupted each other.
    //
    // Alloc() hands each distinct call site a stable, non-overlapping
    // address in a reserved region (0x10210000+). Properties that match
    // the Mac stack exactly:
    //   • same site, called repeatedly (loops, re-entry) → SAME address,
    //     because the Mac reused that stack slot each time too;
    //   • two different sites (even in the same function) → DIFFERENT
    //     addresses, so live buffers never alias;
    //   • each shim that actually stages data behind the token keeps its own
    //     managed store keyed by that address (e.g. FsSpec/_specName in
    //     MacToolbox.FileManager.cs) — the token is a stable identity, not a
    //     live EvoMemory byte range (EvoMemory is gone).
    //
    // Region 0x10210000.. is clear of the canonical globals (≤0x1009ffff)
    // and the named title scratch slots above (0x10200000-0x102005ff).
    private const int ScratchRegionBase = 0x10210000;
    private const int ScratchSiteStride = 0x800;   // 2 KB per site — covers Str255, FSSpec, any Mac stack record
    private static readonly object _scratchLock = new();
    private static readonly Dictionary<(string, int), int> _scratchSites = new();
    private static int _scratchNext = ScratchRegionBase;

    /// Reserve (or return the previously-reserved) scratch address for the
    /// calling source line. `size` is advisory — the fixed 2 KB stride
    /// covers every Mac stack record we transcribe; it exists so the call
    /// reads as documentation of how big the buffer was.
    public static int Alloc(int size = 0x800,
                            [CallerFilePath] string file = "",
                            [CallerLineNumber] int line = 0)
    {
        var key = (file, line);
        lock (_scratchLock)
        {
            if (_scratchSites.TryGetValue(key, out var addr))
                return addr;
            addr = _scratchNext;
            // Round the per-site reservation up to the stride so an
            // oversized buffer (size > stride) still gets contiguous room.
            int reserved = size <= ScratchSiteStride
                ? ScratchSiteStride
                : ((size + ScratchSiteStride - 1) / ScratchSiteStride) * ScratchSiteStride;
            _scratchNext += reserved;
            _scratchSites[key] = addr;
            return addr;
        }
    }
}
