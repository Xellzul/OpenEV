using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

public static partial class MacToolbox
{
    // Modal-loop control tracking mirrored for tests: which item is press-
    // tracked right now and whether the pointer is inside it (hilite shown).
    private static volatile int _trackingItemForTest;
    private static volatile bool _trackingInsideForTest;
    public static (int item, bool inside) GetModalTrackStateForTest()
        => (_trackingItemForTest, _trackingInsideForTest);

    // 1px stroke around a rect (top/bottom/left/right) — the frame primitive the
    // dialog draw helpers share. Keep the edge order stable so a track-state repaint
    // overdraws the previous frame identically.
    private static void StrokeRect(Canvas c, RectI r, RgbaColor color)
    {
        c.FillRect(new RectI(r.X, r.Y, r.Width, 1), color);
        c.FillRect(new RectI(r.X, r.Bottom - 1, r.Width, 1), color);
        c.FillRect(new RectI(r.X, r.Y, 1, r.Height), color);
        c.FillRect(new RectI(r.Right - 1, r.Y, 1, r.Height), color);
    }

    // Mac editable-text dialog item: the DITL rect IS the TE destRect; the
    // Dialog Manager frames it 1px at InsetRect(-3) (SheepShaver ground truth
    // 090921: DLOG 3100 item 5 rect left/right 326/526, frame drawn at
    // 323/528). The interior shows the live TE view — scrolled text, the
    // System 7 hilite-color selection band, or the blinking caret. The
    // interior is painted through a temp image so overflowing/scrolled text
    // clips to the box (Canvas clips to its target's bounds — the temp IS the
    // clip rect).
    private static void DrawDlgEditText(DlgItem it, bool focused, bool caretOn)
    {
        int left = it.Left, top = it.Top, w = it.Right - it.Left, h = it.Bottom - it.Top;
        if (w <= 0 || h <= 0) return;
        string text = it.EditText;
        int selStart = System.Math.Clamp(it.SelStart, 0, text.Length);
        int selEnd = System.Math.Clamp(it.SelEnd, selStart, text.Length);
        int scrollX = System.Math.Max(0, it.ScrollX);
        var fontSys = SystemFont ?? Font;   // dialog TextEdit fields are system-font (Chicago 12) too
        EnqueueDraw(c =>
        {
            // 1px frame 3px outside the destRect (the Dialog Manager's
            // FrameRect(InsetRect(dest, -3, -3)) editText chrome).
            int fL = left - 3, fT = top - 3, fW = w + 6, fH = h + 6;
            StrokeRect(c, new RectI(fL, fT, fW, fH), RgbaColor.Black);
            int iw = fW - 2, ih = fH - 2;      // frame interior = destRect + a 2px white ring
            var inner = new Rgba8Image(iw, ih);
            var ic = new Canvas(inner);
            ic.Clear(RgbaColor.White);
            const int dx = 2, dy = 2;          // destRect's offset inside the frame interior
            int penX = dx + EditPenInset - scrollX;
            if (focused && selEnd > selStart && fontSys is not null)
            {
                // TE selection hilite: the System 7 highlight color band BEHIND
                // black text (ground truth 090921: band #DADAFF = raw #CCCCFF
                // through the Mac 1.8/2.61 DAC ramp — not a 1-bit inversion).
                // A selection touching the text ends hilites to the destRect
                // edge (the classic full-box select-all look).
                int x0 = selStart == 0 ? dx : penX + fontSys.MeasureWidth(text[..selStart], DialogEditTextSize);
                int x1 = selEnd == text.Length ? dx + w : penX + fontSys.MeasureWidth(text[..selEnd], DialogEditTextSize);
                ic.FillRect(new RectI(x0, dy, x1 - x0, h), TeHiliteColor);
            }
            if (fontSys is not null && text.Length > 0)
            {
                int py = dy + System.Math.Max(0, (h - fontSys.LineHeight(DialogEditTextSize)) / 2);
                fontSys.DrawText(ic, text, penX, py, RgbaColor.Black, DialogEditTextSize);
            }
            if (focused && selEnd <= selStart && caretOn)
            {
                int caretX = penX + (fontSys is not null
                    ? fontSys.MeasureWidth(text[..selStart], DialogEditTextSize) : 0);
                if (caretX < dx) caretX = dx;
                if (caretX > dx + w - 1) caretX = dx + w - 1;
                ic.FillRect(new RectI(caretX, dy + 1, 1, h - 2), RgbaColor.Black);
            }
            c.Blit(inner, new RectI(fL + 1, fT + 1, iw, ih), RgbaColor.White);
        });
    }

    // Background fill + WDEF window frame.
    // Mac dialogs erase their content to the window background (white) and the
    // WDEF paints the window frame OUTSIDE the content rect. EVO's DLOGs use
    // two variants (DLOG procID):
    //   1 = dBoxProc  — the classic modal-dialog chrome: an 8px border of
    //       concentric 1px rings (out→in: black, 3D hilite, gray band, 3D
    //       hilite, dark line) around a 3px white margin. Geometry + the
    //       System 7 default "bluish" window colors per the original WDEF
    //       (cross-checked against Executor's reimplementation,
    //       windDocdef.cpp draw_dialog_box + windColor.cpp default ctab).
    //   2 = plainDBox — a bare 1px black frame (the in-game spaceport family).
    // The hilite rings run through Gamma.Correct like every directly-painted
    // Mac UI color (see RGBForeColor).
    private static readonly RgbaColor DBoxLight = Gamma.Correct(new RgbaColor(0xcc, 0xcc, 0xff));  // wDialogLight #CCCCFF
    private static readonly RgbaColor DBoxHalf  = Gamma.Correct(new RgbaColor(0x6d, 0x6d, 0x88));  // dialog_light/dark averaged (w=0x8888)
    private static readonly RgbaColor DBoxGray  = Gamma.Correct(new RgbaColor(0xbb, 0xbb, 0xbb));  // middle band #BBBBBB
    private static void FillDialogBackground(DlgRecord rec)
    {
        var bg = new RectI(rec.WinLeft, rec.WinTop,
                           rec.WinRight - rec.WinLeft, rec.WinBottom - rec.WinTop);
        bool dbox = rec.ProcId == 1;   // dBoxProc → 8px modal chrome; else plainDBox 1px frame
        EnqueueDraw(c =>
        {
            c.FillRect(bg, RgbaColor.White);

            // The rect `d` pixels outside the content rect (StrokeRect draws its 1px edge).
            RectI Ring(int d) => new RectI(bg.X - d, bg.Y - d, bg.Width + 2 * d, bg.Height + 2 * d);

            if (!dbox)
            {
                StrokeRect(c, Ring(1), RgbaColor.Black);   // plainDBox
                return;
            }

            // dBoxProc, rings inside-out: 3px white margin (d1-d3), dark line (d4),
            // hilite (d5), gray band (d6), hilite (d7), outer black frame (d8).
            var r3 = Ring(3);
            c.FillRect(new RectI(r3.X, r3.Y, r3.Width, 3), RgbaColor.White);
            c.FillRect(new RectI(r3.X, r3.Bottom - 3, r3.Width, 3), RgbaColor.White);
            c.FillRect(new RectI(r3.X, r3.Y, 3, r3.Height), RgbaColor.White);
            c.FillRect(new RectI(r3.Right - 3, r3.Y, 3, r3.Height), RgbaColor.White);
            StrokeRect(c, Ring(4), RgbaColor.Black);   // dialog_dark
            // d5 hilite: half on top/left; light on bottom/right, stopping 1px
            // short of the top-right / bottom-left corners (the WDEF's line ends).
            var r5 = Ring(5);
            c.FillRect(new RectI(r5.X, r5.Y, r5.Width, 1), DBoxHalf);
            c.FillRect(new RectI(r5.X, r5.Y, 1, r5.Height), DBoxHalf);
            c.FillRect(new RectI(r5.Right - 1, r5.Y + 1, 1, r5.Height - 1), DBoxLight);
            c.FillRect(new RectI(r5.X + 1, r5.Bottom - 1, r5.Width - 1, 1), DBoxLight);
            StrokeRect(c, Ring(6), DBoxGray);          // middle gray band
            // d7 hilite: light on top/left; half on bottom/right owning its corners.
            var r7 = Ring(7);
            c.FillRect(new RectI(r7.X, r7.Y, r7.Width, 1), DBoxLight);
            c.FillRect(new RectI(r7.X, r7.Y, 1, r7.Height), DBoxLight);
            c.FillRect(new RectI(r7.Right - 1, r7.Y, 1, r7.Height), DBoxHalf);
            c.FillRect(new RectI(r7.X, r7.Bottom - 1, r7.Width, 1), DBoxHalf);
            StrokeRect(c, Ring(8), RgbaColor.Black);   // outer frame
        });
    }

    private static void DrawDlgButton(DlgItem it, bool isDefault, bool pressed = false)
    {
        int w = it.Right - it.Left, h = it.Bottom - it.Top;
        if (w <= 0 || h <= 0) return;
        int left = it.Left, top = it.Top, right = it.Right, bottom = it.Bottom;
        string label = it.Text;
        var fontSys = SystemFont ?? Font;
        bool fauxBold = SystemFont is null;   // button titles are Chicago 12; double-draw only approximates it
        bool disabled = it.CtrlHilite == 255; // HiliteControl(255) — dimmed + inert (the nag's "Not Yet" hold-off)
        EnqueueDraw(c =>
        {
            StrokeRect(c, new RectI(left, top, w, h), RgbaColor.Black);   // 1px button frame
            // Mac TrackControl press hilite: the interior inside the frame inverts
            // (white→black), the title with it. Drawn white here whether pressed or
            // not so a track-state repaint fully overwrites the previous state.
            var bg = pressed ? RgbaColor.Black : RgbaColor.White;
            var fg = pressed ? RgbaColor.White : RgbaColor.Black;
            if (w > 2 && h > 2) c.FillRect(new RectI(left + 1, top + 1, w - 2, h - 2), bg);
            if (isDefault)
            {
                // Mac default-button ring: PenSize(3,3) FrameRoundRect(rect inset -4, 16, 16)
                // — same raster as the ported DrawDefaultButtonOutline (FUN_10041320), whose
                // one-shot draw at dialog-open is erased by this redraw's background fill.
                DrawRoundRectFrame(c, new RectI(left - 4, top - 4, w + 8, h + 8), 16, 16, RgbaColor.Black);
            }
            if (fontSys is not null && label.Length > 0)
            {
                // Centre the title on the face's real line box (the Chicago strike is
                // ascent 13/height 16 — a hardcoded 12 put descenders on the frame).
                int lh = fontSys.LineHeight(12);
                int szX = fontSys.MeasureWidth(label, 12);
                int px = left + (w - szX) / 2, py = top + System.Math.Max(0, (h - lh) / 2);
                fontSys.DrawText(c, label, px, py, fg, 12);
                if (fauxBold) fontSys.DrawText(c, label, px + 1, py, fg, 12);
            }
            if (disabled)
            {
                // Classic Mac gray-out: the whole button (frame + title) dimmed through
                // the 50% gray pattern — knock out every other pixel to the background.
                for (int y = top; y < bottom; y++)
                    for (int x = left + ((y ^ left) & 1); x < right; x += 2)
                        c.FillRect(new RectI(x, y, 1, 1), RgbaColor.White);
            }
        });
    }

    /// Repaint one control in its pressed/normal track state (the Mac
    /// TrackControl hilite ModalDialog shows while the mouse is held on it):
    /// push buttons invert their interior, checkboxes bolden their outline.
    private static void DrawControlTrackState(DlgRecord rec, int itemNo, bool pressed)
    {
        var it = rec.Items.Find(i => i.ItemNo == itemNo);
        if (it is null) return;
        if (it.Kind == DitlItemKind.Button)
        {
            SetPort(rec.Handle);
            DrawDlgButton(it, isDefault: it.ItemNo == rec.DefaultItem, pressed);
        }
        else if (it.Kind == DitlItemKind.Checkbox)
        {
            SetPort(rec.Handle);
            DrawDlgCheckbox(it, pressed);
        }
    }

    /// Control Manager HiliteControl on a GetDialogItem handle: 255 disables the
    /// control (dimmed, ignores clicks — the nag's "Not Yet" hold-off), 0
    /// re-activates it, 1-253 shows the pressed part (the filter's Return flash).
    /// Repaints the button in place.
    public static void HiliteControl(int controlHandle, int hiliteState)
    {
        if (!_itemByHandle.TryGetValue(controlHandle, out var it)) return;
        byte state = (byte)hiliteState;
        if (it.CtrlHilite == state) return;
        it.CtrlHilite = state;
        if (it.Kind != DitlItemKind.Button) return;
        foreach (var rec in _dialogs.Values)
            if (rec.Items.Contains(it))
            {
                SetPort(rec.Handle);
                DrawDlgButton(it, isDefault: it.ItemNo == rec.DefaultItem, pressed: state is > 0 and < 254);
                break;
            }
    }

    /// Window Manager ShowWindow — flips the dialog window visible and republishes
    /// the compositor layers. Game DLOGs are created hidden (visible=0 in the
    /// resource, all but 3000/3100), so every dialog flow calls this explicitly;
    /// the shareware nag's ShowWindow lives at the END of ZoomInWindowAnimation —
    /// before this was real, the nag's HiliteControl(0xff) dim of "Not Yet"
    /// composited on screen before the zoom animation revealed the dialog.
    /// Non-dialog handles (the game window / GWorld ports) stay no-ops: the scene
    /// is not a compositor layer.
    public static void ShowWindow(int window)
    {
        if (_dialogs.TryGetValue(window, out var rec) && !rec.Visible)
        {
            rec.Visible = true;
            RebuildWindowLayers();
        }
    }

    /// Window Manager HideWindow — the visible-flag inverse of ShowWindow above
    /// (the nag/alert flows hide before DisposeDialog; the mission board hides
    /// while a sub-dialog runs).
    public static void HideWindow(int window)
    {
        if (_dialogs.TryGetValue(window, out var rec) && rec.Visible)
        {
            rec.Visible = false;
            RebuildWindowLayers();
        }
    }

    /// Window Manager SetWRefCon — per-window application long. The nag stores its
    /// "Not Yet" re-enable deadline (escalation*300 + TickCount) here.
    public static void SetWRefCon(int window, int value)
    {
        var rec = FindDialog(window);
        if (rec is not null) rec.RefCon = value;
    }

    /// GetWRefCon — the decompile dropped the window arg at the call sites (arg-less glue),
    /// and the sole consumer (DefaultDialogFilter) runs inside the FRONT dialog's
    /// modal loop, so read the frontmost window's refCon.
    public static int GetWRefCon(params object?[] _)
        => _dialogStack.Count > 0 ? _dialogStack.Peek().RefCon : 0;

    /// The Mac standard-filter default-button flash: Return/Enter hilites the
    /// default button for ~8 ticks before the item fires, so a keyboard OK
    /// gives the same visual acknowledgement as a click.
    private static void FlashDefaultButton(DlgRecord rec)
    {
        var it = rec.Items.Find(i => i.ItemNo == rec.DefaultItem);
        if (it is null || it.Kind != DitlItemKind.Button) return;
        DrawControlTrackState(rec, rec.DefaultItem, pressed: true);
        System.Threading.Thread.Sleep(130);   // ≈ 8 ticks
        DrawControlTrackState(rec, rec.DefaultItem, pressed: false);
    }

    private static void DrawDlgCheckbox(DlgItem it, bool pressed = false)
    {
        int top = it.Top, left = it.Left, h = it.Bottom - it.Top;
        bool on = it.CtrlValue != 0;
        string label = it.Text;
        var fontSys = SystemFont ?? Font;
        bool fauxBold = SystemFont is null;   // checkbox titles are Chicago 12; double-draw only approximates it
        // 12x12 box, vertically centred in the item rect.
        int boxSize = 12;
        int boxTop = top + (h - boxSize) / 2;
        EnqueueDraw(c =>
        {
            var box = new RectI(left, boxTop, boxSize, boxSize);
            // Erase the box interior so a track-state repaint fully overwrites
            // the previous pressed/normal ring.
            c.FillRect(box, RgbaColor.White);
            StrokeRect(c, box, RgbaColor.Black);
            if (pressed)
            {
                // Mac TrackControl checkbox hilite: the standard CDEF thickens
                // the outline to 2px while the mouse is held inside (buttons
                // invert; checkboxes/radios bolden — IM: Toolbox Essentials).
                StrokeRect(c, new RectI(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), RgbaColor.Black);
            }
            if (on)
            {
                // Mac check = an X drawn corner to corner.
                for (int i = 1; i < boxSize - 1; i++)
                {
                    c.FillRect(new RectI(box.X + i, box.Y + i, 1, 1), RgbaColor.Black);
                    c.FillRect(new RectI(box.Right - 1 - i, box.Y + i, 1, 1), RgbaColor.Black);
                }
            }
            if (fontSys is not null && label.Length > 0)
            {
                int px = box.Right + 5, py = boxTop - 1;
                fontSys.DrawText(c, label, px, py, RgbaColor.Black, 12);
                if (fauxBold) fontSys.DrawText(c, label, px + 1, py, RgbaColor.Black, 12);
            }
        });
    }

    private static void DrawDlgPicture(DlgItem it)
    {
        if (it.ResId == 0) return;
        var rect = new RectI(it.Left, it.Top, it.Right - it.Left, it.Bottom - it.Top);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        int resId = it.ResId;
        EnqueueDraw(c =>
        {
            var tex = PictResolver?.Invoke(resId);
            if (tex is not null) c.Blit(tex, rect, RgbaColor.White);
        });
    }

    // Mac DITL statText is drawn via TETextBox: WORD-WRAPPED inside the item rect,
    // TOP-ALIGNED (TextEdit's first baseline sits at destRect.top + ascent — no
    // vertical centring), in the dialog's default System font, Chicago 12 →
    // SystemFont. (The filter-drawn alert redraw procs FUN_1003e83c/FUN_1003ec54
    // instead set TextFont(3)/TextSize(9) — Geneva, unaffected here.) The game
    // previously drew it as a single Geneva-12 line that ran long prompts (the
    // New-Pilot christen ship name) off the dialog edge. Wrap to the item width
    // on spaces (honouring explicit \r/\n breaks).
    private const int DialogStaticTextSize = 12;  // Chicago 12 — the no-filter DrawDialog statText size (the 9pt in FUN_1003e83c/FUN_1003ec54 is only for the filter-drawn alerts)
    private static void DrawDlgStaticText(string s, DlgItem it, RgbaColor color, DlgRecord rec)
    {
        var fontSys = SystemFont ?? Font;
        bool fauxBold = SystemFont is null;   // no Chicago face → fake its weight with the +1px double-draw
        if (fontSys is null || s.Length == 0) return;
        // Mac TETextBox wraps in the destRect VERBATIM — no horizontal inset (a
        // previous invented ±2px inset wrapped the 3002 alert a line early).
        int left = it.Left;
        int top = it.Top;
        int maxWidth = it.Right - it.Left;
        if (maxWidth <= 0 || it.Bottom - it.Top <= 0) return;
        // Mac item drawing clips to the window's port. Some DITL statText rects
        // overrun the window bounds (DLOG 3100 item 3 reaches 76px past the
        // right edge) — the WDEF frame hid the overrun on the Mac; unclipped
        // here, the TETextBox erase painted a white bar across the frame. The
        // pixels are rendered into a temp image sized to the item∩window rect
        // (the Canvas clips to its target — the temp IS the port clip); the
        // wrap width stays the FULL destRect width, exactly TETextBox.
        int clipL = System.Math.Max(left, rec.WinLeft), clipT = System.Math.Max(top, rec.WinTop);
        int clipR = System.Math.Min(it.Right, rec.WinRight), clipB = System.Math.Min(it.Bottom, rec.WinBottom);
        if (clipR <= clipL || clipB <= clipT) return;
        var bk = _activeBackColor;
        EnqueueDraw(c =>
        {
            // Mac DrawDialog paints statText via TETextBox, which ERASES the item
            // rect to the current BackColor before drawing (same rule as
            // TETextBoxCore / DrawStyledTextBox). Without the erase, redrawing an
            // item whose text CHANGED overprints the old label — the prefs Sound
            // Volume readout showed two levels superimposed after each up/down click.
            var tmp = new Rgba8Image(clipR - clipL, clipB - clipT);
            var tc = new Canvas(tmp);
            tc.Clear(bk);
            int lineH = fontSys.LineHeight(DialogStaticTextSize);
            if (lineH < DialogStaticTextSize + 1) lineH = DialogStaticTextSize + 2;

            var lines = new System.Collections.Generic.List<string>();
            foreach (var para in s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var cur = new System.Text.StringBuilder();
                foreach (var w in para.Split(' '))
                {
                    if (cur.Length == 0) { cur.Append(w); continue; }
                    string trial = cur.ToString() + " " + w;
                    if (fontSys.MeasureWidth(trial, DialogStaticTextSize) <= maxWidth) { cur.Clear(); cur.Append(trial); }
                    else { lines.Add(cur.ToString()); cur.Clear(); cur.Append(w); }
                }
                lines.Add(cur.ToString());
            }

            int x = left - clipL;
            int y = top - clipT;
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    fontSys.DrawText(tc, line, x, y, color, DialogStaticTextSize);
                    if (fauxBold) fontSys.DrawText(tc, line, x + 1, y, color, DialogStaticTextSize);
                }
                y += lineH;
            }
            c.Blit(tmp, new RectI(clipL, clipT, tmp.Width, tmp.Height), RgbaColor.White);
        });
    }
}
