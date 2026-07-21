namespace OpenEV.Override.Ports.Core.Model;

// Managed homes for the low-level Mac / boot globals that the toolbox boot sequence
// (InitToolboxBootSequence / SystemVersionCheck / InitFullScreenOffscreenWorld) used to
// poke through raw data-segment cells. Migrated to managed fields so those call trees
// hold no unmanaged (EvoMemory) state.
public static class SystemGlobals
{
    // _DAT_10080dd8 → the application QuickDraw globals base (qd). InitGraf is passed
    // qd.thePort (base + 0xca). The toolbox InitGraf is a no-op shim and nothing seeds a
    // real qd struct in the port, so this stays 0 — kept as a field for fidelity to the call.
    public static int QuickDrawGlobalsPtr;

    // Data-seg short at GameToc+0x7112, stamped 8 at startup (SystemVersionCheck). No
    // reader exists in the decompile — kept for fidelity.
    public static short StartupMarker;

    // Data-seg byte at GameToc+0x6d5a — 1 when Gestalt('qtim') (QuickTime) is present.
    public static byte QuickTimePresent;

    // (The game-window bounds Rect — the heap record *0x100811bc — lives in
    // Core.Model.GameWindowGlobals.GameWindowBounds; the GameWindow* fields here were a
    // reader-less duplicate home, removed.)

    // "Old OS" warning state (ShowOldOsWarningIfNeeded). Was the flag byte at *(0x10080e0c)
    // and the DialogPtr cell at *(0x100811ec). True once the System-7.5+ warning is latched;
    // the warning dialog (DLOG 0xbc2) is dead on every platform (its guard always returns 1).
    public static bool OldOsWarningAcknowledged;
    public static int OldOsWarningDialog;
}
