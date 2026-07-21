using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// ONE QuickDraw region as a managed C# object. The Mac rect-region header is
// 10 bytes: rgnSize(=10) + rgnBBox{top,left,bottom,right} — and that is ALL
// the game ever modelled (the old NewHandleClear(10) stubs never carried scanline
// data). This class is that header, typed. Clip TESTS stay always-true
// (RectInRgn etc.), matching the no-clip rendering the port has always done;
// the BBox exists so RectRgn/CopyRgn/OffsetRgn/SectRgn round-trip faithfully
// for the sprite-mask region path (BlitSpriteByDepth's CopyBits mask arg).
public sealed class MacRegion
{
    public readonly int Handle;
    public short BBoxTop, BBoxLeft, BBoxBottom, BBoxRight;   // rgnBBox

    internal MacRegion(int handle) => Handle = handle;

    public void SetBBox(short top, short left, short bottom, short right)
    {
        BBoxTop = top; BBoxLeft = left; BBoxBottom = bottom; BBoxRight = right;
    }
    public void CopyFrom(MacRegion src)
    {
        BBoxTop = src.BBoxTop; BBoxLeft = src.BBoxLeft;
        BBoxBottom = src.BBoxBottom; BBoxRight = src.BBoxRight;
    }
    public void Offset(int dh, int dv)
    {
        BBoxTop = (short)(BBoxTop + dv); BBoxLeft = (short)(BBoxLeft + dh);
        BBoxBottom = (short)(BBoxBottom + dv); BBoxRight = (short)(BBoxRight + dh);
    }
}

// Registry mapping the int "RgnHandle" ported code stores (port visRgn/clipRgn,
// sprite mask regions, the temp region cell) to the managed object. Handles at
// 0x74000000+ (see MacGrafPort for the handle-band map).
public static class MacRegions
{
    public const int HandleBase = 0x74000000;
    private const int Stride = 0x10;

    private static readonly Dictionary<int, MacRegion> _store = new();
    private static int _nextHandle = HandleBase;

    /// NewRgn — a fresh empty region (BBox all zero, like NewHandleClear(10)).
    public static MacRegion New()
    {
        _nextHandle += Stride;
        var rgn = new MacRegion(_nextHandle);
        _store[_nextHandle] = rgn;
        return rgn;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static MacRegion At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);
    public static void Dispose(int handle) => _store.Remove(handle);
}
