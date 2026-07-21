using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007933c from EV Override-11.c lines 51589-51633.
//
// Builds a per-row base-address table for a pixmap/bitmap: row[0] = baseAddr +
// rowBytes*top + (left*depth)/8 (truncating division — srawi+addze in the ASM,
// do not simplify to a bare shift), row[i] = row[i-1] + rowBytes, 0x7fff loop
// guard. The Mac software blitters walked this table for per-row pixel addresses.
public static class BuildPixMapRowTable
{
    // Managed overload: takes the CURRENT row-table ptr (GlobalState.PixMapRowTableBase /
    // the SpriteFrame ColorRowTable/MaskRowTable fields) and RETURNS the rebuilt one,
    // instead of writing through an out-cell.
    //
    // NO-OP: never refills the table — no ColorRowTable/MaskRowTable/PixMapRowTableBase
    // reader remains in the managed renderer (the live blit path is the host RenderTarget
    // bridge, not per-row addresses; grep-verified). The old table is still freed and the
    // result always comes back 0; re-derive the fill formula above vs FUN_1007933c if a
    // software blitter returns.
    public static int Rebuild(int currentTable, int boundsTopLeftPacked, int boundsBotRightPacked, int baseAddr, short rowBytes, short pixelDepth)
    {
        if (currentTable != 0)
            MacToolbox.DisposePtr(currentTable);
        return 0;
    }
}
