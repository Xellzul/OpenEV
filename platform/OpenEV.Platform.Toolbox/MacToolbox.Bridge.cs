using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// Toolbox bridge — recreates the classic Mac model on a software-rendered host.
//
// Architecture: the ported transcriptions run on a background thread, calling
// MacToolbox.* exactly as the decompile emits them. The host thread owns
// presentation; per-frame mouse/keyboard sampling writes into volatile fields
// the background thread reads when it calls Button() / GetMouse().
//
// Rendering primitives (PaintRect, DrawPicture, CopyBits, etc.) enqueue
// closures into _drawQueue. The host's per-frame DrainDrawQueue runs each
// closure against a managed Canvas bound to the target GWorld buffer (an
// Rgba8Image), producing pixels in pure C#. Mac GWorld semantics ("paints
// persist across frames") are preserved because the Rgba8Image buffers are
// never implicitly cleared.
//
// PICT decode lives in OpenEV.Platform.Imaging via a host-installed resolver — keeps
// this project lean and lets us swap impls.
public static partial class MacToolbox
{
    // Volatile input state: host writes, title thread reads.
    private static volatile bool _frameButtonDown;
    private static int _frameMouseX, _frameMouseY;  // 32-bit atomic on x86/x64

    /// Volatile-backed mirror of FrameButtonDown — used by Button() /
    /// StillDown() / WaitNextEvent on the title thread.
    public static bool FrameButtonDownBridge
    {
        get => _frameButtonDown;
        set => _frameButtonDown = value;
    }

    /// Volatile-backed mirror of FrameMouse — used by GetMouse() on the
    /// title thread.
    public static MPoint FrameMouseBridge
    {
        get => new MPoint((short)Volatile.Read(ref _frameMouseY),
                          (short)Volatile.Read(ref _frameMouseX));
        set { Volatile.Write(ref _frameMouseX, value.H);
              Volatile.Write(ref _frameMouseY, value.V); }
    }

    // Hardware keymap bridge (host writes, title thread reads).
    // Mac GetKeys returns a 128-bit KeyMap (4 longs), bit K = keycode K
    // held. The ported keymap helpers (RefreshCachedKeymap, the prefs
    // key-capture filter via PollFirstHeldUserKey) index it as 16-bit
    // words: word = keycode>>4, bit = keycode&0xf. We keep it as 8 shorts.
    // The host samples its keyboard each frame and maps host keys to Mac
    // keycodes via SetHostKeymap.
    //
    // The keymap is published as an IMMUTABLE array swapped atomically by
    // reference: the title thread (PollFirstHeldUserKey / the release-wait)
    // captures the reference once and scans a CONSISTENT frame. Mutating a
    // shared array in place would let a poll mid-rebuild read a torn map (a
    // stale held key in a not-yet-overwritten word) and capture the WRONG
    // keycode when rebinding.
    private static volatile ushort[] _hostKeymap = new ushort[8];

    /// Host installs the current held-key bitmap. `words` must be 8 entries
    /// (word w bit b ⇒ Mac keycode w*16+b held).
    public static void SetHostKeymap(ushort[] words)
    {
        var snap = new ushort[8];
        int n = System.Math.Min(8, words.Length);
        for (int i = 0; i < n; i++) snap[i] = words[i];
        _hostKeymap = snap;   // atomic reference publish
    }

    /// The current keymap frame (caller must not mutate). Capture once for a
    /// consistent 8-word scan.
    internal static ushort[] HostKeymapSnapshot() => _hostKeymap;

    /// Snapshot of the current keymap word `index` (0..7).
    public static ushort HostKeymapWord(int index)
    {
        var km = _hostKeymap;
        return (index >= 0 && index < 8) ? km[index] : (ushort)0;
    }

    // Draw command queue (title thread enqueues, host drains).
    // Each command is tagged with the render-target key it should draw
    // into (0 = the on-screen virtual target). The title thread sets
    // CurrentDrawTarget via SetPort; EnqueueDraw stamps the active target
    // onto the command so the drain can switch render targets IN ORDER
    // (Mac GWorld semantics: draw offscreen → CopyBits to screen → erase
    // offscreen, all in the original call sequence).
    private static readonly ConcurrentQueue<(int target, Action<Canvas> cmd)> _drawQueue = new();

    /// Gates the host's per-frame offscreen-game→screen flush. Set false by
    /// RunGameSessionLauncher at Enter-Ship entry and back true by RunMainGameLoop
    /// once the HUD panel + first scene are painted into the offscreen game buffer,
    /// so the host keeps showing the last good frame (the title) during the world
    /// build instead of flushing a half-drawn black buffer (the "radar briefly shows
    /// the star view" flash). Default true so direct-render paths are unaffected.
    public static volatile bool GameSceneReady = true;

    /// Pauses the per-frame offscreen-game→screen flush while an in-game MODAL owns the
    /// display and the game loop is BLOCKED — the galaxy map (RunGalaxyMapDialog). The flush
    /// is the port's stand-in for the game loop's RepaintGameWindow CopyBits; the original
    /// only runs that copy from the loop, so while the map modal blocks the loop the Mac
    /// never repaints the game window from the offscreen game GWorld. The map projects
    /// systems into that SAME offscreen (= _gameTarget) and CopyBits only the map sub-rect
    /// into its dialog buffer, exactly as the original does — so off-rect systems must stay
    /// hidden. Setting this stops the offscreen from being copied out (matching the
    /// loop-blocked Mac), so the frozen game/HUD keeps showing around the centred dialog
    /// and the off-rect scratch never leaks. Cleared when the modal closes (loop resumes).
    public static volatile bool SuspendGameSceneFlush;

    /// Active draw-target key for newly enqueued commands. 0 = screen
    /// (the virtual target). A non-zero value is a scratch-GWorld pixmap
    /// key with a writable Rgba8Image registered via RegisterRenderTarget.
    /// Set by SetPort (SetPortAndDevice / FUN_1007ab1c).
    public static volatile int CurrentDrawTarget;

    // Screen fade.
    // The Mac fades by ramping the hardware CLUT (SetEntries) over N steps;
    // that's meaningless in the game's true-colour renderer and the palette
    // subsystem is gated off. The faithful analogue is to modulate the
    // composited frame: clear the back buffer to FadeColor and draw the
    // virtual target at brightness FadeLevel (0 = full FadeColor, 1 = image).
    // FadeLevel is ramped synchronously on the title thread (like the Mac's
    // per-step CLUT loop) while the host composites it.
    private static volatile int _fadeLevelBits = 0x3f800000;  // 1.0f bit pattern (volatile float workaround)
    public static float FadeLevel
    {
        get => BitConverter.Int32BitsToSingle(_fadeLevelBits);
        set => _fadeLevelBits = BitConverter.SingleToInt32Bits(value);
    }
    public static RgbaColor FadeColor = RgbaColor.Black;

    // Mirrors the Mac device "faded" flag (GDevice+0x1a): fade-to-colour only
    // runs when NOT already faded (and sets it), fade-to-image only when faded
    // (and clears it). This pairs the toggle calls so a stray fade-out in a
    // draw routine is a no-op unless a fade-in preceded it.
    private static bool _screenFaded;

    /// PaletteFadeIn semantics (FUN_1005d148/FUN_1006fbac): ramp the screen TO the RGBColor
    /// at `fadeColorPtr` over `steps`. The pointer form is the faithful mechanism — the
    /// decompile passes the screen-fade colour CELL (_DAT_10080e00 = Palette.ScreenFadeCTab).
    /// Read the cell and run the same composite ramp as the explicit-RGBColor overload.
    /// The original never writes this cell, so ReadRGBColor returns black — the fade is
    /// always to black, matching every result the explicit-black overload produced.
    /// Safe against stranding: the _screenFaded paired flag makes a fade-to-colour a guarded
    /// no-op when the screen is already faded (so a caller nested in an outer ScreenFadeToColor
    /// doesn't double-fade), and every ptr-form caller has a reachable composite reveal.
    public static void ScreenFadeToColor(int steps, int fadeColorPtr)
    {
        ReadRGBColor(fadeColorPtr, out short r, out short g, out short b);   // ScreenFadeCTab cell → black
        ScreenFadeToColor(steps, r, g, b);                                  // composite ramp (with the _screenFaded guard)
    }

    /// Reset the screen fade to full brightness (image fully visible), no ramp. Used as
    /// a safety when entering a screen that won't ramp the fade back itself (e.g. the
    /// title after a game-exit fade-to-black), so a fade-to-black can never strand the
    /// composited screen at FadeLevel 0.
    public static void ClearScreenFade()
    {
        FadeLevel = 1f;
        _screenFaded = false;
    }

    // Managed overload: fade to an explicit Mac RGBColor (16-bit channels) — no pointer
    // deref, so callers need not stage the colour in EvoMemory.
    public static void ScreenFadeToColor(int steps, short red, short green, short blue)
    {
        if (_screenFaded) return;
        if (steps < 1) steps = 1;
        // Gamma.Correct (Mac DAC ramp, see Gamma.cs) keeps the fade/letterbox colour on the
        // same curve as the rest of the frame. Every fade in the game is to/from black, where
        // the curve is a fixed point (0->0), so this is a no-op in practice — applied only so
        // the rule "every directly-set display colour is gamma-corrected" has no exception.
        FadeColor = Gamma.Correct(new RgbaColor((byte)((red >> 8) & 0xff), (byte)((green >> 8) & 0xff), (byte)((blue >> 8) & 0xff)));
        for (int i = steps - 1; i >= 0; i--)
        {
            FadeLevel = (float)i / steps;
            Thread.Sleep(16);
        }
        FadeLevel = 0f;
        _screenFaded = true;
    }

    /// PaletteFadeOut semantics (FUN_1006fe50): ramp the screen back to the
    /// image over `steps`, clearing the faded flag. No-op if not faded.
    public static void ScreenFadeToImage(int steps)
    {
        if (!_screenFaded) return;
        if (steps < 1) steps = 1;
        for (int i = 1; i <= steps; i++)
        {
            FadeLevel = (float)i / steps;
            Thread.Sleep(16);
        }
        FadeLevel = 1f;
        _screenFaded = false;
    }

    // Cloak screen-palette remap.
    // The Mac cloak (EngageCloaking / FUN_1005d3c4) recolours the WHOLE display by
    // installing a preset colour table: at boot FUN_1005d1a8 built each preset by
    // remapping the standard 8-bit CLUT toward a hue with RemapToHSL (FUN_1007093c),
    // and engaging the cloak SetEntries-loads that table into the hardware CLUT and
    // MakeITable-rebuilds the inverse table — from then on EVERY pixel that reaches
    // the screen (sprite CopyBits across mismatched ctSeeds, QuickDraw RGB draws,
    // dialogs) resolves colour -> inverse-table nearest remapped entry -> that
    // entry's colour. The game's true-colour renderer has no hardware CLUT, so — same
    // pattern as the screen fade above — the faithful analogue is a present-time
    // per-pixel transform of the composited frame through exactly that pipeline.
    //
    // The remap per entry is hue × L(entry) with L = the UNSIGNED HSL lightness
    // (max+min)/2 (see RGB2HSLValue) — for a full-channel preset hue the composed
    // OS-glue chain (RGB2HSL + FixRatio/FixMul, all OS code outside the binary) is
    // the IDENTITY on that lightness: a GRADED ramp of the hue. Ground truth: the
    // SheepShaver planet-disc capture, per-pixel aligned 2026-07-10 — 28/28 Earth
    // art colours exact. (A prior SIGNED-lightness fold theory matched the greys
    // but inverted mixed colours ≥ 0x8000 — bright oceans on a dark planet.)
    //
    // TWO distinct pixel paths reach the cloaked screen (both SheepShaver-capture-
    // verified, 2026-07-03):
    //  · INDEXED content (sprites, PICT panel art — the game's blitter and CopyBits
    //    copy palette indices; the offscreen shares the device ctab): the CLUT swap
    //    retints each pixel IN PLACE to its own entry's remap, hue × L(entry) —
    //    Earth's dominant ocean blue (0,0x33,0x66) sits at L 0x3333, deep ocean
    //    (0,0,0x88) at L 0x4444, the dark-green nav-box art at L/2. Capture: nav
    //    box art (0,0x22,0) -> display 39 = L(0x1111).
    //  · RGB-DRAWN content (PaintRect fills, pens, text — QuickDraw resolves the
    //    colour via Color2Index over the REMAPPED table's inverse): out = nearest
    //    remapped VALUE, i.e. the hue-channel projection. Capture: the target-box
    //    interior REFILLED while cloaked with the same (0,0x22,0) -> display 63 =
    //    the 0x2222 level.
    // The port keeps that distinction via the Canvas alpha provenance tag
    // (Canvas.RgbDrawnTag: image blits carry 255, QuickDraw primitives 254) and
    // dispatches each pixel to the matching LUT. Nearest-matching runs in the Mac's
    // own colour space, so buffer pixels are Gamma.Uncorrect-ed first (they carry
    // the DAC ramp).
    //
    // LUTs: 32768 RGBA cells (5 bits per channel). The resolution and the
    // cell-CENTER representative are CALIBRATED against the SheepShaver capture: 4-bit
    // cells and/or cell-origin representatives mis-assign exact-palette art colours
    // (panel grays 0x111111/0x222222 must land on their own gray-ramp entries -> the
    // capture's 39/63 display levels; white must resolve to the white entry's full-hue
    // remap; adjacent 5-bit cells reproduce the capture's coexisting 102/110 levels).
    // Null when no remap is installed. _screenPaletteLutIndex = indexed-art path
    // (nearest ORIGINAL entry, output its remap); _screenPaletteLutRgb = RGB-drawn
    // path (nearest REMAPPED value).
    private static volatile byte[] _screenPaletteLutIndex;
    private static volatile byte[] _screenPaletteLutRgb;

    // Buffer display byte -> Mac 16-bit channel high 5 bits (inverse-table key).
    private static readonly byte[] _mac5Bit = BuildMac5BitTable();
    private static byte[] BuildMac5BitTable()
    {
        var t = new byte[256];
        for (int i = 0; i < 256; i++) t[i] = (byte)((Gamma.Uncorrect((byte)i) * 0x101) >> 11);
        return t;
    }

    /// Install the cloak screen-palette remap toward the preset hue (Mac 16-bit
    /// channels, e.g. (0,-1,0) = 0xFFFF green). Called by the ported preset
    /// installs (EngageCloaking / ReapplyCloakPalette) next to their faithful
    /// InstallScreenPalette call, mirroring how FadeIn pairs ScreenFadeToColor
    /// with the inert CLUT ramp.
    public static void ScreenPaletteRemap(short hueR, short hueG, short hueB)
    {
        // 1. Remap every entry of the standard 8-bit table toward the hue:
        //    entry -> hue × L(entry), L = unsigned HSL lightness (the capture-derived
        //    composite of FUN_1007093c's RGB2HSL + FixRatio/FixMul OS-glue chain —
        //    see the class note; FUN_100716e0 stores each component's high word).
        var ct = BuildDepthColorTable(8);
        int n = ct.Count;
        var remapped = new ushort[n * 3];
        for (int i = 0; i < n; i++)
        {
            long l = RGB2HSLValue(ct.R[i], ct.G[i], ct.B[i]);   // 0..0xFFFF
            remapped[i * 3 + 0] = (ushort)((l * (ushort)hueR) >> 16);
            remapped[i * 3 + 1] = (ushort)((l * (ushort)hueG) >> 16);
            remapped[i * 3 + 2] = (ushort)((l * (ushort)hueB) >> 16);
        }

        // 2. MakeITable equivalent, one search per path (see the class note): for each
        //    5-bit r.g.b cell (CENTRE representative, Euclidean over 16-bit channels,
        //    lowest index on ties):
        //     · lutIndex — the ORIGINAL entry nearest the cell (which palette index a
        //       source pixel of this colour IS), output = that entry's remap;
        //     · lutRgb — the entry whose REMAPPED value is nearest the cell (what
        //       Color2Index resolves through the post-install inverse table).
        var lutIndex = new byte[32768 * 4];
        var lutRgb = new byte[32768 * 4];
        for (int cell = 0; cell < 32768; cell++)
        {
            int cr = ((cell >> 10) & 0x1F) << 11 | 0x400;
            int cg = ((cell >> 5) & 0x1F) << 11 | 0x400;
            int cb = (cell & 0x1F) << 11 | 0x400;
            int bestOrig = 0; long bestOrigD = long.MaxValue;
            int bestRemap = 0; long bestRemapD = long.MaxValue;
            for (int i = 0; i < n; i++)
            {
                long dr = cr - (ushort)ct.R[i];
                long dg = cg - (ushort)ct.G[i];
                long db = cb - (ushort)ct.B[i];
                long d = dr * dr + dg * dg + db * db;
                if (d < bestOrigD) { bestOrigD = d; bestOrig = i; }
                dr = cr - remapped[i * 3 + 0];
                dg = cg - remapped[i * 3 + 1];
                db = cb - remapped[i * 3 + 2];
                d = dr * dr + dg * dg + db * db;
                if (d < bestRemapD) { bestRemapD = d; bestRemap = i; }
            }
            for (int c = 0; c < 3; c++)
            {
                lutIndex[cell * 4 + c] = Gamma.Correct((byte)(remapped[bestOrig * 3 + c] >> 8));
                lutRgb[cell * 4 + c] = Gamma.Correct((byte)(remapped[bestRemap * 3 + c] >> 8));
            }
            lutIndex[cell * 4 + 3] = 255;
            lutRgb[cell * 4 + 3] = 255;
        }
        _screenPaletteLutRgb = lutRgb;
        _screenPaletteLutIndex = lutIndex;
    }

    /// Drop the cloak remap. Called from Palette.InstallScreenPalette whenever a
    /// colour table is installed WITH SetEntries (apply != 0) — on the Mac that
    /// replaces the whole visible CLUT (the disengage path installs the default
    /// table); apply==0 installs never touched the hardware palette, so they
    /// leave the visible remap alone.
    public static void ScreenPaletteRestore()
    {
        _screenPaletteLutIndex = null;
        _screenPaletteLutRgb = null;
    }


    /// Present-time application: map every src pixel through the remap into dst,
    /// dispatching on the Canvas provenance tag (alpha 254 = QuickDraw-RGB-drawn ->
    /// inverse-table path; anything else = indexed-art path). Returns false (dst
    /// untouched) when no remap is installed. Called by the host's RenderFrame
    /// between window-layer compositing and the screen fade.
    public static bool ApplyScreenPaletteRemap(Rgba8Image src, Rgba8Image dst)
    {
        var lutIndex = _screenPaletteLutIndex;
        var lutRgb = _screenPaletteLutRgb;
        if (lutIndex == null || lutRgb == null) return false;
        var s = src.Pixels; var d = dst.Pixels;
        var m5 = _mac5Bit;
        for (int o = 0; o < s.Length; o += 4)
        {
            var lut = s[o + 3] == Canvas.RgbDrawnTag ? lutRgb : lutIndex;
            int cell = ((m5[s[o]] << 10) | (m5[s[o + 1]] << 5) | m5[s[o + 2]]) << 2;
            d[o] = lut[cell]; d[o + 1] = lut[cell + 1]; d[o + 2] = lut[cell + 2]; d[o + 3] = 255;
        }
        return true;
    }

    // Staging-only GWorld port keys (port+2) whose draws must be DISCARDED, not
    // fall back to the screen: the faithful sprite-sheet loader stages each PICT
    // into a real per-slot offscreen GWorld, but the game has no Rgba8Image for those
    // (CopyMask samples the host sprite cache instead) - without this, every
    // sheet PICT lands on the visible screen at boot ("all sprites stacked").
    private static readonly ConcurrentDictionary<int, byte> _discardKeys = new();
    public static void RegisterDiscardTarget(int portKey) => _discardKeys[portKey] = 1;
    public static void UnregisterDiscardTarget(int portKey) => _discardKeys.TryRemove(portKey, out _);

    /// Enqueue a Canvas action, tagged with the active draw target. Called by
    /// the title thread when it invokes a render primitive; executed on the
    /// host thread by DrainDrawQueue.
    public static void EnqueueDraw(Action<Canvas> cmd)
    {
        _drawQueue.Enqueue((CurrentDrawTarget, cmd));
        AdvanceDrawCompletion();
    }

    /// Enqueue a Canvas action against an explicit target key, independent of
    /// CurrentDrawTarget. Used by the host to seed an offscreen GWorld (e.g.
    /// paint the backdrop into the credits scratch buffer once at boot) and by
    /// CopyBits to land its blit in the destination buffer.
    public static void EnqueueDrawTo(int target, Action<Canvas> cmd)
    {
        _drawQueue.Enqueue((target, cmd));
        AdvanceDrawCompletion();
    }

    // Draw-queue tick atomicity (producer-thread-only fields; the boundary is published to
    // the host via Volatile.Write).
    // The game/title thread ticks at up to ~60 Hz (TickCount()-capped, same
    // as the original) and enqueues one whole visual frame — play-area
    // erase, then one blit per sprite — as a sequence of separate EnqueueDraw
    // calls. The host thread drains at its own, independently-clocked rate
    // (RenderFrame → DrainDrawQueue). Without a boundary, a drain landing
    // mid-sequence commits the erase but not yet the sprite redraws for that
    // tick — one host frame shows an emptied play area (ships flicker out),
    // caught up on the next frame. BeginDrawBatch/EndDrawBatch lets a
    // producer mark a run of enqueues that must drain as a single unit;
    // outside a batch, behaviour is unchanged (each item completes as soon
    // as it's enqueued, exactly as before this change).
    private static int _drawBatchDepth;
    private static long _drawEnqueuedCount;
    private static long _drawCompletedBoundary;
    private static long _drawDequeuedCount;

    private static void AdvanceDrawCompletion()
    {
        _drawEnqueuedCount++;
        if (_drawBatchDepth == 0) Volatile.Write(ref _drawCompletedBoundary, _drawEnqueuedCount);
    }

    /// Start a run of draw commands that must be drained by the host as one
    /// atomic unit. Must be paired with a bare EndDrawBatch call, same as the
    /// original's own control flow at each call site — no port-added
    /// try/finally wrapper (an exception here has no ported handler either,
    /// same as the original). Producer (game/title) thread only; nests.
    public static void BeginDrawBatch() => _drawBatchDepth++;

    /// Close a run started by BeginDrawBatch. Only when the outermost batch
    /// closes does the batch's draws become visible to DrainDrawQueue.
    public static void EndDrawBatch()
    {
        _drawBatchDepth--;
        if (_drawBatchDepth == 0) Volatile.Write(ref _drawCompletedBoundary, _drawEnqueuedCount);
    }

    /// A blocking dialog session entered MID-BATCH must not strand the batch open.
    /// RunMainGameLoop wraps each game tick in BeginDrawBatch/EndDrawBatch, and the
    /// tick itself opens dialogs that block it for their whole lifetime (TickShipAI →
    /// the spaceport hub on landing, the galaxy map on M, and their sub-dialogs — some
    /// of which, like the bar's slot machine and holo-vid news, run raw Button()-poll
    /// loops that never enter ModalDialog). With the depth still >0 nothing enqueued
    /// since BeginDrawBatch (the play-area blackout, the dialog chrome and items, every
    /// interaction repaint) ever crosses _drawCompletedBoundary: the host presents the
    /// pre-batch frame forever while the dialog runs invisibly underneath ("landing
    /// freezes the game"). GetNewDialog calls this when the window record is created —
    /// publish everything queued so far and zero the depth so the dialog's draws
    /// complete per-enqueue — and DisposeDialog restores the saved depth, keeping the
    /// interrupted tick's Begin/End pairing balanced (ModalDialog wraps its own loop
    /// too, a nested no-op). Producer thread only, same as Begin/EndDrawBatch.
    public static int SuspendDrawBatchForModal()
    {
        int depth = _drawBatchDepth;
        if (depth != 0)
        {
            _drawBatchDepth = 0;
            Volatile.Write(ref _drawCompletedBoundary, _drawEnqueuedCount);
        }
        return depth;
    }

    /// Restore the batch depth SuspendDrawBatchForModal zeroed (no-op when it was 0).
    public static void ResumeDrawBatchAfterModal(int depth) => _drawBatchDepth = depth;

    // Single reused Canvas — the drain is single-threaded (host thread only),
    // so one instance is rebound to each target buffer in command order.
    private static Canvas? _drainCanvas;

    /// Drain everything queued so far. Called once per frame from the host
    /// thread. Rebinds the Canvas to a target buffer whenever the next
    /// command's target key differs (Mac GWorld semantics: offscreen draws
    /// land in their buffer so a subsequent CopyBits (target 0) can sample
    /// them). `resolve` maps a non-zero target key to its Rgba8Image (null →
    /// fall back to the screen target).
    public static void DrainDrawQueue(Rgba8Image screenTarget, Func<int, Rgba8Image?> resolve)
    {
        var canvas = _drainCanvas ??= new Canvas();
        int boundKey = 0;
        canvas.Target = screenTarget;
        // Drain only the commands queued AS OF THIS CALL (a snapshot count), not
        // "until the queue is empty". A title-thread tracking loop — e.g.
        // HitTestTitleButton's `while (StillDown())`, which enqueues an orb blit
        // every iteration — produces commands faster than the software canvas
        // drains them; a `while (TryDequeue)` would then never return, the host
        // loop would never reach SampleInput again, FrameButtonDown would never
        // flip false, StillDown() would stay true forever, and the producer would
        // spin forever too: a hard livelock that froze the screen on the title
        // with no dialog (the "New/Open Pilot hangs the game" report). The GPU
        // host drained fast enough to hide it; software rendering does not.
        // Bounding to the entry-time count guarantees this returns each frame;
        // anything queued mid-drain rides to the next frame (FIFO order + the
        // persistent GWorld buffers keep Mac CopyBits semantics intact).
        //
        // Further bounded to _drawCompletedBoundary: a producer inside a
        // BeginDrawBatch/EndDrawBatch run (e.g. one game tick's erase + all
        // sprite redraws) hasn't finished advancing the boundary yet, so its
        // still-open batch's items are excluded from this frame's budget even
        // though they're already sitting in the queue — draining them only
        // once the whole batch has landed is what makes the tick atomic.
        long allowed = Volatile.Read(ref _drawCompletedBoundary) - _drawDequeuedCount;
        int budget = (int)Math.Min(_drawQueue.Count, Math.Max(0, allowed));
        while (budget-- > 0 && _drawQueue.TryDequeue(out var item))
        {
            _drawDequeuedCount++;
            if (_discardKeys.ContainsKey(item.target)) continue;   // staging-only GWorld
            if (item.target != boundKey)
            {
                canvas.Target = item.target == 0 ? screenTarget
                                                 : (resolve(item.target) ?? screenTarget);
                boundKey = item.target;
            }
            try { item.cmd(canvas); }
            catch (Exception ex) { Console.WriteLine($"[Bridge] draw cmd threw: {ex.Message}"); }
        }
    }

    // Host-installed resolvers.
    /// Invoked by RunGameSessionLauncher (FUN_10045778) at the top of Enter
    /// Ship. The host installs
    /// the render-world setup the original built at boot but the decompile couldn't
    /// resolve, plus the world-state activation and the offscreen-game-GWorld
    /// routing. Without it the loop runs but the sprite tables are empty.
    public static System.Action? OnEnterGameWorld;

    /// Host texture-wiring hook for one 'spïn' sheet's worth of frame records: LoadSpriteSheetsAndGWorlds
    /// (FUN_1001d634) calls this right after it builds each band's SpriteFrame table, passing
    /// (spinId, table, baseIdx, maxFrames); the host decodes the sheet and re-points each EXISTING
    /// record's ColorRef/Bounds at a real texture (AllocateSlotBitmapHeader's own ColorRef math is
    /// faithful but reads a slot-GWorld PixBase that's always 0 in the software renderer — the
    /// true-color-vs-CLUT gap). Returns the number of frames wired; null host = no texture (records
    /// stay ColorRef=0, sprite just doesn't draw — matches an unbacked GWorld faithfully).
    public static Func<int, int[], int, int, int>? WireSpriteBand;

    /// Host launcher for the standalone "Register EV Override" app. LaunchApplication-
    /// BySpec invokes it (when set) with the composed app name; the host Process.Starts
    /// the built OpenEV.Register exe and returns noErr(0) / an OSErr. Null → the old no-op
    /// stub path (LaunchApplication shim returns 0). This is the game→register launch glue.
    public static Func<string, int>? AppLauncher;

    /// Host URL opener for the Register app's "WWW Order" pane (SendStringToAregComponent —
    /// the original hands the clicked string to Internet Config's registered component, which
    /// launches it in the user's browser). The host Process.Starts the URL and returns noErr(0)
    /// / an OSErr. Null → the faithful original failure path (OpenRegistrationComponent's
    /// component-absent branch, i.e. "no Internet Config installed").
    public static Func<string, int>? UrlLauncher;

    /// PICT id → Rgba8Image resolver. Host installs from the TextureCache.
    /// Title thread asks for handles; we treat the pict id itself as the
    /// handle (non-zero = exists).
    public static Func<int, Rgba8Image?>? PictResolver;

    /// PICT id → handle resolver (Mac GetPicture trap). Returns the
    /// pict id itself when the PICT exists in the data, or 0 if missing.
    /// The ported transcriptions check `if (pict != 0)` to decide whether to draw.
    public static Func<int, int>? GetPictureImpl;

    /// Default UI face = classic Mac Geneva (font family ID 3): the title menu,
    /// pilot-info panel, and most UI text. EVO doesn't bundle Geneva, so the
    /// host loads a real Geneva or a substitute. Every TextFont(3) consumer
    /// resolves here via ResolveFont, so this one field fixes them all.
    public static SoftwareFont? Font;

    /// Mac SYSTEM font (family ID 0) = Chicago — what the Dialog Manager draws
    /// no-filter dialogs in (statText, button/checkbox titles, dialog TextEdit,
    /// all at 12), and what TextFont(0) selects via ResolveFont. The host wires
    /// the bundled free ChicagoFLF. Null → the pre-Chicago approximation: the
    /// Geneva `Font` above with a faux-bold +1px double-draw.
    public static SoftwareFont? SystemFont;

    /// Times face (font family ID 20 / TextFont(0x14)) — the About-EVÉ credits
    /// roll. Null → ResolveFont falls back to the default face.
    public static SoftwareFont? TimesFont;

    /// The game's one bundled custom face: "Sillycon" (FOND 2020 / sfnt 9295,
    /// font family ID 2020 / TextFont(0x7e4)). Null → ResolveFont falls back.
    public static SoftwareFont? SillyconFont;

    // Scratch GWorld pixmap registry (for CopyBits).
    // Mac CopyBits takes raw pixmap addresses for srcBits / dstBits.
    // The ported transcriptions compute these as `EvoMemory.ReadInt(addr) + 2`
    // (the +2 skips Mac's BitMap-handle-vs-baseAddr indirection). The host
    // registers each cached PICT Rgba8Image under the integer key the
    // CopyBits call site will produce; the impl looks up srcBits in this
    // dictionary and blits from that image onto the dst rect.
    private static readonly ConcurrentDictionary<int, Rgba8Image> _scratchPixmaps = new();

    /// Register an Rgba8Image as the pixmap source for `pixmapKey`. Pass
    /// null to remove.
    public static void SetScratchPixmap(int pixmapKey, Rgba8Image? texture)
    {
        if (texture is null) _scratchPixmaps.TryRemove(pixmapKey, out _);
        else                 _scratchPixmaps[pixmapKey] = texture;
    }

    /// Resolve a CopyBits srcBits/dstBits arg to its registered image,
    /// or null if no image is registered at that key.
    public static Rgba8Image? ResolveScratchPixmap(int pixmapKey)
        => _scratchPixmaps.TryGetValue(pixmapKey, out var tex) ? tex : null;

    // Writable offscreen-GWorld buffers.
    // A scratch GWorld that ported code *draws into* (then CopyBits to
    // screen) needs a real writable buffer. The host registers one per
    // offscreen pixmap key here. SetPort routes CurrentDrawTarget to a key
    // found here.
    private static readonly ConcurrentDictionary<int, Rgba8Image> _renderTargets = new();

    /// Register (or, with null, remove) a writable buffer for an offscreen
    /// GWorld pixmap key. The host owns the buffer's lifetime; this only
    /// records the association.
    public static void RegisterRenderTarget(int pixmapKey, Rgba8Image? target)
    {
        if (target is null) _renderTargets.TryRemove(pixmapKey, out _);
        else                _renderTargets[pixmapKey] = target;
    }

    /// Resolve a target key to its writable buffer, or null if the key is the
    /// screen / has no offscreen buffer.
    public static Rgba8Image? ResolveRenderTarget(int pixmapKey)
        => _renderTargets.TryGetValue(pixmapKey, out var rt) ? rt : null;

    /// Resolve a CopyBits srcBits pixmap key to a sampleable image —
    /// checking writable buffers first (the SCREEN/BACKDROP/ANIM GWorlds),
    /// then the static PICT scratch pixmaps (orb atlas, etc.). This is what
    /// makes the faithful multi-GWorld CopyBits work: e.g.
    /// `CopyBits(BACKDROP→SCREEN)` samples the offscreen buffer the title
    /// composed the backdrop into.
    public static Rgba8Image? ResolveTexture(int pixmapKey)
        => _renderTargets.TryGetValue(pixmapKey, out var rt) ? rt
         : (_scratchPixmaps.TryGetValue(pixmapKey, out var tex) ? tex : null);

    // Active ForeColor as RgbaColor. Mac ForeColor(0x21) = blackColor; many ports call
    // ForeColor before PaintRect. Keyed on the last ForeColor index; shares the
    // classic-QuickDraw colorConstant map with ForeColor (MapQuickDrawColorIndex).
    public static RgbaColor ResolveForeColor() => MapQuickDrawColorIndex(_foreColor);
}
