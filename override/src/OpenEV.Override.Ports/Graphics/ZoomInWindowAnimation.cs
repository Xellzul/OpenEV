using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10073030 (EV Override-11.c lines 47517-47580): the classic Mac "zoom rectangles"
// window-open animation. Reads the window's portRect corners (window+0x10 / window+0x14, the
// window-record boundary accessor now), globalizes them, then draws 17 expanding frame rects
// (in the window-manager port, clipped to the gray region's bounding box) before ShowWindow.
//
// Re-derived: the previous pass built the animated Rect and the corner Points TRANSPOSED —
// the packed dword's LOW half (the h/left coord) was stored in the v/top slot and vice versa,
// and the inset steps were swapped to match — internally consistent, but FrameRect/EraseRect
// then drew every frame x/y-swapped on screen. Rect/Point packing is now the real Mac layout
// (packed {v@hi, h@lo}; Rect {top,left,bottom,right}).
// Also fixed: ClipRect(*grayRgn + 2) — the gray region's rgnBBox sits at master-ptr+2; the
// previous pass read ReadInt(handle + 2) (missing the handle deref, wrong field). GetGrayRgn
// is a 0-stub in the port, so the bbox resolves through the managed-region registry when real.
public static class ZoomInWindowAnimation
{
    public static void Run(int window)
    {
        int[] savedPort = new int[3];
        MacToolbox.GetPort(savedPort);
        MacToolbox.GetPortRect(window, out int topLeft, out int botRight);   // window+0x10/+0x14 (boundary accessor)
        MacToolbox.SetPort(window);

        topLeft = GlobalizePoint(topLeft);
        botRight = GlobalizePoint(botRight);

        MacToolbox.GetCWMgrPort(out int wmgrPort);
        MacToolbox.SetPort(wmgrPort);
        MacToolbox.PenNormal();
        int grayRgn = MacToolbox.GetGrayRgn();
        short[] grayBBox = MacRegions.IsHandle(grayRgn)
            ? new[] { MacRegions.At(grayRgn).BBoxTop, MacRegions.At(grayRgn).BBoxLeft,
                      MacRegions.At(grayRgn).BBoxBottom, MacRegions.At(grayRgn).BBoxRight }
            : new short[4];   // the port's GetGrayRgn stub returns 0 — empty clip (ClipRect is a no-op shim)
        MacToolbox.ClipRect(grayBBox);

        int stepH = HalfOf(SixteenthOf((short)botRight - (short)topLeft));               // right - left
        int stepV = HalfOf(SixteenthOf((short)(botRight >> 16) - (short)(topLeft >> 16))); // bottom - top

        // Animated Rect {top,left,bottom,right} = the globalized window portRect.
        short[] rect =
        {
            (short)(topLeft >> 16), (short)topLeft,
            (short)(botRight >> 16), (short)botRight,
        };

        int[] tickOut = new int[1];   // Delay's out-tick; result unused.
        MacToolbox.InsetRect(rect, (short)(stepH * 16), (short)(stepV * 16));
        for (short i = 0; i < 17; i = (short)(i + 1))
        {
            MacToolbox.Delay(1, tickOut);
            MacToolbox.FrameRect(rect);
            MacToolbox.InsetRect(rect, 1, 1);
            MacToolbox.EraseRect(rect);
            MacToolbox.InsetRect(rect, -1, -1);
            MacToolbox.InsetRect(rect, (short)-stepH, (short)-stepV);
        }

        MacToolbox.ShowWindow(window);
        MacToolbox.SetPort(savedPort[0]);
    }

    // Globalize a packed Point ({v@hi, h@lo}) via LocalToGlobal — a no-op shim in the port, kept
    // for the structure (the original stages the Point in two stack shorts and rebuilds it).
    private static int GlobalizePoint(int packed)
    {
        short[] pt = { (short)(packed >> 16), (short)packed };   // {v, h}
        MacToolbox.LocalToGlobal(pt);
        return (pt[0] << 16) | (pt[1] & 0xffff);
    }

    // Signed divide-by-16 / by-2, rounded toward zero (matches the decompile's >>4/>>1 + carry idiom).
    private static int SixteenthOf(int v) => (v >> 4) + ((v < 0 && (v & 0xf) != 0) ? 1 : 0);
    private static int HalfOf(int v) => (v >> 1) + ((v < 0 && (v & 1) != 0) ? 1 : 0);
}
