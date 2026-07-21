using System;

namespace OpenEV.Platform.Toolbox;

// Per-frame input snapshot + frame-pump entry for the FUN_xxx-owns-
// the-loop architecture.
//
// MonoGame / SpriteBatch / RenderTarget2D dependencies stripped for the port.
// TickFrame is replaced with a no-arg stub. Host wiring will restore
// rendering later.
public static partial class MacToolbox
{
    // Per-frame input snapshot. Backed by volatile fields in MacToolbox.Bridge.cs
    // so the title thread reads MonoGame-thread writes correctly.
    public static MPoint FrameMouse
    {
        get => FrameMouseBridge;
        set => FrameMouseBridge = value;
    }
    public static bool FrameButtonDown
    {
        get => FrameButtonDownBridge;
        set => FrameButtonDownBridge = value;
    }
    /// Mac event-record event-code slot. WaitNextEvent shim reads this
    /// (and zeroes it after consumption).
    /// Mac event-record message slot for the keyDown WaitNextEvent emits (char in the low byte).
    public static int    FrameEventMessage;
    /// Mac event-record 'where' slot: the mouse point AT THE INSTANT WaitNextEvent captured a
    /// mouseDown edge (EventRecord+0xa, real Mac hardware interrupts stamp this at click time and
    /// it stays fixed in the queue until dequeued). WaitNextEvent below latches FrameMouse here the
    /// moment the rising edge fires; callers must read THIS, not live FrameMouse, when handling a
    /// MouseDown event — the cursor can drift for multiple frames between edge detection and
    /// dispatch (e.g. the title thread doesn't poll at all during a flight session), so a live read
    /// hit-tests wherever the cursor happens to be NOW instead of where the click actually landed.
    public static MPoint FrameEventWhere;
    /// Mac event-record modifiers slot (btnState 0x80 / cmdKey 0x100 / shiftKey 0x200 /
    /// alphaLock 0x400 / optionKey 0x800 / controlKey 0x1000, + right-side 0x2000/0x4000).
    /// The host samples the held modifier keys into this each frame (SampleInput);
    /// ModalDialog's MakeEvent transcribes it into the filter event's modifiers word
    /// (EventRecord +0xe). Volatile: host thread writes, title thread reads.
    public static volatile int FrameModifiers;
    /// Characters typed this frame.
    public static readonly System.Collections.Generic.List<char> FrameTextInput
        = new System.Collections.Generic.List<char>();

    // Active port dimensions (set by host before TickFrame).

    /// Host entry — no-op stub in the game (MonoGame dep stripped).
    /// Host wiring will restore the full SpriteBatch-backed impl.
    public static void TickFrame()
    {
        /* no-op: MonoGame dep stripped for the game; host wiring will restore */
    }

    /// WaitNextEvent — Mac Toolbox event-pump call. The app relinquishes the
    /// CPU for up to `sleepTicks` (60ths of a second) and wakes EARLY only when
    /// an event becomes available — a click edge, a typed key, or a freshly
    /// invalidated window (updateEvt). When the window expires with nothing
    /// pending it receives a null event (Inside Macintosh: Processes) — so the
    /// idle null-event period IS the sleep parameter. The title and Register
    /// loops both pass 60: their idle handlers (hover-orb frame advance,
    /// field-cursor pick) tick about once per second, the original pacing.
    /// Do NOT "speed this up" to tick-rate null events: that is NOT the trap's
    /// semantic, and made the title hover orb cycle ~30x faster than the Mac
    /// (user-verified against SheepShaver, 2026-07-02).
    /// Returns true when an event is produced; eventCode is the Mac
    /// WaitNextEvent code (1=mouseDown, 3=keyDown, 6=updateEvt).
    private static bool _prevButtonDownOnTitleThread;
    public static bool WaitNextEvent(int eventMask, out ushort eventCode,
                                       int sleepTicks, int mouseRgn)
    {
        // Poll at ~tick granularity across the sleep window so an arriving
        // event wakes the app early. Capped at 1s so the callers' quit-flag
        // checks stay responsive to oversized sleep values.
        int totalMs = (int)System.Math.Clamp(sleepTicks * 1000 / 60, 16, 1000);
        const int pollMs = 16;
        for (int waited = 0; waited < totalMs; waited += pollMs)
        {
            // keyDown: pop one typed char from the durable, locked _typedBuf (EnqueueTypedChar).
            // Callers are the game's title loop and the Register event loop; both hosts feed every
            // TextInput into _typedBuf. We drain ONLY that — not the per-frame-cleared
            // FrameTextInput — so a keystroke can't be wiped by the host's Draw before this poll
            // reads it (that race dropped chars; the Register's text fields received nothing). We
            // must NOT also drain FrameTextInput: the hosts fill BOTH, so a second drain would
            // double every char. The Mac keyDown EventRecord carries the char in the message low byte.
            char typed = TryDequeueTypedChar(out char durable) ? durable : '\0';
            if (typed != '\0')
            {
                FrameEventMessage = (byte)typed;
                eventCode = (ushort)MacEventType.KeyDown;
                return true;
            }

            // mouseDown: rising edge of the host button state.
            bool now = FrameButtonDownBridge;
            bool prev = _prevButtonDownOnTitleThread;
            _prevButtonDownOnTitleThread = now;
            if (now && !prev)
            {
                FrameEventWhere = FrameMouseBridge;
                eventCode = (ushort)MacEventType.MouseDown;
                return true;
            }

            // updateEvt: a pending update region is an AVAILABLE event, so it
            // wakes the sleep early too — lowest priority after mouse/key
            // within a poll (the Window Manager's update-region model; see
            // MacToolbox.InvalRect). The real trap puts the WindowPtr needing
            // update in EventRecord.message; the port has one game window and
            // the title's update handler doesn't read it, so clear the stale
            // keyDown message instead.
            if (UpdateEventsEnabled && _updateEvtPending)
            {
                _updateEvtPending = false;
                FrameEventMessage = 0;
                eventCode = (ushort)MacEventType.UpdateEvt;
                return true;
            }

            System.Threading.Thread.Sleep(pollMs);
        }
        eventCode = (ushort)MacEventType.NullEvent;
        return false;
    }

    // Window-Manager update-region model (single flag).
    // InvalRect accumulates a per-window update region on the Mac; the Window
    // Manager then synthesizes ONE updateEvt when the app next asks for
    // events. The port keeps a single pending flag (one game window; the
    // title's update handler repaints the whole port), cleared when the
    // updateEvt is delivered above — so Begin/EndUpdate stay no-ops. Gated
    // behind an explicit opt-in: the Register app shares this pump but
    // repaints via its own NeedRedraw flag and never enables it, so it keeps
    // seeing only mouseDown/keyDown/null.
    /// Opt-in for InvalRect→updateEvt delivery (the game host sets this once at setup).
    public static bool UpdateEventsEnabled;
    private static volatile bool _updateEvtPending;
    /// Mark the game window invalidated — the next WaitNextEvent yields an updateEvt.
    public static void NoteWindowInvalidated() { if (UpdateEventsEnabled) _updateEvtPending = true; }
}
