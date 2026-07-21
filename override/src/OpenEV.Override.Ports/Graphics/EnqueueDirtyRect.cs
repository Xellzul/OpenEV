using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007bfac (EV Override-11.c lines 53145-53172): clamp the incoming dirty rect to
// the play area (GlobalState.InnerRight/InnerBottom) and head-insert it onto the per-frame
// dirty-rect list for UpdateWindowRegionLayout's erase + composite passes. The managed list
// entry is a {top,left,bottom,right} short[4] (was a NewPtr(0xc) node {rect, next}); the
// decompile's node reads {top,left} from rect+0 and {bottom,right} from rect+2 shorts (= +4 bytes).
public static class EnqueueDirtyRect
{
    // Clamp + enqueue a managed rect; the caller's array is mutated in place, matching the
    // original's in-place clamp of the caller's rect.
    public static void Run(short[] rect)
    {
        if (rect[0] < 0) rect[0] = 0;
        if (GlobalState.InnerBottom < rect[2]) rect[2] = GlobalState.InnerBottom;
        if (rect[1] < 0) rect[1] = 0;
        if (GlobalState.InnerRight < rect[3]) rect[3] = GlobalState.InnerRight;

        GlobalState.DirtyRects.Insert(0, new[] { rect[0], rect[1], rect[2], rect[3] });
    }
}
