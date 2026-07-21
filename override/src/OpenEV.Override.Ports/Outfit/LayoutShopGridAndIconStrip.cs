// Port of FUN_1003ddb4 (EV Override-11.c lines 25327-25368). Its only two
// callers are RunShipyardDialog.cs and Misc/AdvanceLoadout.cs (the outfitter) —
// both shop dialogs, never a mission dialog — and the three GridLayout rect
// blocks it fills are read only by DrawOutfitShop/RedrawShipyardDialog and the
// two icon-strip preloaders.

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Outfit;

public static class LayoutShopGridAndIconStrip
{
    private const int CellWidth = 83;   // 0x53
    private const int CellHeight = 54;  // 0x36
    private const int StripCell = 32;   // 0x20 — icon-strip cell size

    // Managed-rect form ({top,left,bottom,right} shorts — only top/left are read).
    public static void Run(short[] originRect)
        => Run(originRect[0], originRect[1]);

    private static void Run(short originTop, short originLeft)
    {
        // The decompile's ((int)u >> 2) + (neg && rem!=0) sequences are the compiler's
        // truncating signed-division idiom (srawi+addze) — do NOT collapse this back to
        // a bare >> or &, they diverge for negative operands.
        for (short i = 0; i < GridLayout.CellCount; i++)
        {
            int row = i / 4;   // 5 rows
            int col = i % 4;   // 4 columns
            short[] cell = GridLayout.CellRects[i];
            MacToolbox.SetRect(cell, (short)(originLeft + col * CellWidth), (short)(originTop + row * CellHeight),
                               (short)(originLeft + (col + 1) * CellWidth + 1), (short)(originTop + (row + 1) * CellHeight + 1));
            // cell[] is Mac Rect order (top,left,bottom,right) — see MacToolbox.SetRect.
            int midX = (cell[3] + cell[1]) / 2;
            int midY = (cell[0] + cell[2]) / 2;
            MacToolbox.SetRect(GridLayout.IconCellRects[i], (short)(midX - 16), (short)(midY - 24), (short)(midX + 16), (short)(midY + 8));
        }
        for (short i = 0; i < GridLayout.StripCount; i++)
        {
            int row = i / 8;
            int col = i % 8;
            MacToolbox.SetRect(GridLayout.IconStripRects[i], (short)(col * StripCell), (short)(row * StripCell),
                               (short)((col + 1) * StripCell), (short)((row + 1) * StripCell));
        }
    }
}
