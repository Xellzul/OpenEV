namespace OpenEV.Override.Ports.Graphics.Model;

// Managed home for the shipyard/outfitter grid-layout rects (each was a
// heap Rect[] block reached by its own ptr cell, noted per field below). Each
// rect is the project's {top,left,bottom,right} short[4] convention. Written by
// Outfit.LayoutShopGridAndIconStrip; read by the shipyard / outfitter draw
// paths and the icon-strip preloaders.
public static class GridLayout
{
    public const int CellCount = 20;
    public const int StripCount = 128;

    // The 4x5 grid cell rects (window coords). Was the heap Rect[] behind ptr cell 0x1008100c.
    public static readonly short[][] CellRects = Make(CellCount);
    // The 32x32 icon destination rect centred in each cell. Was the heap Rect[] behind ptr cell 0x10081004.
    public static readonly short[][] IconCellRects = Make(CellCount);
    // The per-index source rect inside the 8-wide icon-strip GWorld. Was the heap Rect[] behind ptr cell 0x10081008.
    public static readonly short[][] IconStripRects = Make(StripCount);

    private static short[][] Make(int n)
    {
        var a = new short[n][];
        for (int i = 0; i < n; i++) a[i] = new short[4];
        return a;
    }
}
