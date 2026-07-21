namespace OpenEV.Override.Ports.Dialog.Model;

// MANAGED home for the Mac menu-bar hide/restore globals.
// The Mac stored these as pointers-to-storage in the GameToc cluster
// 0x100811f4..0x10081200 (-0x746c..-0x7460). The whole subsystem is inert on Windows —
// no Mac menu bar, region traps are stubs — but the build/hide/restore
// handshake (BuildMenuBarGrayRegion / HideMacMenuBar / RestoreMacMenuBar) is
// kept faithful. Not backed by a single FUN_xxxxxxxx — a managed-state home for
// globals scattered across those three functions.
public static class MenuBarState
{
    /// GrayRgn ∪ menu-bar rect (region handle from BuildMenuBarGrayRegion). Slot 0x100811fc.
    public static int GrayRgn;
    /// The pre-hide GrayRgn copy HideMacMenuBar saves for Restore. Slot 0x100811f8.
    public static int SavedRgn;
    /// The pre-hide LMGetMBarHeight value. Slot 0x100811f4.
    public static short SavedMBarHeight;
    /// "Menu bar is hidden" flag. Slot 0x10081200.
    public static byte Hidden;
}
