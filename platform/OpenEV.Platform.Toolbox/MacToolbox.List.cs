using System;
using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// Minimal managed Mac List Manager. The real List Manager + EVO's custom LDEF
// (theProc resource) aren't portable, so the L* traps were no-op stubs — which
// left every list box BLANK (the mission BBS list, the active-missions list).
// This stores rows of text + a single selection and draws them on LUpdate, which
// is all those single-column lists need. Cells are QuickDraw Points packed as
// (v << 16 | h): v = row, h = column (always 0 here).
public static partial class MacToolbox
{
    // LDEF selected-row fill (see ListDraw below) and the platinum-theme scrollbar chrome
    // (see ListDrawScrollBar) — host substitute chrome, not decompile-derived colours.
    private const uint ListSelectedRowFill = 0x008800u;
    private const uint PlatinumTrackFill = 0xEEEEEEu;
    private const uint PlatinumFrame = 0x444444u;
    private const uint PlatinumShadeLine = 0x555555u;
    private const uint PlatinumThumbFill = 0xCCCCCCu;
    private const uint PlatinumArrowActive = 0x000000u;
    private const uint PlatinumArrowDisabled = 0x888888u;

    internal sealed class MacListRec
    {
        public short[] ViewRect = new short[4];   // {top,left,bottom,right}, window-local
        public int Window;
        public int RowCount;
        public string[] Cells = Array.Empty<string>();
        public int SelectedRow = -1;
        public int RowHeight = 12;                 // Geneva-9 cell height
        public byte SelFlags;
        public int DrawingMode = 1;
        public int TopRow;                         // first visible row (vertical scroll offset)

        public int VisibleRows => Math.Max(1, (ViewRect[2] - ViewRect[0]) / (RowHeight > 0 ? RowHeight : 12));
        public int MaxTopRow => Math.Max(0, RowCount - VisibleRows);
        public bool NeedsScroll => RowCount > VisibleRows;
        public const int ScrollBarWidth = 15;

        public void ClampTop()
        {
            if (TopRow > MaxTopRow) TopRow = MaxTopRow;
            if (TopRow < 0) TopRow = 0;
        }
        // Scroll so the selected row is within the visible window.
        public void EnsureSelectedVisible()
        {
            if (SelectedRow < 0) return;
            if (SelectedRow < TopRow) TopRow = SelectedRow;
            else if (SelectedRow >= TopRow + VisibleRows) TopRow = SelectedRow - VisibleRows + 1;
            ClampTop();
        }
    }

    // List Manager handle band — its OWN slot in the registry band map (see
    // MacGrafPort.cs). 0x7c000000 sits above dialogs (0x78) and clear of every
    // other band, so a list handle never aliases a grafport/pixmap/region handle.
    // (It formerly started at 0x6c000000 — the grafport band — where after 64
    // lists the values began overlapping live grafport handles.)
    private const int ListHandleBase = 0x7c000000;
    private static readonly Dictionary<int, MacListRec> _macLists = new();
    private static int _macListNextHandle = ListHandleBase;

    internal static MacListRec? ResolveList(int handle)
        => (handle != 0 && _macLists.TryGetValue(handle, out var l)) ? l : null;

    /// First visible row (vertical scroll offset) — for tests/diagnostics.
    public static int LGetTopRow(int lHandle) => ResolveList(lHandle)?.TopRow ?? 0;

    /// Dispose a managed list (more specific than the params-object LDispose stub).
    public static void LDispose(int lHandle) => _macLists.Remove(lHandle);

    /// Scroll the list by dRows (and dCols, unused — single column). Mac LScroll.
    public static void LScroll(int dCols, int dRows, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is null) return;
        l.TopRow += dRows;
        l.ClampTop();
    }

    // Create a managed list. dataBounds is {top,left,bottom,right}: rows = bottom-top.
    internal static int ListNew(short[] rView, short[] dataBounds, int theWindow)
    {
        if (rView is null || rView.Length < 4 || dataBounds is null || dataBounds.Length < 4) return 0;
        int rows = dataBounds[2] - dataBounds[0];
        if (rows < 0) rows = 0;
        var l = new MacListRec
        {
            ViewRect = new short[] { rView[0], rView[1], rView[2], rView[3] },
            Window = theWindow,
            RowCount = rows,
            Cells = new string[rows],
            SelectedRow = -1,
        };
        int h = _macListNextHandle;
        _macListNextHandle += 4;
        _macLists[h] = l;
        return h;
    }

    // Draw the list into the CURRENT port (the window), reproducing the app's own
    // LDEF 128 (disassembled from the 'LDEF' 128 resource in the EV Override app
    // fork): every cell PAINTS its background — ForeColor(blackColor) for
    // unselected cells, ForeColor(greenColor) for the selected row — then draws
    // the cell text in ForeColor(whiteColor). Verified against the user's real
    // SheepShaver capture of the BBS list (2026-07-02) at pixel level: black rows,
    // WHITE text, green selection (0,165,0), black tail below the last row. (An
    // earlier pass misread that capture at thumbnail scale as white-rows/black-
    // text List-Manager-default and inverted this — the LDEF paints its own
    // colors; the white-rows look was the regression, not the fix.)
    // Cells span the full rView width; the vertical scrollbar lives OUTSIDE rView
    // (Mac List Manager geometry) and is drawn by ListDrawScrollBar below.
    // Caller has already SetPort(window) + TextFont/TextSize; cells window-local.
    internal static void ListDraw(MacListRec l)
    {
        l.ClampTop();
        int top = l.ViewRect[0], left = l.ViewRect[1], bottom = l.ViewRect[2], right = l.ViewRect[3];
        int rowH = l.RowHeight > 0 ? l.RowHeight : 12;
        int visible = l.VisibleRows;

        // Paint the WHOLE list body black first (the LDEF's unselected-cell fill,
        // extended over the blank tail): LClick's hold-drag redraws the list
        // incrementally as the selection tracks the mouse, so a row that WAS
        // hilited a moment ago must have its old green fill cleared; and the tail
        // below the last real row reads as the same black the cells use (in the
        // reference the whole box is black except the green selection).
        var bodyRect = new short[] { (short)top, (short)left, (short)bottom, (short)right };
        ForeColor(QuickDrawColor.Black);                                      // blackColor — cell background (LDEF unselected fill)
        PaintRect(bodyRect);

        var rowRect = new short[4];
        for (int i = 0; i < visible; i++)
        {
            int row = l.TopRow + i;
            if (row >= l.RowCount) break;
            int rTop = top + i * rowH;
            int rBot = Math.Min(rTop + rowH, bottom);
            string text = (row < l.Cells.Length ? l.Cells[row] : null) ?? "";
            if (row == l.SelectedRow)
            {
                rowRect[0] = (short)rTop; rowRect[1] = (short)left;
                rowRect[2] = (short)rBot; rowRect[3] = (short)right;
                // LDEF 128 selected fill = ForeColor(0x155) — the CLASSIC QuickDraw
                // greenColor, RGB {0x0000,0x8000,0x11B0} — which on the game's 8-bit
                // system palette quantizes to the green-ramp entry (0,0x8888,0).
                // Through the host's Mac-DAC gamma that renders (0,165,0), exactly
                // the selected-row green in the SheepShaver reference capture.
                RGBForeColor(ListSelectedRowFill);
                PaintRect(rowRect);
            }
            ForeColor(QuickDrawColor.White);                                  // whiteColor — LDEF draws ALL cell text white
            MoveTo(left + 3, rTop + rowH - 3);                // baseline near the cell bottom
            DrawString(text);
        }
        ListDrawScrollBar(l);
        ForeColor(QuickDrawColor.Black);
    }

    // The list's vertical scrollbar — Mac List Manager geometry: a 16px strip
    // hugging rView's RIGHT edge, one pixel proud of its top/bottom, present
    // whether or not there is anything to scroll (inactive = empty track).
    //
    // SUBSTITUTE CHROME: on the Mac this control is drawn by the OS scrollbar
    // CDEF (platinum theme), not by any EVO resource, so the port has to supply
    // a stand-in. The colors are the platinum constants (#444444 frame, #555555
    // top shading, #EEEEEE track, #888888 disabled arrow glyphs) — the host's
    // Mac-DAC gamma maps them to exactly the values measured in the user's
    // SheepShaver reference capture (102/119/243/165). Both arrows sit together
    // at the bottom, matching that capture (the Mac OS 8.5+ Smart-Scrolling
    // default layout).
    private const int ScrollArrowCellH = 14;
    private static void ListDrawScrollBar(MacListRec l)
    {
        int top = l.ViewRect[0] - 1, bottom = l.ViewRect[2] + 1;
        int sbLeft = l.ViewRect[3], sbRight = sbLeft + MacListRec.ScrollBarWidth + 1;
        bool active = l.NeedsScroll;

        var sb = new short[] { (short)top, (short)sbLeft, (short)bottom, (short)sbRight };
        RGBForeColor(PlatinumTrackFill);
        PaintRect(sb);
        RGBForeColor(PlatinumFrame);
        FrameRect(sb);
        int downTop = bottom - 1 - ScrollArrowCellH;          // divider above the down-arrow cell
        int upTop = downTop - 1 - ScrollArrowCellH;           // divider above the up-arrow cell
        MoveTo(sbLeft + 1, upTop); LineTo(sbRight - 2, upTop);
        MoveTo(sbLeft + 1, downTop); LineTo(sbRight - 2, downTop);
        RGBForeColor(PlatinumShadeLine);
        MoveTo(sbLeft + 1, top + 1); LineTo(sbRight - 2, top + 1);

        // Arrow glyphs: 7×4 triangles centered in each cell — gray when the list
        // fits (disabled control), black when scrolling is live.
        RGBForeColor(active ? PlatinumArrowActive : PlatinumArrowDisabled);
        int cx = (sbLeft + sbRight) / 2;
        void Triangle(int cellTop, bool pointUp)
        {
            for (int r = 0; r < 4; r++)
            {
                int w = pointUp ? (2 * r + 1) : (7 - 2 * r);
                int y = cellTop + 5 + r;
                MoveTo(cx - w / 2 - 1, y); LineTo(cx + w / 2, y);
            }
        }
        Triangle(upTop + 1, pointUp: true);
        Triangle(downTop + 1, pointUp: false);

        // Thumb (active only): square platinum-gray box positioned proportionally
        // in the track above the arrow cells.
        if (active)
        {
            int trackTop = top + 2, trackBottom = upTop - 1;
            int trackH = trackBottom - trackTop;
            int thumbH = Math.Max(10, trackH * l.VisibleRows / Math.Max(1, l.RowCount));
            if (thumbH > trackH) thumbH = trackH;
            int thumbTop = trackTop + (l.MaxTopRow > 0 ? (trackH - thumbH) * l.TopRow / l.MaxTopRow : 0);
            var thumb = new short[] { (short)thumbTop, (short)(sbLeft + 1),
                                      (short)(thumbTop + thumbH), (short)(sbRight - 1) };
            RGBForeColor(PlatinumThumbFill); PaintRect(thumb);
            RGBForeColor(PlatinumFrame); FrameRect(thumb);
        }
    }

    // Scrollbar hit-test shared by LClick: which scroll action does a click at
    // window-local (h, v) in the scrollbar strip request? Returns the TopRow
    // delta (±1 line for the stacked bottom arrows, ±page for the track), or 0
    // for none. The strip is [rView.right, rView.right+16) — outside the cells.
    internal static int ListScrollBarHit(MacListRec l, int h, int v)
    {
        int sbLeft = l.ViewRect[3];
        if (h < sbLeft || h >= sbLeft + MacListRec.ScrollBarWidth + 1) return 0;
        if (!l.NeedsScroll) return 0;                         // disabled control — inert
        int bottom = l.ViewRect[2] + 1;
        int downTop = bottom - 1 - ScrollArrowCellH;
        int upTop = downTop - 1 - ScrollArrowCellH;
        if (v >= downTop) return 1;                           // down arrow — line down
        if (v >= upTop) return -1;                            // up arrow — line up
        // Track click: page up/down relative to the thumb position.
        int top = l.ViewRect[0] - 1;
        int trackTop = top + 2, trackBottom = upTop - 1;
        int trackH = trackBottom - trackTop;
        int thumbH = Math.Max(10, trackH * l.VisibleRows / Math.Max(1, l.RowCount));
        int thumbTop = trackTop + (l.MaxTopRow > 0 ? (trackH - thumbH) * l.TopRow / Math.Max(1, l.MaxTopRow) : 0);
        return v < thumbTop + thumbH / 2 ? -l.VisibleRows : l.VisibleRows;
    }
}
