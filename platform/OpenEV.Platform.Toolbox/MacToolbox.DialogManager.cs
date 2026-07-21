using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// Real (minimal) Mac Dialog Manager for the game.
//
// The ported transcriptions (PrefsDialogInit / GameSpeedDialog / the alert
// modals) drive their dialogs through the raw Toolbox traps — GetNewDialog,
// GetDialogItem, SetControlValue, ModalDialog, DrawDialog, DisposeDialog —
// exactly as the decompile emits them. This file makes those traps real so
// the dialog actually renders and dismisses, instead of the old no-op shims
// (which made ModalDialog spin / the dialog never appear).
//
// Model (faithful to the Mac, adapted to the game's immediate-mode draw queue):
//   * GetNewDialog loads the DLOG+DITL via a host-installed template
//     delegate (GetDialogTemplateImpl), centres the window on the main
//     screen, and builds a DialogRecord with every item positioned in
//     GLOBAL (screen) coords — the game has no per-port coordinate origin, all
//     draws are absolute, so item rects are stored already-offset.
//   * Dialog handles are OPAQUE ids in the 0x78000000 band — no
//     memory behind them (dialog 4-rules final core batch; the old 64-byte
//     EvoMemory window-record stub is gone). Consumers read the window rect
//     via GetDialogPortRect, the visRgn token via GetDialogVisRgn, and
//     CopyBits dst keys keep the numeric `dialog + 2` form (a pure
//     dictionary key, same convention as MacGrafPort.PixmapKey).
//   * Control items (checkboxes / CNTL): the handle GetDialogItem returns is
//     an opaque id; SetControlValue / GetControlValue route through the
//     managed DlgItem.CtrlValue and DrawDialog reads it back.
//   * ModalDialog runs the modal loop on the title thread: each frame it
//     paints the dialog background, runs the registered modal filter proc
//     (which redraws the keybind grid + calls DrawDialog), then polls the
//     mouse-down edge and hit-tests the items, returning the first ENABLED
//     item clicked (Mac ModalDialog only reports enabled items).

// Typed Mac EventRecord — the managed replacement for the raw 16-byte
// EventRecord scratch ModalDialog used to hand filters by address.
// Field layout mirrored from the Mac record the decompile reads:
//   +0 what(short)  +2 message(int)  +6 when(int)  +10 where.v/.h  +14 modifiers(short)
public sealed class MacEvent
{
    public short What;            // 0 nullEvent, 1 mouseDown, 3 keyDown, 5 autoKey, 6 updateEvt
    /// `What` as the typed Mac event-record kind — filters compare/assign via this.
    public MacEventType WhatType { get => (MacEventType)What; set => What = (short)value; }
    public int   Message;         // keyDown: charCode = Message & 0xff, keycode = (Message >> 8) & 0xff
    public int   When;            // TickCount at event time (real Mac WaitNextEvent value; the shipyard double-click filter reads it)
    public short WhereV, WhereH;  // mouse in global coords
    public short Modifiers;
    /// Filter-proc out param (the Mac filterProc's `short* itemHit`): the dialog item
    /// the filter fired. Wrappers write it from the `ref short itemHit` after Run;
    /// ModalDialog reads it when the filter returns nonzero. >0 = fire that item,
    /// <0 = consumed (no item), 0 = filter didn't set one.
    public short ItemHit;
    /// The packed Point an `EvoMemory.ReadInt(evt + 10)` used to produce (v high, h low).
    public int WherePacked => (WhereV << 16) | (WhereH & 0xffff);
}

public static partial class MacToolbox
{
    // DITL item kind codes (the DITL type byte's low 7 bits).
    public enum DitlItemKind : byte
    {
        UserItem     = 0,
        Button       = 4,
        Checkbox     = 5,
        RadioButton  = 6,
        Control      = 7,
        StaticText   = 8,
        EditableText = 16,
        Icon         = 32,
        Picture      = 64,
    }

    // Host-installed dialog template. The Game host wires this from src/'s parsed DLOG/DITL resources
    // (OverrideGameData.Dlogs / .Ditls). Returns null for an unknown id.
    public sealed class DlgTemplateItem
    {
        public DitlItemKind Kind;
        public int Top, Left, Bottom, Right;   // DITL-local coords
        public bool Enabled;
        public string Text = "";
        public int ResourceId;                  // CNTL / ICON / PICT id
    }
    public sealed class DlgTemplate
    {
        public int Top, Left, Bottom, Right;    // DLOG window bounds (often offscreen → we centre)
        public int ProcId;                      // WDEF variant: 1 = dBoxProc (8px modal chrome), 2 = plainDBox (1px frame)
        public int PositionType;                // DLOG auto-position code (0x280A centerMainScreen, 0x300A alertPositionMainScreen, …)
        public int ItemsId;
        public bool Visible;                    // DLOG visible-at-creation flag (false for every game DLOG but 3000/3100 — flows ShowWindow explicitly)
        public List<DlgTemplateItem> Items = new();
        public int Width  => Right - Left;
        public int Height => Bottom - Top;
    }
    public static Func<int, DlgTemplate?>? GetDialogTemplateImpl;

    // Live dialog records.
    private sealed class DlgItem
    {
        public int ItemNo;
        public DitlItemKind Kind;
        public bool Enabled;
        public string Text = "";
        public string EditText = "";            // live contents of an EditableText item
        public int ResId;
        public int Top, Left, Bottom, Right;    // GLOBAL coords
        public int Handle;                      // opaque item id GetDialogItem returns (was a 2-byte EvoMemory cell addr)
        public short CtrlValue;                 // control value (checkbox state etc.) — lived behind Handle in EvoMemory before
        public byte CtrlHilite;                 // HiliteControl state: 0 active, 255 disabled/dimmed, 1-253 part pressed
        // TextEdit state of an EditableText item (the Dialog Manager keeps a TE
        // record per editText; the port keeps it inline on the item):
        public int SelStart, SelEnd;            // selection [SelStart, SelEnd); collapsed = caret position
        public int SelAnchor;                   // fixed end while shift-click / TEClick drag extends
        public int ScrollX;                     // TE horizontal auto-scroll offset (px) — overflowing text pans to keep the caret in view
        public bool Contains(int x, int y)
            => x >= Left && x < Right && y >= Top && y < Bottom;
    }
    private sealed class DlgRecord
    {
        public int Handle;                      // WindowRecord addr (= dialog ptr)
        public int DlogId;
        public int ProcId;                      // DLOG WDEF variant (see DlgTemplate.ProcId)
        public int RefCon;                      // SetWRefCon/GetWRefCon (the nag's "Not Yet" re-enable deadline)
        public bool Visible;                    // window visible flag: DLOG's at creation, then ShowWindow/HideWindow
        public int WinTop, WinLeft, WinBottom, WinRight;  // global window rect
        public List<DlgItem> Items = new();
        public int DefaultItem = 1;             // SetDialogDefaultItem → Return fires this
        public int FocusedEdit = 0;             // item no of the editable text with focus (0 = none)
        public bool CaretOn = true;             // TE caret blink phase (GetCaretTime); reset visible on any edit
        // Per-window backing buffer for the window-layer compositor. Screen-sized so
        // the global-coord dialog draws land at their absolute position with no
        // coordinate rework; registered at BufferKey = Handle+2, the key SetPort/
        // CopyBits already route dialog draws to. Always allocated in GetNewDialog.
        public Rgba8Image? Buffer;
        public int BufferKey;
        // Draw-batch depth SuspendDrawBatchForModal saved when this dialog opened
        // mid-batch (the game tick opens dialogs); DisposeDialog restores it.
        public int SavedBatchDepth;
    }

    // Window-layer compositor: a snapshot of the visible dialog windows the host composites over the
    // scene each frame, back-to-front. Built on the title thread after any
    // window stack/rect change (push/pop/MoveWindow) and published as an
    // immutable array the host reads lock-free (volatile reference swap).
    public readonly struct WindowLayer
    {
        public readonly Rgba8Image Buffer;
        public readonly int Left, Top, Right, Bottom;   // screen rect to composite (incl. frame margin)
        public WindowLayer(Rgba8Image buffer, int left, int top, int right, int bottom)
        { Buffer = buffer; Left = left; Top = top; Right = right; Bottom = bottom; }
    }
    // FillDialogBackground draws the WDEF frame just OUTSIDE the window rect —
    // up to 8px for the dBoxProc modal chrome; grow the composited rect so it
    // isn't clipped. Over-grown (transparent) pixels are alpha-0 no-ops, so a
    // margin past the widest frame is safe.
    private const int DialogLayerMargin = 9;
    private static volatile WindowLayer[] _windowLayers = System.Array.Empty<WindowLayer>();

    // Rebuild the published layer snapshot from the live stack (title thread only).
    // Stack enumerates top-first → walk in reverse for back-to-front paint order.
    private static void RebuildWindowLayers()
    {
        var arr = _dialogStack.ToArray();   // top-first
        var layers = new List<WindowLayer>(arr.Length);
        for (int i = arr.Length - 1; i >= 0; i--)   // bottom (back) → top (front)
        {
            var rec = arr[i];
            if (rec.Buffer is null || !rec.Visible) continue;   // hidden window: draws stay in the buffer, nothing composites
            layers.Add(new WindowLayer(rec.Buffer,
                rec.WinLeft - DialogLayerMargin, rec.WinTop - DialogLayerMargin,
                rec.WinRight + DialogLayerMargin, rec.WinBottom + DialogLayerMargin));
        }
        _windowLayers = layers.ToArray();
    }

    /// The visible dialog windows to composite over the scene, back-to-front.
    /// Empty when the compositor is off (no window has a buffer). Host-thread safe
    /// (returns an immutable snapshot swapped in by the title thread).
    public static WindowLayer[] SnapshotVisibleWindows() => _windowLayers;

    // Typed-char capture (thread-safe). The host's Window.TextInput handler runs on the MonoGame thread and
    // appends here; the modal loop (title thread) drains it. A dedicated
    // locked buffer — NOT the per-frame-cleared FrameTextInput list — so a
    // keystroke can't be cleared by the 60 Hz Draw before the 50 Hz modal
    // loop reads it (that race dropped characters from the name field).
    // Mac EventRecord modifier bits carried with each key (the subset the port
    // maps: the host drives cmdKey from Ctrl per the keyboard rule).
    public const short MacShiftKeyBit = 0x200;   // shiftKey
    public const short MacCmdKeyBit   = 0x100;   // cmdKey (host maps Ctrl → Cmd)

    /// One queued keystroke: the Mac charCode plus the modifier bits at press
    /// time (per-key, not frame-sampled — a fast Ctrl tap can't miss its key).
    public readonly struct TypedKey
    {
        public readonly char Ch;
        public readonly short Mods;
        public TypedKey(char ch, short mods) { Ch = ch; Mods = mods; }
    }

    private static readonly object _typedLock = new object();
    private static readonly Queue<TypedKey> _typedBuf = new Queue<TypedKey>();
    public static void EnqueueTypedChar(char c) => EnqueueTypedKey(c, 0);
    public static void EnqueueTypedKey(char c, short macModifiers)
    {
        lock (_typedLock) _typedBuf.Enqueue(new TypedKey(c, macModifiers));
    }
    private static List<TypedKey> DrainTypedKeys()
    {
        lock (_typedLock)
        {
            if (_typedBuf.Count == 0) return _noTypedKeys;
            var list = new List<TypedKey>(_typedBuf);
            _typedBuf.Clear();
            return list;
        }
    }
    private static readonly List<TypedKey> _noTypedKeys = new List<TypedKey>();
    /// Pop a single typed char from the durable buffer (FIFO). Used by WaitNextEvent so a
    /// keystroke survives the host's per-frame FrameTextInput.Clear (the same race the durable
    /// buffer fixes for ModalDialog). Returns false when the buffer is empty. (Modifiers ride
    /// along in FrameModifiers for this path — the register event loop samples them per frame.)
    public static bool TryDequeueTypedChar(out char c)
    {
        lock (_typedLock)
        {
            if (_typedBuf.Count == 0) { c = '\0'; return false; }
            c = _typedBuf.Dequeue().Ch;
            return true;
        }
    }
    private static void ClearTypedChars()
    {
        lock (_typedLock) _typedBuf.Clear();
    }

    public static bool HasOpenDialog => _dialogStack.Count > 0;
    /// The frontmost open dialog's DLOG resource id (0 if none). Read by
    /// LoadStyledTextResource to attach styled text to the dialog being built.
    public static int CurrentDialogId => _dialogStack.Count > 0 ? _dialogStack.Peek().DlogId : 0;

    public static bool DialogTrace;   // opt-in per-dialog open trace, like FileManagerTrace
    private static readonly Dictionary<int, DlgRecord> _dialogs = new();
    private static readonly Stack<DlgRecord> _dialogStack = new();
    // item handle (opaque id) → item, so SetDialogItemText / SetControlValue
    // can resolve the item GetDialogItem handed back. Cleared when the dialog
    // arena resets.
    private static readonly Dictionary<int, DlgItem> _itemByHandle = new();

    // Opaque dialog/item handle band. NO EvoMemory behind these. Strides preserved (64 B per
    // window, 2 B per item) so `dialog + 2` / item-handle arithmetic stays
    // numerically distinct. Bump-allocated; reset when the last dialog is
    // disposed (handles are reused — see Serial). The old arena base
    // 0x10210000 silently overlapped MacScratch's region base; that latent
    // collision is gone with the re-base.
    public const int DlgHandleBase = 0x78000000;
    private const int DlgHandleBandSize = 0x01000000;   // 16 MB span above the base — the opaque handle band
    private static int _dlgHandleNext = DlgHandleBase;
    private static int DlgAlloc(int bytes)
    {
        int p = _dlgHandleNext;
        _dlgHandleNext += (bytes + 1) & ~1;   // keep even-aligned
        return p;
    }

    /// True for any handle in the opaque dialog/item band — the port
    /// accessors (GetPortRectShorts/GetPortVisRgn/...) dual-dispatch on this
    /// so window-record walks of a DIALOG route to the managed DlgRecord.
    public static bool IsDialogHandle(int handle)
        => handle >= DlgHandleBase && handle < DlgHandleBase + DlgHandleBandSize;

    // Modal-filter proc registry. The Ports layer registers a typed filter
    // adapter — fn(dialogPtr, MacEvent) → non-zero if consumed — under the
    // filter FUN's code-address sentinel; NewRoutineDescriptor returns that
    // sentinel, and ModalDialog dispatches the delegate. (The legacy
    // raw-EventRecord-scratch registry + bridge are gone — every filter is
    // typed now; dialog 4-rules final core batch.)
    private static readonly Dictionary<int, Func<int, MacEvent, int>> _modalFiltersTyped = new();
    public static void RegisterModalFilter(int procPtr, Func<int, MacEvent, int> fn)
        => _modalFiltersTyped[procPtr] = fn;

    /// Resolve a filter proc sentinel to its typed delegate. Null = no filter.
    private static Func<int, MacEvent, int>? ResolveModalFilter(int procPtr)
        => _modalFiltersTyped.TryGetValue(procPtr, out var typed) ? typed : null;

    /// Synthesize the event ModalDialog hands the filter each poll.
    private static MacEvent MakeEvent(short what)
    {
        var m = FrameMouseBridge;
        return new MacEvent
        {
            What = what,
            Message = 0,
            // Real TickCount, like Mac WaitNextEvent's `when` (+6): ShipyardFilter's
            // double-click (evt.When - LastClickWhen < 16) needs a real monotonic tick.
            When = (int)TickCount(),
            WhereV = m.V,
            WhereH = m.H,
            // DEVIATION (faithful): live host-sampled modifier bits (cmdKey 0x100 /
            // shiftKey 0x200 / …, see FrameModifiers) on every event kind — the raw
            // scratch read 0 here, so the commodity exchange's shift-cycle-backward and
            // the player-info shift gate run live. RunModalLoop's keyDown dispatch
            // overwrites Message AND Modifiers with the drained TypedKey's own per-key
            // char/mods after this returns.
            Modifiers = (short)FrameModifiers,
        };
    }

    // Per-dialog UserItem redraw hook, keyed by DLOG id. The Mac draws a
    // dialog's userItems via their item draw procs; our DrawDialog only does
    // the standard items, so a dialog with a custom-drawn area (e.g. the Game
    // Speed slider, item 4 of DLOG 4002) registers a redraw here. ModalDialog
    // invokes it after every background fill + DrawDialog so the custom art is
    // not erased (dialogs with a modal filter redraw their own art instead).
    private static readonly Dictionary<int, Action> _dialogUserDraw = new();
    public static void RegisterDialogUserDraw(int dlogId, Action draw)
        => _dialogUserDraw[dlogId] = draw;

    // Styled-text overlays (the shareware nag's TEXT 900/901 message body).
    // LoadStyledTextResource registers the caret-resolved text here per DLOG id —
    // WITH the 'styl' resource's per-run font/size/face/line metrics when present —
    // and RedrawDialog replays each after the standard items. (The nag's message
    // item is a UserItem — DrawDialog doesn't draw it — and a one-shot draw at
    // dialog setup would be erased by the first RedrawDialog background fill, so it
    // must be replayed every redraw.) GetNewDialog resets the list for the id so
    // re-opens don't stack.

    /// One 'styl' resource run: chars [Start..next run's Start) draw in font
    /// family `Font` at `Size` with QuickDraw Style bits `Face`; `Height`/`Ascent`
    /// are the run's TE line metrics straight from the resource (TE sizes each
    /// LINE by the max height/ascent of the runs on it).
    public readonly struct StyledRun
    {
        public readonly int Start;
        public readonly short Font, Size, Height, Ascent;
        public readonly byte Face;
        public StyledRun(int start, short font, short size, byte face, short height, short ascent)
        { Start = start; Font = font; Size = size; Face = face; Height = height; Ascent = ascent; }
    }

    private static readonly Dictionary<int, List<(short[] rect, string text, StyledRun[]? runs)>> _dialogStyledText = new();
    public static void AddDialogStyledText(int dlogId, short[] rect, string text)
        => AddDialogStyledText(dlogId, rect, text, null);
    public static void AddDialogStyledText(int dlogId, short[] rect, string text, StyledRun[]? runs)
    {
        if (string.IsNullOrEmpty(text) || rect is null || rect.Length < 4) return;
        if (!_dialogStyledText.TryGetValue(dlogId, out var list)) { list = new(); _dialogStyledText[dlogId] = list; }
        list.Add(((short[])rect.Clone(), text, runs is { Length: > 0 } ? runs : null));
    }

    // Styled TETextBox: word-wrap `text` into `rect` honouring the 'styl' runs —
    // per-run font/size, QuickDraw Style bits (bold = +1px double-draw with the
    // advance widened by 1, condensed/extended = advance ∓1, the classic QD glyph
    // derivations), and TE's per-LINE metrics (baseline/pitch = max run ascent/
    // height on that line). Left-aligned, black, erases the rect to the current
    // BackColor first — the plain-TETextBox contract.
    private static void DrawStyledTextBox(string text, short[] rectShorts, StyledRun[] runs)
    {
        var rect = new RectI(rectShorts[1], rectShorts[0],
                             rectShorts[3] - rectShorts[1], rectShorts[2] - rectShorts[0]);
        if (rect.Width <= 0 || rect.Height <= 0 || text.Length == 0) return;
        var bk = _activeBackColor;
        EnqueueDraw(c =>
        {
            c.FillRect(rect, bk);   // TE erases its box before drawing

            StyledRun RunAt(int i)
            {
                var r = runs[0];
                foreach (var cand in runs) { if (cand.Start <= i) r = cand; else break; }
                return r;
            }
            int CharW(char ch, in StyledRun r)
            {
                var f = ResolveFontId(r.Font);
                if (f is null) return 0;
                int w = f.MeasureWidth(ch.ToString(), r.Size);
                if ((r.Face & 0x01) != 0) w += 1;   // bold widens the advance by 1
                if ((r.Face & 0x20) != 0) w -= 1;   // condensed narrows by 1
                if ((r.Face & 0x40) != 0) w += 1;   // extended widens by 1
                return w;
            }

            // Split into tokens (runs of non-spaces / single spaces / hard breaks),
            // then greedy word-wrap. A token's chars may span style runs.
            int n = text.Length;
            int lineStart = 0, x = 0, yTop = rect.Y;
            var line = new System.Collections.Generic.List<(int idx, char ch)>();

            void FlushLine()
            {
                if (line.Count > 0)
                {
                    short ascent = 0, height = 0;
                    foreach (var (idx, _) in line)
                    {
                        var r = RunAt(idx);
                        if (r.Ascent > ascent) ascent = r.Ascent;
                        if (r.Height > height) height = r.Height;
                    }
                    int px = rect.X;
                    foreach (var (idx, ch) in line)
                    {
                        var r = RunAt(idx);
                        var f = ResolveFontId(r.Font);
                        if (f is not null && ch != ' ')
                        {
                            // Baseline at yTop+ascent; DrawText's y is the box top (baseline - font ascent).
                            int gy = yTop + ascent - f.Ascent(r.Size);
                            f.DrawText(c, ch.ToString(), px, gy, RgbaColor.Black, r.Size);
                            if ((r.Face & 0x01) != 0) f.DrawText(c, ch.ToString(), px + 1, gy, RgbaColor.Black, r.Size);
                        }
                        px += CharW(ch, in r);
                    }
                    yTop += height > 0 ? height : 12;
                }
                else
                {
                    // Empty line (blank paragraph): advance by the metrics of the run there.
                    var r = RunAt(System.Math.Min(lineStart, n - 1));
                    yTop += r.Height > 0 ? r.Height : 12;
                }
                line.Clear();
                x = 0;
            }

            int i = 0;
            while (i < n)
            {
                char ch = text[i];
                if (ch == '\r' || ch == '\n')
                {
                    lineStart = i;
                    FlushLine();
                    i++;
                    continue;
                }
                if (ch == ' ')
                {
                    line.Add((i, ch));
                    x += CharW(ch, RunAt(i));
                    i++;
                    continue;
                }
                // Word token [i, j).
                int j = i, wordW = 0;
                while (j < n && text[j] != ' ' && text[j] != '\r' && text[j] != '\n')
                { wordW += CharW(text[j], RunAt(j)); j++; }
                if (x > 0 && x + wordW > rect.Width)
                {
                    // Word doesn't fit: wrap (dropping the trailing spaces of the line).
                    while (line.Count > 0 && line[^1].ch == ' ') line.RemoveAt(line.Count - 1);
                    lineStart = i;
                    FlushLine();
                }
                for (int k = i; k < j; k++) { line.Add((k, text[k])); x += CharW(text[k], RunAt(k)); }
                i = j;
            }
            if (line.Count > 0) FlushLine();
        });
    }

    public static int GetNewDialog(int dlogId, int storage, int behind)
    {
        var tmpl = GetDialogTemplateImpl?.Invoke(dlogId);
        // No template → behave like the old shim (return 0). Callers whose
        // dialog isn't served take their no-op path; no log (benign, common).
        if (tmpl is null) return 0;

        // Fresh open of this dialog id → clear any styled-text overlay from a prior open
        // (LoadStyledTextResource re-registers it during this open's setup).
        _dialogStyledText.Remove(dlogId);

        // Auto-position on the main screen per the DLOG's positioning code (the
        // resource bounds are the classic offscreen placeholder). centerMainScreen
        // (0x280A, the common case) centres both axes; the alertPosition* codes
        // (0x300A/0x700A/0xB00A — the shareware nag) sit HIGHER: the classic alert
        // position leaves one-fifth of the free space above the window (parent
        // window == main screen here — the game fills it). The original read the
        // render-context portRect bottom/right; that ctx slot (0x10080d08) has no
        // backing anymore (EvoMemory itself is gone) and Toolbox can't see
        // Core.Model.GlobalState, so use the host display size InitMainScreenDevice
        // mirrors here.
        int screenH = _mainScreenHeight;
        int screenW = _mainScreenWidth;
        if (screenW <= 0) screenW = 800;
        if (screenH <= 0) screenH = 600;
        bool alertPos = tmpl.PositionType is 0x300a or 0x700a or 0xb00a;
        int winLeft = Math.Max(0, (screenW - tmpl.Width) / 2);
        int winTop  = Math.Max(0, (screenH - tmpl.Height) / (alertPos ? 5 : 2));

        // Opaque handle — no window-record bytes behind it (EvoMemory itself is
        // gone now). DlgRecord below holds the live window rect; consumers
        // use GetDialogPortRect/GetDialogVisRgn.
        int handle = DlgAlloc(64);

        var rec = new DlgRecord
        {
            Handle = handle, DlogId = dlogId, ProcId = tmpl.ProcId,
            WinTop = winTop, WinLeft = winLeft,
            WinBottom = winTop + tmpl.Height, WinRight = winLeft + tmpl.Width,
            // Honour the DLOG visible flag: nearly every game DLOG is created
            // HIDDEN and revealed by the flow's explicit ShowWindow (the nag's
            // comes after the zoom-rect animation), so pre-show setup draws
            // (HiliteControl dims, fills) must not reach the screen yet.
            Visible = tmpl.Visible,
        };
        int n = 0;
        foreach (var t in tmpl.Items)
        {
            n++;
            // Allocate an opaque 2-byte-stride id for EVERY item and use it as
            // the item's handle: a stable non-zero identity GetDialogItem hands
            // back and SetControlValue/SetDialogItemText map to the item (the
            // volume static-text, item 0x25, needs this). The control VALUE
            // lives in DlgItem.CtrlValue — no EvoMemory cell behind the handle.
            int itemHandle = DlgAlloc(2);
            var item = new DlgItem
            {
                ItemNo = n, Kind = t.Kind, Enabled = t.Enabled, Text = t.Text, ResId = t.ResourceId,
                Top = winTop + t.Top, Left = winLeft + t.Left,
                Bottom = winTop + t.Bottom, Right = winLeft + t.Right,
                Handle = itemHandle,
            };
            rec.Items.Add(item);
            _itemByHandle[itemHandle] = item;
        }
        // Window-layer compositor: give this window its own screen-sized backing
        // buffer at handle+2 (the key RedrawDialog's SetPort + the spaceport's
        // CopyBits already target), so its draws land here instead of the shared
        // scene buffer and the host composites it as a layer.
        rec.BufferKey = handle + 2;
        rec.Buffer = new Rgba8Image(screenW, screenH);
        RegisterRenderTarget(rec.BufferKey, rec.Buffer);
        _dialogs[handle] = rec;
        _dialogStack.Push(rec);
        RebuildWindowLayers();
        // Dialogs opened from INSIDE RunMainGameLoop's tick-atomic draw batch (TickShipAI →
        // spaceport hub / galaxy map, and their sub-dialogs) block the tick for the whole
        // dialog session — with the batch left open nothing drawn since BeginDrawBatch would
        // ever drain, so the dialog runs invisibly behind the frozen scene. Suspend for the
        // window's lifetime (DisposeDialog restores) rather than just around ModalDialog:
        // the bar's slot machine and holo-vid news run their own Button()-poll loops that
        // never enter ModalDialog at all. Nested opens save 0 → no-op. LIFO discipline
        // (every EVO dialog disposes before its parent resumes) keeps the restores paired.
        rec.SavedBatchDepth = SuspendDrawBatchForModal();
        ClearTypedChars();   // drop any keystrokes that arrived before the dialog opened
        if (DialogTrace) Console.WriteLine($"[Dialog] GetNewDialog({dlogId}) → handle=0x{handle:x} at ({winTop},{winLeft}) {tmpl.Width}x{tmpl.Height}, {rec.Items.Count} items");
        return handle;
    }

    private static DlgRecord? FindDialog(int handle)
        => _dialogs.TryGetValue(handle, out var r) ? r
         : (_dialogStack.Count > 0 ? _dialogStack.Peek() : null);

    // Managed-rect overload: rectOut is the project's {top,left,bottom,right}
    // short[4]; the int type/handle outs are vestigial (every call site passes 0 —
    // callers wanting type/handle use the short[]/int[] managed overload in
    // MacToolbox.cs). Delegates there with null type/handle; only rectOut is
    // populated, identically.
    public static void GetDialogItem(int dialog, int itemNo, int typeOut, int handleOut, short[] rectOut)
        => GetDialogItem(dialog, itemNo, (short[]?)null, (int[]?)null, rectOut);

    /// Managed convenience: the item's opaque handle directly (0 if the item is
    /// missing) — replaces the GetDialogItem(handleOut cell) + ReadInt dance.
    public static int GetDialogItemHandle(int dialog, int itemNo)
    {
        var rec = FindDialog(dialog);
        var it = rec?.Items.Find(i => i.ItemNo == itemNo);
        return it?.Handle ?? 0;
    }

    // Control values. The handle is the opaque id GetDialogItem returned; the value
    // lives in the managed DlgItem, not in memory behind the handle.
    public static int SetControlValue(int controlHandle, int value)
    {
        if (_itemByHandle.TryGetValue(controlHandle, out var it)) it.CtrlValue = (short)value;
        return 0;
    }
    public static short GetControlValue(int controlHandle)
        => _itemByHandle.TryGetValue(controlHandle, out var it) ? it.CtrlValue : (short)0;

    // Dialog window-record accessors — migration targets for ported code that read the raw window-record stub:
    //   PaintRect/FrameRect/EraseRect/InvalRect(win+0x10)  → GetDialogPortRect
    //   EvoMemory.ReadInt(win+0x18) (visRgn for RectInRgn)  → GetDialogVisRgn
    // CopyBits dst keys keep the numeric `dialog + 2` form (opaque key, same
    // convention as MacGrafPort.PixmapKey — the bridge never reads memory there).

    /// The dialog window's portRect {top,left,bottom,right} in global coords
    /// (the game's dialogs have no per-port origin — portRect IS the screen rect).
    public static short[] GetDialogPortRect(int dialog)
    {
        var rec = FindDialog(dialog);
        if (rec is null) return new short[4];
        return new[] { (short)rec.WinTop, (short)rec.WinLeft, (short)rec.WinBottom, (short)rec.WinRight };
    }

    /// The dialog window's visRgn "handle" — a non-zero token only ever fed to
    /// the RectInRgn stub / CopyBits mask-rgn arg (neither inspects it).
    public static int GetDialogVisRgn(int dialog)
        => FindDialog(dialog)?.Handle ?? 0;

    // MoveWindow — move a window's top-left to global (h, v), preserving size.
    // Translates the dialog record's port rect so RecenterWindowIntoPlayArea
    // (FUN_100583c8) can nudge in-game dialogs (spaceport/outfit/shipyard/bar) out
    // of the 144px HUD panel; the recenter passes re-read via GetPortRectShorts.
    // Non-dialog windows (the game port sentinel) have no record → no-op.
    public static int MoveWindow(int window, long h, long v, int front)
    {
        var rec = FindDialog(window);
        if (rec is not null)
        {
            // Translate the whole window by the delta — the window AND its content move
            // together (Mac MoveWindow). The game stores item rects in GLOBAL coords, and
            // RedrawSpaceportDialog/DrawDialog draw each item at its global rect (via
            // GetDialogItem), so the items MUST shift with the window or the frame moves
            // while the PICT/text/buttons stay put.
            int dx = (int)h - rec.WinLeft;
            int dy = (int)v - rec.WinTop;
            rec.WinLeft += dx; rec.WinRight += dx;
            rec.WinTop += dy; rec.WinBottom += dy;
            foreach (var it in rec.Items)
            {
                it.Left += dx; it.Right += dx;
                it.Top += dy; it.Bottom += dy;
            }
            // The window's backing buffer is screen-sized (draws land at global
            // coords), so it needs no move — but the composited rect does.
            RebuildWindowLayers();
        }
        return 0;
    }

    public static void DisposeDialog(int dialog)
    {
        // Strict lookup: an unknown/stale handle disposes NOTHING. FindDialog's
        // front-dialog fallback would pop the wrong window and double-resume its
        // draw batch; Mac DisposeDialog on a bogus DialogPtr is a no-op.
        _dialogs.TryGetValue(dialog, out var rec);
        if (rec is not null)
        {
            if (rec.Buffer is not null)   // window-layer compositor: release the backing buffer
            {
                RegisterRenderTarget(rec.BufferKey, null);
                rec.Buffer = null;
            }
            _dialogs.Remove(rec.Handle);
            if (_dialogStack.Count > 0 && _dialogStack.Peek() == rec) _dialogStack.Pop();
            ResumeDrawBatchAfterModal(rec.SavedBatchDepth);   // re-arm the tick batch this open suspended
        }
        if (_dialogStack.Count == 0)
        {
            _dlgHandleNext = DlgHandleBase;  // reclaim the handle band
            _itemByHandle.Clear();
        }
        RebuildWindowLayers();
    }

    // Mac SetDialogItemText: set a dialog item's text.
    public static void SetDialogItemText(int itemHandle, string s)
    {
        if (!_itemByHandle.TryGetValue(itemHandle, out var it)) return;
        s ??= "";
        if (it.Kind == DitlItemKind.EditableText)
        {
            it.EditText = s;
            // Mac TESetText resets the selection; clamp the TE state to the new text.
            if (it.SelStart > s.Length) it.SelStart = s.Length;
            if (it.SelEnd > s.Length) it.SelEnd = s.Length;
            if (it.SelAnchor > s.Length) it.SelAnchor = s.Length;
            if (it.ScrollX > 0) EnsureEditVisible(it, it.SelEnd);
        }
        else it.Text = s;
    }

    // Mac GetDialogItemText: read a dialog item's text. For the New Pilot name field /
    // the confirm-text alert, this returns whatever the user typed.
    public static string GetDialogItemText(int itemHandle)
    {
        if (_itemByHandle.TryGetValue(itemHandle, out var it))
            return it.Kind == DitlItemKind.EditableText ? it.EditText : it.Text;
        return "";
    }

    // Mac SelIText: give an editable item keyboard focus and select chars
    // [start, end) — clamped to the text, so the callers' (0, 254) selects all
    // and the first keystroke replaces it. Repaints the item in place (SelIText
    // shows the new selection immediately — the New Pilot too-long-name beep
    // path calls this mid-modal-loop).
    public static int SelectDialogItemText(int dialog, int itemNo, int start, int end)
    {
        var rec = FindDialog(dialog);
        if (rec is null) return 0;
        var it = rec.Items.Find(i => i.ItemNo == itemNo);
        if (it is null || it.Kind != DitlItemKind.EditableText) return 0;
        rec.FocusedEdit = itemNo;
        int len = it.EditText.Length;
        it.SelStart = System.Math.Clamp(start, 0, len);
        it.SelEnd = System.Math.Clamp(end, it.SelStart, len);
        it.SelAnchor = it.SelStart;
        rec.CaretOn = true;
        EnsureEditVisible(it, it.SelStart);   // a select-all shows the text from its start
        RepaintEditItem(rec, itemNo);
        return 0;
    }

    // ParamText (Dialog Manager ^0..^3 substitution).
    // Mac ParamText(p0,p1,p2,p3) stores up to four Str255 strings; the Dialog
    // Manager then substitutes them for the literals ^0..^3 whenever it DRAWS a
    // statText item (and window titles). A NULL/zero pointer leaves that slot
    // UNCHANGED (Inside Macintosh: Dialog Manager); an explicit empty string
    // clears it. We keep four managed strings and substitute in DrawDialog's
    // StaticText path — the faithful Mac location. (TETextBox and the EVO alert
    // redraws are raw TextEdit draws and do NOT substitute, matching the Mac.)
    private static readonly string[] _paramText = { "", "", "", "" };

    /// Set substitution slot `idx` (0..3). null = leave unchanged (Mac semantics).
    private static void SetParamTextSlot(int idx, string? s)
    {
        if (idx < 0 || idx > 3 || s is null) return;
        _paramText[idx] = s;
    }

    /// Decode an un-collapsed managed Str255 byte[] (length byte + chars).
    /// Returns null when `buf` is null so the slot is left unchanged.
    private static string? DecodeParamPascalBytes(byte[]? buf)
    {
        if (buf is null) return null;
        if (buf.Length == 0) return "";
        int len = System.Math.Min(buf[0] & 0xff, buf.Length - 1);
        var bytes = new byte[len];
        System.Array.Copy(buf, 1, bytes, 0, len);
        try { return System.Text.Encoding.GetEncoding(10000).GetString(bytes); }
        catch { return System.Text.Encoding.Latin1.GetString(bytes); }
    }

    /// Replace ^0..^3 in `s` with the stored ParamText strings. Scans
    /// left-to-right; a `^` not followed by 0-3 is left literal; substituted
    /// text is not re-scanned (no recursion) — exactly the Mac behaviour.
    internal static string SubstituteParamText(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('^') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length + 16);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '^' && i + 1 < s.Length)
            {
                char d = s[i + 1];
                if (d >= '0' && d <= '3') { sb.Append(_paramText[d - '0']); i++; continue; }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// ParamText params-absorber funnel: a managed byte[] decodes into its slot;
    /// null and any other shape (e.g. the broken int[62] buffers
    /// RunMultiButtonModalDialog passes) leave the slot unchanged.
    internal static void ApplyParamTextArgs(object?[] args)
    {
        if (args is null) return;
        for (int i = 0; i < args.Length && i < 4; i++)
        {
            switch (args[i])
            {
                case null: break;                                                       // leave unchanged
                case byte[] b: SetParamTextSlot(i, DecodeParamPascalBytes(b)); break;
                // int[]/short/etc. (un-collapsed earlier-transcription shapes): leave unchanged.
            }
        }
    }

    /// ParamText(byte[],byte[],byte[],byte[]) funnel — un-collapsed buffers.
    internal static void ApplyParamTextBytes(byte[]? s1, byte[]? s2, byte[]? s3, byte[]? s4)
    {
        SetParamTextSlot(0, DecodeParamPascalBytes(s1));
        SetParamTextSlot(1, DecodeParamPascalBytes(s2));
        SetParamTextSlot(2, DecodeParamPascalBytes(s3));
        SetParamTextSlot(3, DecodeParamPascalBytes(s4));
    }

    /// Managed-string ParamText — the migration target for callers whose four
    /// substitution strings are already C# strings (dialog 4-rules B9:
    /// RunMultiButtonModalDialog / ShowSharewareNagDialog). null leaves a slot
    /// unchanged (the Mac NULL-pointer semantic); "" clears it (the Mac
    /// empty-Pascal-string semantic).
    public static void ParamText(string? s1, string? s2, string? s3, string? s4)
    {
        SetParamTextSlot(0, s1);
        SetParamTextSlot(1, s2);
        SetParamTextSlot(2, s3);
        SetParamTextSlot(3, s4);
    }

    // DrawDialog (standard items): renders buttons / checkboxes / static-text / pictures. UserItems
    // (the keybind slots + PICT-132 backdrop) are drawn by the prefs
    // filter's FUN_10044ef4 path, not here. Draws to the current port
    // (SetPort(dialog) → screen fallback).
    public static void DrawDialog(int dialog)
    {
        var rec = FindDialog(dialog);
        if (rec is null) return;
        foreach (var it in rec.Items)
        {
            // Mac dialogs clip item drawing to the window's port. Several alert
            // DITLs (DLOG 3000/3001/3002) carry Picture items positioned BELOW the
            // window bounds (helper/placeholder art that the WDEF never reveals).
            // The game draws items in absolute screen coords with no port clip, so those
            // off-window pictures were painted onto the title backdrop as a garbled
            // image "off the dialog". Skip any item that doesn't intersect the
            // dialog window rect — the faithful equivalent of the port clip.
            bool outside = it.Right <= rec.WinLeft || it.Left >= rec.WinRight ||
                           it.Bottom <= rec.WinTop || it.Top >= rec.WinBottom;
            if (outside) continue;
            switch (it.Kind)
            {
                case DitlItemKind.Button:
                    DrawDlgButton(it, isDefault: it.ItemNo == rec.DefaultItem);
                    break;
                case DitlItemKind.Checkbox:
                    DrawDlgCheckbox(it);
                    break;
                case DitlItemKind.StaticText:
                {
                    // Mac Dialog Manager substitutes ParamText ^0..^3 as it draws
                    // statText (enabled or disabled — both are static label art).
                    string txt = SubstituteParamText(it.Text);
                    if (txt.Length > 0)
                        DrawDlgStaticText(txt, it, RgbaColor.Black, rec);
                    break;
                }
                case DitlItemKind.Picture:
                    DrawDlgPicture(it);
                    break;
                case DitlItemKind.EditableText:
                    DrawDlgEditText(it, focused: rec.FocusedEdit == it.ItemNo, caretOn: rec.CaretOn);
                    break;
                // UserItem / Icon / Control: skip (handled elsewhere or unused).
            }
        }
    }

}
