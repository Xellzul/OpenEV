namespace OpenEV.Override.Ports.Graphics.Model;

// Managed model of EVO's palette / GDevice-depth state record — the heap struct the
// decompile reached through *PaletteStateRecordSlot (0x100823f0 == _DAT_100823f0).
// Migrated to managed fields so the palette teardown / fade / restore paths hold no
// unmanaged (EvoMemory) record state. Field names carry the original byte offset.
//
// Inert in the port's true-colour renderer (the whole palette layer is gated on
// RenderGlobals.ColorQuickDrawAvailable, always 0), so these stay at their defaults;
// kept faithful to the decompile's record layout for parity.
public static class PaletteState
{
    public static byte Flag0;        // +0    — depth-restore guard byte
    public static int GDevice;      // +2    — target screen GDevice handle
    public static short SavedDepth;   // +6    — saved screen pixel depth (teardown)
    public static int SnapshotCTab; // +8    — saved-snapshot palette CTabHandle
    public static int SnapshotSeed; // +0xc  — saved-snapshot CTable seed
    public static int SavedCTab;    // +0x10 — saved palette CTabHandle
    public static int SavedSeed;    // +0x14 — saved CTable seed / first colour
    public static short DepthCheck;   // +0x18 — expected depth (resync check)
    public static byte FadedFlag;    // +0x1a — 1 while a colour fade is applied
    public static short FadedRed;     // +0x1c — saved fade target colour (RGB shorts)
    public static short FadedGreen;   // +0x1e
    public static short FadedBlue;    // +0x20
}
