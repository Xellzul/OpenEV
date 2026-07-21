using System;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// Real implementations of the QuickDraw pen / Rect-by-value primitives that
// draw the in-game RADAR (and other HUD overlays built from collapsed stack
// Rects). These were no-op absorbers, so DrawRadarHud computed correct blip
// positions but nothing rendered — the "empty radar" symptom. Each enqueues a
// Canvas closure tagged with the current draw target (SetPort →
// CurrentDrawTarget): the radar sets the port to the backdrop GWorld, draws its
// blips there, then CopyBits the radar rect onto the on-screen game GWorld.
//
// The pen primitives mirror Mac QuickDraw: Line(dh,dv) strokes from the pen to
// pen+(dh,dv); Line(0,0) paints the single pen pixel (the radar blip dot).
public static partial class MacToolbox
{
    private static RectI RectFromShorts(short[] r)
        => new RectI(r[1], r[0], System.Math.Max(0, r[3] - r[1]), System.Math.Max(0, r[2] - r[0]));

    /// QuickDraw Line(dh, dv): stroke from the pen to pen+(dh,dv) in the current
    /// fore colour, then advance the pen. Line(0,0) = one pixel at the pen.
    public static void Line(int dh, int dv)
    {
        int x0 = _penX, y0 = _penY, x1 = _penX + dh, y1 = _penY + dv;
        _penX = x1; _penY = y1;
        DrawLineSegment(x0, y0, x1, y1);
    }

    /// QuickDraw LineTo(h, v): stroke from the pen to the ABSOLUTE point (h, v) in
    /// the current fore colour, then advance the pen there. The absolute cousin of
    /// Line(dh, dv).
    public static void LineTo(int x, int y)
    {
        int x0 = _penX, y0 = _penY;
        _penX = x; _penY = y;
        DrawLineSegment(x0, y0, x, y);
    }

    private static void DrawLineSegment(int x0, int y0, int x1, int y1)
    {
        // Use _activeForeColor (updated by BOTH ForeColor and RGBForeColor) — the
        // radar sets blip colours via RGBForeColor(friendly/neutral/hostile), which
        // ResolveForeColor() (keyed only on the indexed _foreColor) would ignore,
        // rendering every blip black.
        var color = _activeForeColor;
        // Snapshot the QuickDraw pen rect (PenSize / PenNormal) NOW: the draw closures
        // run later at flush, so per-segment thickness must be captured at enqueue time
        // (same reason `color` is). Callers set a wide pen for beam lasers / hyperspace
        // lanes / the galaxy-map route, then reset to 1 — without this the strokes were 1px.
        int penW = System.Math.Max(1, _penW), penH = System.Math.Max(1, _penH);
        // Canvas.StrokeLine handles both the zero-length pen-dot (penW×penH at the
        // pen) and the penH-thick band hanging to one side of the segment, matching
        // QuickDraw's pen sweep (the old rotated, top-left-anchored white quad).
        EnqueueDraw(c => c.StrokeLine(x0, y0, x1, y1, penW, penH, color));
    }

    /// QuickDraw FrameOval(Rect) for a collapsed short[4] Rect: the oval REGION of
    /// the rect minus the oval region of the rect inset by the pen size — QuickDraw's
    /// actual definition, so the ring is a solid connected outline that nests exactly
    /// inside a same-rect PaintOval. (The previous parametric dot-walk plotted a
    /// sparse "beaded" ring that also overshot the rect by 1px right/bottom — the
    /// galaxy-map system dots rendered as dotted circles and the radar planet blips
    /// as hollow diamonds. Pixel-verified against a SheepShaver capture: the 8×8
    /// galaxy dot ring and the 4×4 radar blip ring both match the original exactly.)
    public static void FrameOval(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) rc = new RectI(rect[1], rect[0], 1, 1);
        var color = _activeForeColor;   // RGBForeColor blip colour (see DrawLineSegment note)
        // Pen thickness snapshotted at enqueue time, like DrawLineSegment.
        int penW = System.Math.Max(1, _penW), penH = System.Math.Max(1, _penH);
        var inner = new RectI(rc.X + penW, rc.Y + penH, rc.Width - 2 * penW, rc.Height - 2 * penH);
        EnqueueDraw(c =>
        {
            for (int y = rc.Y; y < rc.Bottom; y++)
            {
                var (ox0, ox1) = OvalSpan(rc, y);
                if (ox1 <= ox0) continue;
                var (ix0, ix1) = OvalSpan(inner, y);
                if (ix1 <= ix0)
                {
                    c.FillRect(new RectI(ox0, y, ox1 - ox0, 1), color);   // no interior on this row
                    continue;
                }
                if (ix0 > ox0) c.FillRect(new RectI(ox0, y, ix0 - ox0, 1), color);
                if (ox1 > ix1) c.FillRect(new RectI(ix1, y, ox1 - ix1, 1), color);
            }
        });
    }

    /// PaintOval for a managed {top,left,bottom,right} short[4] rect.
    public static void PaintOval(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) return;
        DrawOvalFilled(rc, _activeForeColor);
    }

    // QuickDraw's oval region, one scanline at a time: the [x0, x1) span of the
    // ellipse inscribed in `rc` at row y (pixel-centre sampling, round-to-nearest).
    // Rects ≤2px in either dimension are solid, matching QuickDraw's degenerate
    // ovals. Pixel-verified against the original at 8×8, 6×6, 4×4 and 2×2.
    private static (int x0, int x1) OvalSpan(RectI rc, int y)
    {
        int w = rc.Width, h = rc.Height;
        if (w <= 0 || h <= 0 || y < rc.Y || y >= rc.Bottom) return (0, 0);
        if (w <= 2 || h <= 2) return (rc.X, rc.Right);
        double cx = rc.X + w / 2.0, cy = rc.Y + h / 2.0, rx = w / 2.0, ry = h / 2.0;
        double ny = (y + 0.5 - cy) / ry;
        if (ny < -1.0 || ny > 1.0) return (0, 0);
        double half = rx * System.Math.Sqrt(System.Math.Max(0.0, 1.0 - ny * ny));
        return ((int)System.Math.Round(cx - half), (int)System.Math.Round(cx + half));
    }

    // Filled ellipse via the per-scanline oval-region spans.
    private static void DrawOvalFilled(RectI rc, RgbaColor color)
    {
        EnqueueDraw(c =>
        {
            for (int y = rc.Y; y < rc.Bottom; y++)
            {
                var (x0, x1) = OvalSpan(rc, y);
                if (x1 > x0) c.FillRect(new RectI(x0, y, x1 - x0, 1), color);
            }
        });
    }

    /// QuickDraw FillCRect(Rect, PixPatHandle) for a collapsed short[4] Rect — the radar
    /// interference/static + armor-bar fill. The 2nd arg is a Mac PixPat HANDLE (from
    /// GetPixPat); we resolve its decoded 'ppat' tile and tile it into the rect. When the
    /// pattern is unavailable (undecodable / unknown handle) we fall back to a flat fill of
    /// the active fore colour — the prior behaviour.
    public static void FillCRect(short[] rect, int pixPatHandle)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) return;
        var tile = ResolvePixPat(pixPatHandle);
        if (tile is not null)
        {
            EnqueueDraw(cv => cv.FillPattern(rc, tile));
            return;
        }
        var c = _activeForeColor;
        EnqueueDraw(cv => cv.FillRect(rc, c));
    }

    /// QuickDraw ScrollRect(Rect, dh, dv, updateRgn) for a collapsed short[4] Rect —
    /// the galaxy-map drag-pan (ScrollGalaxyMapArea, port of FUN_10033fac). Shifts the
    /// current port's pixels inside `rect` by (dh, dv) and fills the vacated strip. Its
    /// sole caller repaints the whole rect over this a moment later (DrawGalaxyMap's
    /// CopyBits with ScrollInProgress set), so the scroll only shows as the intermediate
    /// slide the map modal's unbatched draws let the host present — the Mac was
    /// single-buffered and showed exactly that. Was a no-op stub; restored per the
    /// "a no-op NOW is no licence to keep it one" rule after auditing the one call site.
    ///
    /// updateRgn is ignored: the caller passes GalaxyMapState.UpdateRgn, a stub-NewRgn
    /// handle (0) nothing reads (modelling the update region would be invented substrate).
    /// The vacated strip fills black: the Mac fills it with the current bkPat, which
    /// ScrollGalaxyMapArea sets to qd.black (+0xBA) just above the call; BackPat is an
    /// unseeded no-op here, so we fill black directly (also the map area's own background).
    public static void ScrollRect(short[] rect, int dh, int dv, int updateRgn)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);                 // captured at enqueue time (closure runs at drain)
        if (rc.Width <= 0 || rc.Height <= 0) return;
        EnqueueDraw(c => c.ScrollRect(rc, dh, dv, RgbaColor.Black));
    }

}
