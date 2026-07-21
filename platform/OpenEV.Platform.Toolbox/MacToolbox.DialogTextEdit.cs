using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

public static partial class MacToolbox
{
    // Dialog TextEdit (the editText items' live TE behaviour).
    // The Mac Dialog Manager runs a real TextEdit record per editText item and
    // ModalDialog feeds it internally (TEClick/TEKey/TEIdle) — none of that
    // appears in the decompile because it was Toolbox-internal. This block is
    // that internal behaviour: selection model, caret, click/drag hit-testing,
    // keyboard editing, the System 7 std-filter Cmd-X/C/V/A equivalents
    // (Cmd → Ctrl per the port's keyboard rule), and horizontal auto-scroll.
    // The New Pilot name field (DLOG 3100 item 5) and the confirm-text alert
    // (DLOG 3001 item 5) are the two consumers.

    private const int DialogEditTextSize = 12;   // editText items draw in the system font (Chicago 12), like statText
    private const int CaretBlinkMs = 533;        // GetCaretTime default: 32 ticks
    private const int DoubleClickMs = 533;       // GetDblTime default: 32 ticks
    private const int EditPenInset = 3;          // TE pen starts 3px inside the box frame
    // System 7 default highlight color (#CCCCFF) for the TE selection band —
    // same raw constant family as the WDEF's wDialogLight; gamma-corrected like
    // every directly-painted Mac UI color.
    private static readonly RgbaColor TeHiliteColor = Gamma.Correct(new RgbaColor(0xcc, 0xcc, 0xff));

    private static int EditTextWidth(string s)
        => s.Length == 0 ? 0 : (SystemFont ?? Font)?.MeasureWidth(s, DialogEditTextSize) ?? 0;

    /// TEClick's char-offset hit test (glyph-midpoint rule: a click left of a
    /// glyph's midpoint puts the caret before it). `x` is global; ScrollX pans.
    private static int EditOffsetFromX(DlgItem it, int x)
    {
        string t = it.EditText;
        int rel = x - (it.Left + EditPenInset) + it.ScrollX;
        if (rel <= 0 || t.Length == 0) return 0;
        for (int i = 0; i < t.Length; i++)
        {
            int w0 = EditTextWidth(t[..i]);
            int w1 = EditTextWidth(t[..(i + 1)]);
            if (rel < (w0 + w1) / 2) return i;
        }
        return t.Length;
    }

    /// Set the selection from a fixed anchor and a free end (either order).
    private static void SetEditSelection(DlgItem it, int anchor, int off)
    {
        int len = it.EditText.Length;
        anchor = System.Math.Clamp(anchor, 0, len);
        off = System.Math.Clamp(off, 0, len);
        it.SelAnchor = anchor;
        it.SelStart = System.Math.Min(anchor, off);
        it.SelEnd = System.Math.Max(anchor, off);
    }

    /// TE word boundaries around `off`: the run of letters/digits there, or the
    /// single other char under it. (0,0) for empty text.
    private static (int start, int end) WordRangeAt(string t, int off)
    {
        if (t.Length == 0) return (0, 0);
        off = System.Math.Clamp(off, 0, t.Length);
        int at = System.Math.Min(off, t.Length - 1);
        int s = at, e = at + 1;
        if (char.IsLetterOrDigit(t[at]))
        {
            while (s > 0 && char.IsLetterOrDigit(t[s - 1])) s--;
            while (e < t.Length && char.IsLetterOrDigit(t[e])) e++;
        }
        return (s, e);
    }

    /// TE word select (double-click).
    private static void SelectWordAt(DlgItem it, int off)
    {
        var (s, e) = WordRangeAt(it.EditText, off);
        SetEditSelection(it, s, e);
    }

    /// TE auto-scroll: keep char-offset `off`'s pixel inside the visible box,
    /// never scrolling past the text's own end.
    private static void EnsureEditVisible(DlgItem it, int off)
    {
        int visibleW = (it.Right - it.Left) - 2 * EditPenInset;
        if (visibleW <= 0) return;
        int textW = EditTextWidth(it.EditText);
        int px = EditTextWidth(it.EditText[..System.Math.Clamp(off, 0, it.EditText.Length)]);
        int maxScroll = System.Math.Max(0, textW + 1 - visibleW);   // +1: room for the caret past the last glyph
        if (it.ScrollX > maxScroll) it.ScrollX = maxScroll;
        if (px - it.ScrollX < 0) it.ScrollX = px;
        else if (px - it.ScrollX > visibleW - 1) it.ScrollX = px - (visibleW - 1);
        if (it.ScrollX < 0) it.ScrollX = 0;
    }

    /// Delete the selected run; caret collapses to the cut point.
    private static void DeleteEditSelection(DlgItem it)
    {
        it.EditText = it.EditText.Remove(it.SelStart, it.SelEnd - it.SelStart);
        it.SelEnd = it.SelAnchor = it.SelStart;
        EnsureEditVisible(it, it.SelStart);
    }

    /// Repaint one edit item in place (selection/caret/scroll changes).
    private static void RepaintEditItem(DlgRecord rec, int itemNo)
    {
        var it = rec.Items.Find(i => i.ItemNo == itemNo);
        if (it is null || it.Kind != DitlItemKind.EditableText) return;
        SetPort(rec.Handle);
        DrawDlgEditText(it, focused: rec.FocusedEdit == itemNo, caretOn: rec.CaretOn);
    }

    /// TEKey on the focused edit field. Returns true when display state changed.
    private static bool ApplyEditKey(DlgRecord rec, DlgItem ed, char ch, bool shift)
    {
        string t = ed.EditText;
        switch (ch)
        {
            case '\b':                       // backspace: the selection, else the char before the caret
                if (ed.SelEnd > ed.SelStart) { DeleteEditSelection(ed); return true; }
                if (ed.SelStart > 0)
                {
                    ed.EditText = t.Remove(ed.SelStart - 1, 1);
                    ed.SelStart = ed.SelEnd = ed.SelAnchor = ed.SelStart - 1;
                    EnsureEditVisible(ed, ed.SelStart);
                    return true;
                }
                return false;
            case (char)127:                  // forward delete
                if (ed.SelEnd > ed.SelStart) { DeleteEditSelection(ed); return true; }
                if (ed.SelStart < t.Length)
                {
                    ed.EditText = t.Remove(ed.SelStart, 1);
                    EnsureEditVisible(ed, ed.SelStart);
                    return true;
                }
                return false;
            case (char)0x1c:                 // left arrow
            case (char)0x1d:                 // right arrow
            {
                bool leftward = ch == (char)0x1c;
                if (shift)
                {
                    // Extend: move the free (non-anchor) end.
                    int mv = ed.SelAnchor == ed.SelStart ? ed.SelEnd : ed.SelStart;
                    int to = mv + (leftward ? -1 : 1);
                    if (to < 0 || to > t.Length) return false;
                    SetEditSelection(ed, ed.SelAnchor, to);
                    EnsureEditVisible(ed, to);
                    return true;
                }
                // Collapse: TE lands the caret on the selection edge, else steps.
                int pos = ed.SelEnd > ed.SelStart
                    ? (leftward ? ed.SelStart : ed.SelEnd)
                    : System.Math.Clamp(ed.SelStart + (leftward ? -1 : 1), 0, t.Length);
                bool moved = pos != ed.SelStart || ed.SelEnd != ed.SelStart;
                ed.SelStart = ed.SelEnd = ed.SelAnchor = pos;
                EnsureEditVisible(ed, pos);
                return moved;
            }
            case (char)0x1e:                 // up/down: single-line fields, TE ignores
            case (char)0x1f:
                return false;
            case '\t':
            {
                // Dialog Manager Tab: focus the next editText item, select-all.
                var edits = rec.Items.FindAll(i => i.Kind == DitlItemKind.EditableText);
                if (edits.Count == 0) return false;
                int idx = edits.FindIndex(i => i.ItemNo == rec.FocusedEdit);
                var next = edits[(idx + 1 + edits.Count) % edits.Count];
                int prevFocus = rec.FocusedEdit;
                rec.FocusedEdit = next.ItemNo;
                SetEditSelection(next, 0, next.EditText.Length);
                EnsureEditVisible(next, 0);
                if (prevFocus != next.ItemNo) RepaintEditItem(rec, prevFocus);   // hide the old field's caret
                return true;
            }
            default:
                if (ch < ' ') return false;
                // Printable: replaces the selection (or inserts at the caret).
                ed.EditText = t.Remove(ed.SelStart, ed.SelEnd - ed.SelStart)
                               .Insert(ed.SelStart, ch.ToString());
                ed.SelStart = ed.SelEnd = ed.SelAnchor = ed.SelStart + 1;
                EnsureEditVisible(ed, ed.SelStart);
                return true;
        }
    }

    // Fallback scrap when the host clipboard hooks aren't wired (headless).
    private static string _internalScrap = "";
    private static string ScrapGet() => HostClipboardGet is not null ? (HostClipboardGet() ?? "") : _internalScrap;
    private static void ScrapSet(string s) { _internalScrap = s; HostClipboardSet?.Invoke(s); }
    /// Pasted text is squeezed to the same printable-ASCII set the hosts feed
    /// TextInput from (' '..'~') — a Str255 field never holds control chars.
    private static string SanitizeEditText(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s) if (c is >= ' ' and < (char)127) sb.Append(c);
        return sb.ToString();
    }

    /// The System 7 standard-filter keyboard edit equivalents on the focused
    /// field: Cmd-X/C/V plus select-all. Returns true when display changed.
    private static bool ApplyEditCommand(DlgItem ed, char ch)
    {
        switch (char.ToLowerInvariant(ch))
        {
            case 'a':
                SetEditSelection(ed, 0, ed.EditText.Length);
                EnsureEditVisible(ed, 0);
                return true;
            case 'c':
                if (ed.SelEnd > ed.SelStart) ScrapSet(ed.EditText[ed.SelStart..ed.SelEnd]);
                return false;
            case 'x':
                if (ed.SelEnd <= ed.SelStart) return false;
                ScrapSet(ed.EditText[ed.SelStart..ed.SelEnd]);
                DeleteEditSelection(ed);
                return true;
            case 'v':
            {
                string paste = SanitizeEditText(ScrapGet());
                if (paste.Length == 0) return false;
                ed.EditText = ed.EditText.Remove(ed.SelStart, ed.SelEnd - ed.SelStart)
                                         .Insert(ed.SelStart, paste);
                ed.SelStart = ed.SelEnd = ed.SelAnchor = ed.SelStart + paste.Length;
                EnsureEditVisible(ed, ed.SelStart);
                return true;
            }
            default:
                return false;
        }
    }

    /// Test-only: the live TE state of an edit item in an OPEN dialog.
    public static (string text, int selStart, int selEnd, int scrollX, bool focused)?
        GetDialogEditStateForTest(int handle, int itemNo)
    {
        var rec = FindDialog(handle);
        var it = rec?.Items.Find(i => i.ItemNo == itemNo);
        if (rec is null || it is null || it.Kind != DitlItemKind.EditableText) return null;
        return (it.EditText, it.SelStart, it.SelEnd, it.ScrollX, rec.FocusedEdit == itemNo);
    }
}
