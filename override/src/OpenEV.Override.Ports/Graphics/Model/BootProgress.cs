namespace OpenEV.Override.Ports.Graphics.Model;

// The boot/credits loading-bar state — managed home for the cells formerly behind
// the boot-allocator pointer-slot run [0x10081090, 0x100810ac): the bar Rect, three
// RGBColor records, the two
// progress doubles, and the 'spït' handle park at 0x100810a8. AnimateBootProgressBar
// seeds Total (from 'spït' 128) + the rect + colours; AdvanceCreditsScrollProgress
// accumulates Current; RedrawCreditsProgressBar draws Current/Total with them.
public static class BootProgress
{
    public static double Current;   // was **(GameToc-0x75c0) via PTR slot 0x100810a0
    public static double Total;     // was **(GameToc-0x75bc) via PTR slot 0x100810a4

    // The bar Rect {top,left,bottom,right} (was *0x10081090, GameToc-0x75d0).
    public static readonly short[] BarRect = new short[4];

    // The three bar colours (16-bit RGBColor records → packed 0xRRGGBB):
    public static int BarFrameColor;   // was *0x10081094 (toc-0x75cc) — grey {25000, 25000, 25000}
    public static int BarMidColor;     // was *0x10081098 (toc-0x75c8) — {0, 40000, 0}
    public static int BarFillColor;    // was *0x1008109c (toc-0x75c4) — {0, 0xffff, 0}
}
