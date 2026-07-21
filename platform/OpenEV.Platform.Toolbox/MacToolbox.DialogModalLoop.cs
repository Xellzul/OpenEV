using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

public static partial class MacToolbox
{
    // ModalDialog (the modal loop). Runs on the title thread. Blocks until an ENABLED item is clicked,
    // then returns its 1-based number in itemHitOut.
    //
    // The dialog is STATIC between user actions, and the screen render
    // target preserves contents, so the dialog is drawn ONCE per call (not
    // every frame). Re-enqueuing a full redraw each frame raced the host's
    // draw drain (a drain catching the background fill before the
    // keys-grid CopyBits showed a white flash) — that was the flicker. We
    // redraw only on entry and again whenever the filter captures a key
    // (which advances the armed slot and changes a binding). (Ship-render
    // flicker from the same drain-race class is now fixed structurally via
    // MacToolbox.BeginDrawBatch/EndDrawBatch instead of avoiding re-enqueues.)
    public static void ModalDialog(int filterProc, ref short itemHitOut)
    {
        var rec = _dialogStack.Count > 0 ? _dialogStack.Peek() : null;
        if (rec is null) { itemHitOut = 1; return; }
        var filter = ResolveModalFilter(filterProc);

        // Blocking mid-batch would strand every draw since BeginDrawBatch un-drainable
        // (the dialog runs invisibly behind the frozen scene). GetNewDialog already
        // suspends for the whole window session — this inner save/restore (a no-op
        // then, saving 0) keeps the loop safe standalone too.
        int savedBatchDepth = SuspendDrawBatchForModal();
        try { RunModalLoop(rec, filter, ref itemHitOut); }
        finally { ResumeDrawBatchAfterModal(savedBatchDepth); }
    }

    // The blocking poll loop ModalDialog wraps in the draw-batch suspend above.
    private static void RunModalLoop(DlgRecord rec, Func<int, MacEvent, int>? filter, ref short itemHitOut)
    {
        // Require a fresh press: ignore the button if it's already held on entry.
        bool prevDown = FrameButtonDownBridge;
        // A control being tracked (pressed, not yet released). Buttons AND
        // checkboxes fire on mouse-UP inside the control (Mac TrackControl) —
        // for buttons additionally so the press is already released when
        // ModalDialog returns and the dialog closes, otherwise the still-held
        // press leaks to the screen underneath as a spurious click the next
        // frame. UserItems still fire on mouse-DOWN (the Game Speed slider,
        // item 4, needs the press to start its own StillDown drag loop).
        int trackingControl = 0;
        bool trackingInside = false;   // pointer currently over the pressed control (hilite shown)
        // An edit-field press being tracked: TEClick's StillDown drag extends
        // the selection from its anchor until the button releases.
        DlgItem? trackingEdit = null;
        // Word-mode drag (after a double click): the anchor word's bounds — the
        // drag snaps to whole words and never shrinks inside the anchor word.
        bool trackingWordDrag = false;
        int trackingWordStart = 0, trackingWordEnd = 0;
        // TEClick double-click detection (GetDblTime): last press's item + time.
        int lastEditClickItem = 0;
        long lastEditClickMs = 0;
        long lastCaretFlipMs = System.Environment.TickCount64;

        _trackingItemForTest = 0; _trackingInsideForTest = false;
        RedrawDialog(rec, filter);   // draw once; persists in the screen RT

        while (true)
        {
            if (UpdateEventsEnabled && _updateEvtPending)
            {
                _updateEvtPending = false;
                RedrawDialog(rec, filter, fill: false);
            }

            // Poll-based key capture WITHOUT redrawing: send the filter a
            // null event (its key path runs on what 0/3/6, its redraw only
            // on what 6). If it consumed a key, redraw to reflect the new
            // binding + advanced slot.
            if (filter is not null)
            {
                var nev = MakeEvent((short)MacEventType.NullEvent);
                if (filter(rec.Handle, nev) != 0)                 // what = nullEvent
                {
                    // A POSITIVE itemHit from a null event = the filter fired an item on its
                    // own, on idle: the bar's mash-timer bar-person encounter (item 6), or a
                    // held keymap shortcut in the shipyard/outfit (Map/Info/Missions). Return
                    // it. itemHit <= 0 = capture-only — the prefs keybind grid returns 1 with
                    // itemHit 0 to request a redraw of the just-captured binding.
                    if (nev.ItemHit > 0) { itemHitOut = nev.ItemHit; return; }
                    RedrawDialog(rec, filter, fill: false);       // no bg fill → no flicker
                }
            }

            // Keyboard input. Faithful Mac ModalDialog: the filterProc sees every
            // keyDown FIRST (like the mouseDown dispatch above), which drives each
            // dialog's own Return/Enter → item mapping and letter-key shortcuts
            // (spaceport r/f/c/e/t/o/s/n/b, galaxy map c/-/+/tab/arrows, cmd-period
            // cancel, …) — all ported from the decompile and living entirely in the
            // filter (the spaceport dialog never calls SetDialogDefaultItem). Only a
            // POSITIVE itemHit fires, matching the mouseDown filter-first contract;
            // anything else falls through to the generic handling below.
            //
            // Return/Enter/Enter-key then fires the dialog's default item when the
            // filter declines — the Mac SetDialogDefaultItem contract, for EVERY
            // modal, not just ones with a focused edit field. Drain chars
            // unconditionally; route the remaining printable/backspace chars into the
            // focused edit field only when one exists.
            {
                var typed = DrainTypedKeys();
                if (typed.Count > 0)
                {
                    var ed = rec.FocusedEdit != 0 ? rec.Items.Find(i => i.ItemNo == rec.FocusedEdit) : null;
                    bool changed = false;
                    foreach (var key in typed)
                    {
                        char ch = key.Ch;
                        if (filter is not null)
                        {
                            var kevt = MakeEvent((short)MacEventType.KeyDown);
                            kevt.Message = (byte)ch;
                            kevt.Modifiers = key.Mods;   // per-key capture, not the frame-sampled snapshot MakeEvent defaults to
                            if (filter(rec.Handle, kevt) != 0 && kevt.ItemHit > 0)
                            { itemHitOut = kevt.ItemHit; return; }
                        }
                        if (ch == '\r' || ch == '\n' || ch == (char)3)
                        {
                            if (rec.DefaultItem != 0 &&
                                rec.Items.Find(i => i.ItemNo == rec.DefaultItem)?.CtrlHilite != 255)
                            {
                                FlashDefaultButton(rec);   // the std-filter ~8-tick hilite flash
                                itemHitOut = (short)rec.DefaultItem; return;
                            }
                        }
                        else if (ed is not null)
                        {
                            changed |= (key.Mods & MacCmdKeyBit) != 0
                                ? ApplyEditCommand(ed, ch)                                   // Cmd-X/C/V/A (Ctrl on the host)
                                : ApplyEditKey(rec, ed, ch, (key.Mods & MacShiftKeyBit) != 0);
                            ed = rec.FocusedEdit != 0 ? rec.Items.Find(i => i.ItemNo == rec.FocusedEdit) : null;   // Tab may have moved focus
                        }
                    }
                    if (changed)
                    {
                        rec.CaretOn = true;                                // an edit shows the caret immediately (TE resets the blink)
                        lastCaretFlipMs = System.Environment.TickCount64;
                        RepaintEditItem(rec, rec.FocusedEdit);             // item-level repaint — no full-dialog flicker
                    }
                }
            }

            bool down = FrameButtonDownBridge;
            var p = FrameMouseBridge;
            if (down && !prevDown)
            {
                // Faithful Mac ModalDialog: the filterProc sees the mouseDown FIRST,
                // and a nonzero return means the event is CONSUMED — ModalDialog
                // returns the filter's itemHit to the caller AS-IS, including an
                // explicitly-set -1. EVO's in-game filters (TrackBbsButtonHit /
                // TrackSingleButtonClick / TrackTwoButtonDialog…) block through the
                // whole press themselves and hand back -1 for "clicked, tracked,
                // released OUTSIDE every button" (or after a grid/list selection);
                // the caller loops match no item on -1 and re-enter ModalDialog. Only
                // itemHit == 0 — a filter that consumed the event but never wrote the
                // out-param (a wrapper that doesn't propagate itemHit, e.g. the galaxy
                // map's) — falls through to the built-in hit-test below, so un-updated
                // dialogs keep working; a filter returning 0 falls through as on the
                // Mac. The filter's mouseDown side effects (grid/list selection) run
                // either way.
                if (filter is not null)
                {
                    var mev = MakeEvent((short)MacEventType.MouseDown);   // at the current (held) mouse point
                    if (filter(rec.Handle, mev) != 0 && mev.ItemHit != 0) { itemHitOut = mev.ItemHit; return; }
                }
                int hit = HitTestDialog(rec, p.H, p.V);
                if (hit != 0)
                {
                    var it = rec.Items.Find(i => i.ItemNo == hit);
                    bool trackable = it is not null &&
                        (it.Kind == DitlItemKind.Button || it.Kind == DitlItemKind.Checkbox);
                    if (trackable && it!.CtrlHilite == 255)
                    {
                        // Disabled control (HiliteControl 255 — the nag's "Not Yet"
                        // hold-off): the click lands in the dialog but does nothing.
                    }
                    else if (trackable)
                    {
                        trackingControl = hit;            // fire on release (Mac TrackControl)
                        trackingInside = true;
                        _trackingItemForTest = hit; _trackingInsideForTest = true;
                        DrawControlTrackState(rec, hit, pressed: true);
                    }
                    else if (it is not null && it.Kind == DitlItemKind.EditableText)
                    {
                        // TEClick: take focus and place the caret at the glyph the
                        // click lands on; shift-click extends the selection, a
                        // double click selects the word; drag below extends until
                        // release. Never reported as a hit (Mac editText items
                        // are handled inside ModalDialog).
                        int prevFocus = rec.FocusedEdit;
                        rec.FocusedEdit = hit;
                        long nowMs = System.Environment.TickCount64;
                        bool dbl = lastEditClickItem == hit && nowMs - lastEditClickMs <= DoubleClickMs;
                        lastEditClickItem = hit; lastEditClickMs = nowMs;
                        int off = EditOffsetFromX(it, p.H);
                        trackingWordDrag = false;
                        if ((FrameModifiers & MacShiftKeyBit) != 0)
                        {
                            // Shift-click: keep the selection end farther from the
                            // click as the anchor and extend to the click.
                            int anchor = System.Math.Abs(off - it.SelStart) >= System.Math.Abs(off - it.SelEnd)
                                ? it.SelStart : it.SelEnd;
                            SetEditSelection(it, anchor, off);
                        }
                        else if (dbl)
                        {
                            // Double click: select the word and switch the drag into
                            // WORD mode (Mac TE) — holding keeps the whole word, and
                            // dragging extends word-by-word from this anchor word.
                            SelectWordAt(it, off);
                            trackingWordStart = it.SelStart;
                            trackingWordEnd = it.SelEnd;
                            trackingWordDrag = true;
                        }
                        else SetEditSelection(it, off, off);
                        EnsureEditVisible(it, off);
                        rec.CaretOn = true;
                        lastCaretFlipMs = nowMs;
                        trackingEdit = it;
                        if (prevFocus != hit && prevFocus != 0) RepaintEditItem(rec, prevFocus);   // hide the old field's caret/selection
                        RepaintEditItem(rec, hit);
                    }
                    else { itemHitOut = (short)hit; return; }   // UserItems etc.: fire on press
                }
            }
            else if (down && trackingControl != 0)
            {
                // Held: hilite tracks the mouse in/out of the pressed control
                // (Mac TrackControl un-hilites when the pointer drags off it).
                bool inside = HitTestDialog(rec, p.H, p.V) == trackingControl;
                if (inside != trackingInside)
                {
                    trackingInside = inside;
                    _trackingInsideForTest = inside;
                    DrawControlTrackState(rec, trackingControl, pressed: inside);
                }
            }
            else if (down && trackingEdit is not null)
            {
                // TEClick StillDown drag: extend the selection to the char under
                // the pointer, auto-scrolling to keep the moving end in view.
                int off = EditOffsetFromX(trackingEdit, p.H);
                if (trackingWordDrag)
                {
                    // Word mode (after a double click): the anchor WORD always stays
                    // selected; the selection snaps outward to whole-word boundaries
                    // under the pointer. Holding still keeps the full word.
                    var (ws, we) = WordRangeAt(trackingEdit.EditText, off);
                    int newStart = System.Math.Min(trackingWordStart, ws);
                    int newEnd = System.Math.Max(trackingWordEnd, we);
                    if (newStart != trackingEdit.SelStart || newEnd != trackingEdit.SelEnd)
                    {
                        trackingEdit.SelStart = newStart;
                        trackingEdit.SelEnd = newEnd;
                        trackingEdit.SelAnchor = newStart == trackingWordStart ? trackingWordStart : trackingWordEnd;
                        EnsureEditVisible(trackingEdit, off);
                        RepaintEditItem(rec, trackingEdit.ItemNo);
                    }
                }
                else
                {
                    int moving = trackingEdit.SelAnchor == trackingEdit.SelStart
                        ? trackingEdit.SelEnd : trackingEdit.SelStart;
                    if (off != moving)
                    {
                        SetEditSelection(trackingEdit, trackingEdit.SelAnchor, off);
                        EnsureEditVisible(trackingEdit, off);
                        RepaintEditItem(rec, trackingEdit.ItemNo);
                    }
                }
            }
            else if (!down && prevDown)
            {
                if (trackingControl != 0)
                {
                    // Released: un-hilite (the dialog may stay open — an in-game Buy
                    // button must not stick inverted), then fire only if still inside.
                    if (trackingInside) DrawControlTrackState(rec, trackingControl, pressed: false);
                    _trackingItemForTest = 0; _trackingInsideForTest = false;
                    if (HitTestDialog(rec, p.H, p.V) == trackingControl)
                    {
                        itemHitOut = (short)trackingControl;
                        return;
                    }
                    trackingControl = 0;
                }
                trackingEdit = null;   // TEClick drag ends
            }
            prevDown = down;

            // TE caret blink (GetCaretTime): flip the focused field's caret
            // phase and repaint just that item — only for a collapsed selection
            // (a range hilite has no caret) and not mid-drag.
            if (rec.FocusedEdit != 0 && trackingEdit is null)
            {
                long nowMs = System.Environment.TickCount64;
                if (nowMs - lastCaretFlipMs >= CaretBlinkMs)
                {
                    lastCaretFlipMs = nowMs;
                    rec.CaretOn = !rec.CaretOn;
                    var ed = rec.Items.Find(i => i.ItemNo == rec.FocusedEdit);
                    if (ed is not null && ed.SelStart == ed.SelEnd) RepaintEditItem(rec, rec.FocusedEdit);
                }
            }

            // Bail out if the dialog was disposed underneath us (defensive).
            if (!_dialogs.ContainsKey(rec.Handle)) { itemHitOut = 1; return; }

            System.Threading.Thread.Sleep(20);
        }
    }

    // Full one-shot redraw: window background + (via the filter's updateEvt
    // path) the keybind grid + DrawDialog, or DrawDialog directly when there
    // is no filter.
    //
    // `fill` paints the white window background first. That's needed on the
    // INITIAL redraw, but on a keybind-capture repaint only the grid changed —
    // its CopyBits overwrites its whole region and DrawDialog repaints the
    // static items over themselves, so filling white first just produces a
    // 1-frame white flash if the host's draw drain catches it before the grid
    // CopyBits (the "menu flickers when I change a keybind" report). Skip it.
    private static void RedrawDialog(DlgRecord rec, Func<int, MacEvent, int>? filter, bool fill = true)
    {
        SetPort(rec.Handle);
        if (fill) FillDialogBackground(rec);
        // Draw the standard DITL items (buttons / static text / PICT / edit fields). Faithful
        // Mac behaviour: ModalDialog always draws the dialog, with the filterProc supplementary.
        // A scene dialog's filter (spaceport/shipyard/…) ADDITIONALLY runs its own full custom
        // redraw on the UpdateEvt below, which dominates (DrawDialog is idempotent — same items
        // at the same rects). A behaviour-only filter (the shareware nag's DefaultDialogFilter,
        // alerts) draws NOTHING on UpdateEvt, so without this unconditional DrawDialog its
        // buttons/PICT never painted and the window showed blank.
        DrawDialog(rec.Handle);
        if (filter is not null)
        {
            filter(rec.Handle, MakeEvent((short)MacEventType.UpdateEvt));   // scene grid / custom art
        }
        // Redraw any custom UserItem art (e.g. the Game Speed slider) on top of
        // the freshly-filled background so it isn't erased.
        if (_dialogUserDraw.TryGetValue(rec.DlogId, out var userDraw))
            userDraw();
        // Replay registered styled-text overlays (the shareware nag's message body) on top —
        // word-wrapped into the item rect, black on the erased background. With 'styl' runs
        // the styled renderer honours per-run font/size/face + TE line metrics; without, the
        // plain TETextBox draws in the current font (the pre-styl degraded path).
        if (_dialogStyledText.TryGetValue(rec.DlogId, out var styled))
            foreach (var (r, t, runs) in styled)
            {
                if (runs is not null) DrawStyledTextBox(t, r, runs);
                else TETextBox(t, r, 0);
            }
    }

    /// First ENABLED item under (x,y), searched top-of-list-last so later
    /// (higher z-order) items win. Mac ModalDialog only reports enabled items.
    private static int HitTestDialog(DlgRecord rec, int x, int y)
    {
        int found = 0;
        foreach (var it in rec.Items)
            if (it.Enabled && it.Contains(x, y)) found = it.ItemNo;
        return found;
    }

    // Test-only diagnostic: for an OPEN dialog, report item `itemNo`'s DITL kind
    // (DitlItemKind as int) + enabled flag, and whether a left-click at the item's
    // centre hit-tests BACK to it. A button that "doesn't respond to clicking" is
    // one where this returns a different item (usually 0) — because it's parsed
    // Enabled=false or as a non-clickable kind, so HitTestDialog skips it. The
    // EnqueueSyntheticHit path injects the item number directly and never exercises
    // this, which is how a dead spaceport button slipped past the existing tests.
    public static (int kind, bool enabled, bool present, int hitAtCentre) ProbeDialogItemForTest(int handle, int itemNo)
    {
        var rec = FindDialog(handle);
        var it = rec?.Items.Find(i => i.ItemNo == itemNo);
        if (rec is null || it is null) return (0, false, false, 0);
        int cx = (it.Left + it.Right) / 2;
        int cy = (it.Top + it.Bottom) / 2;
        return ((int)it.Kind, it.Enabled, true, HitTestDialog(rec, cx, cy));
    }

}
