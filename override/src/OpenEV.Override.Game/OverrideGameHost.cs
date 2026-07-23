using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Silk.NET.SDL;
using Silk.NET.Maths;
using OpenEV.Platform.Imaging;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Boot;
using MK = OpenEV.Platform.Toolbox.MacKeycode;

namespace OpenEV.Override.Game;

// The game's software host. SDL2 owns the window / input / present; everything is drawn in pure C#
// into managed Rgba8Image GWorld buffers via the Canvas rasterizer (no GPU). The ~60 Hz loop
// (matched to the game's own TickCount tick cap, see RunMainGameLoop):
//   - samples mouse/keyboard each frame → MacToolbox FrameMouse / FrameButtonDown / the host KeyMap
//   - drains the Canvas command queue into the persistent virtual buffer (Mac GWorld semantics)
//   - composites the fade + letterbox and uploads one streaming texture
// The title thread (spawned in TitleAdapter.Setup) runs the transcribed TitleMainLoop with its
// original do/while WaitNextEvent loop intact.
internal sealed unsafe class OverrideGameHost : IDisposable
{
    // Virtual screen / play-area size, resolved from HostSettings at startup (default 800×600,
    // clamped ≥ 640×480) then held fixed — the original read the monitor gdRect ONCE at init and its
    // play area equalled that resolution. Written by Run() BEFORE the title thread spawns, so the
    // plain-int fields need no locking; every downstream layout derives from them.
    public static int VirtualWidth = 800;
    public static int VirtualHeight = 600;

    // GWorld pixmap CopyBits keys (= ReadInt(gworldSlot) + 2).
    private const int ScreenPixmapKey = MacToolbox.ScreenPixmapSentinel + 2;   // 0x1008f722
    private const int BackdropPixmapKey = 0x1008f6ee;
    private const int AnimPixmapKey = 0x1008f700;
    internal const int AnimPixmapSentinel = AnimPixmapKey - 2;                 // 0x1008f6fe
    private const int GamePixmapKey = MacToolbox.GamePixmapSentinel + 2;       // 0x1008f726

    private readonly string? _gameDir;
    private readonly HostSettings _settings;

    // Managed GWorld buffers (Mac offscreen GWorlds; persist across frames). Allocated in Run() by
    // AllocateBuffers once the resolution is resolved — NOT as field initializers, because for
    // `resolution = native` the size isn't known until after SDL video init queries the desktop mode.
    private Rgba8Image _virtualTarget = null!;
    private Rgba8Image _backdropTarget = null!;
    private Rgba8Image _animTarget = null!;
    private Rgba8Image _gameTarget = null!;
    private Rgba8Image _compose = null!;
    private Rgba8Image _paletteCompose = null!;   // cloak screen-palette remap output
    private readonly Canvas _hostCanvas = new();

    private readonly Sdl _sdl = Sdl.GetApi();
    private Window* _window;
    private Renderer* _renderer;
    private Texture* _texture;    // streaming upload of the CPU `present` buffer (sampled nearest)
    // Offscreen render target for sharp fractional scaling: the streaming texture is prescaled into
    // it by an exact integer multiple (nearest), then linear-downscaled onto the window. Recreated
    // only when the per-axis multiples change (see PresentScaled/TryEnsurePrescale).
    private Texture* _prescale;
    private int _prescaleNx, _prescaleNy;   // cached prescale multiples (0 = none allocated)
    private bool _targetTextureOk;           // renderer supports render-to-texture (else direct copy)
    private int _maxTexW, _maxTexH;          // renderer max texture size (0 = unknown/no limit)
    private bool _quit;

    private readonly ushort[] _keymapWords = new ushort[8];

    // Edge-latched input (fast-tap registration over RDP). SampleInput LEVEL-samples the mouse button
    // + keyboard once per ~16.7 ms frame. A physical press is held long enough that a sample catches
    // it, but a remote client (RDP from a phone) synthesizes very short down→up bursts that can fall
    // entirely between two samples and get dropped ("clicks take multiple tries to register").
    // PumpEvents latches each mouse-down / key-down EVENT into these; SampleInput ORs the latch with
    // the live held state so a press since the last sample registers for at least one frame, then
    // clears it. Host-thread only (PumpEvents and SampleInput run back-to-back), so no locking.
    private bool _mousePressedSinceSample;
    private int _mousePressX, _mousePressY;
    private readonly List<int> _keysPressedSinceSample = new();

    // internal, not public: HostSettings is host-internal and OverrideGameHost is only constructed
    // from Program.Main in this same assembly.
    internal OverrideGameHost(string? gameDir, HostSettings settings) { _gameDir = gameDir; _settings = settings; }

    public void Run()
    {
        // InitTimer raises the OS timer resolution (SDL calls timeBeginPeriod(1) on Windows). Without
        // it the default ~15.6 ms scheduler granularity makes both the host's per-frame _sdl.Delay and
        // the game thread's Thread.Sleep(1) frame-cap overshoot wildly → judder.
        if (_sdl.Init(Sdl.InitVideo | Sdl.InitEvents | Sdl.InitAudio | Sdl.InitTimer) != 0)
        {
            Console.WriteLine($"[host] SDL_Init failed: {_sdl.GetErrorS()}");
            return;
        }

        // No "EV Override" data folder next to the exe (e.g. run before unpacking the .sit). The
        // faithful Mac alert (DLOG 3000) lives inside that missing folder, so it can't be drawn.
        // APPROVED DEVIATION (2026-07-19): fall back to a host message box — allowed only because the
        // Mac dialog is genuinely unavailable with no app fork loaded. Shown before CreateWindow so no
        // blank window flashes; the early return unwinds to Program.Main, where Dispose null-guards
        // the (uncreated) window/renderer.
        if (_gameDir is null)
        {
            Console.WriteLine("[host] data folder not found — showing host alert and exiting.");
            unsafe
            {
                _sdl.ShowSimpleMessageBox((uint)MessageBoxFlags.Error, "Override",
                    OpenPluginResourceFiles.ErrDataFilesMissing, (Window*)null);
            }
            return;
        }

        // Resolve the play-area resolution from settings (host substrate + faithful restoration of the
        // full-monitor play area). AFTER SDL video init and BEFORE buffers/window/texture so
        // `resolution = native` can read the desktop mode. Resolved once and held fixed (the original
        // read the monitor gdRect once). Written before TitleAdapter.Setup spawns the title thread.
        int vw = _settings.Width, vh = _settings.Height;
        if (_settings.NativeResolution)
        {
            DisplayMode dm = default;
            if (_sdl.GetDesktopDisplayMode(0, ref dm) == 0 && dm.W > 0 && dm.H > 0)
            {
                vw = dm.W; vh = dm.H;
            }
            else
            {
                Console.WriteLine($"[host] GetDesktopDisplayMode failed: {_sdl.GetErrorS()}; " +
                                  $"falling back to {HostSettings.DefaultWidth}x{HostSettings.DefaultHeight}");
                vw = HostSettings.DefaultWidth; vh = HostSettings.DefaultHeight;
            }
        }
        vw = Math.Max(HostSettings.MinWidth, vw);
        vh = Math.Max(HostSettings.MinHeight, vh);

        VirtualWidth = vw;
        VirtualHeight = vh;

        AllocateBuffers(VirtualWidth, VirtualHeight);

        byte[] title = Encoding.UTF8.GetBytes("Escape Velocity: Override");
        // ALLOW_HIGHDPI: on a Retina Mac this makes the renderer's backing store the FULL physical
        // pixel resolution (drawable = 2× the window points) instead of a point-resolution buffer the
        // OS then bilinear-upscales — that upscale is what made integer scaling look antialiased. Host
        // presentation substrate; inert on Windows/Linux (DPI-unaware → drawable == window). See the
        // pixel-rect conversion in RenderFrameToWindow / ToDrawablePixels.
        var winFlags = WindowFlags.Resizable | WindowFlags.Shown | WindowFlags.AllowHighdpi;
        if (_settings.Fullscreen) winFlags |= WindowFlags.FullscreenDesktop;   // borderless fullscreen
        fixed (byte* tp = title)
        {
            _window = _sdl.CreateWindow(tp, Sdl.WindowposCentered, Sdl.WindowposCentered,
                VirtualWidth, VirtualHeight, (uint)winFlags);
        }
        if (_window == null) { Console.WriteLine($"[host] CreateWindow failed: {_sdl.GetErrorS()}"); return; }

        // The Win/GUI key is a game modifier now (→ the Mac option key, see ModifierKeyTable): grab
        // the keyboard while the window is focused so pressing it reaches the game instead of popping
        // the Start menu. Alt+Tab stays with the OS (SDL default); the grab releases with focus.
        _sdl.SetWindowKeyboardGrab(_window, SdlBool.True);

        SetWindowIconFromFinderIcon();

        // Keep EVO's QuickDraw analogue in the managed software buffers, but let SDL use the GPU for
        // the final texture upload/scale/present when it can — forcing SDL's software renderer makes
        // large play-area resolutions crawl (the last full-frame scale runs on the CPU too). Fall back
        // to software so headless / minimal-driver environments keep the old behavior.
        _renderer = _sdl.CreateRenderer(_window, -1, (uint)RendererFlags.Accelerated);
        if (_renderer == null)
        {
            Console.WriteLine($"[host] accelerated renderer unavailable: {_sdl.GetErrorS()}; falling back to software.");
            _renderer = _sdl.CreateRenderer(_window, -1, (uint)RendererFlags.Software);
        }
        if (_renderer == null) { Console.WriteLine($"[host] CreateRenderer failed: {_sdl.GetErrorS()}"); return; }

        // Renderer caps used by the sharp-scaling path: whether it can render into a texture, and the
        // max texture size (0 = don't clamp) so the integer prescale target can't exceed it.
        RendererInfo ri = default;
        if (_sdl.GetRendererInfo(_renderer, ref ri) == 0)
        {
            _targetTextureOk = (ri.Flags & (uint)RendererFlags.Targettexture) != 0;
            _maxTexW = ri.MaxTextureWidth;
            _maxTexH = ri.MaxTextureHeight;
        }

        _texture = _sdl.CreateTexture(_renderer, (uint)PixelFormatEnum.Abgr8888,
            (int)TextureAccess.Streaming, VirtualWidth, VirtualHeight);
        if (_texture == null) { Console.WriteLine($"[host] CreateTexture failed: {_sdl.GetErrorS()}"); return; }
        // Crisp by default; PresentScaled overrides per frame only when it needs a smooth downscale.
        _sdl.SetTextureScaleMode(_texture, ScaleMode.Nearest);

        // Register the GWorld buffers under their CopyBits pixmap keys so SetPort can route primitives
        // and CopyBits can resolve src/dst (Mac GWorld model).
        MacToolbox.RegisterRenderTarget(ScreenPixmapKey, _virtualTarget);
        MacToolbox.RegisterRenderTarget(BackdropPixmapKey, _backdropTarget);
        MacToolbox.RegisterRenderTarget(AnimPixmapKey, _animTarget);
        MacToolbox.RegisterRenderTarget(GamePixmapKey, _gameTarget);

        Console.WriteLine($"[software host] gameDir = {_gameDir ?? "(not found)"}");
        Console.WriteLine($"[software host] Window {VirtualWidth}x{VirtualHeight} ready " +
            $"({(_settings.Fullscreen ? "fullscreen" : "windowed")}, scaling={_settings.Scaling}" +
            $"{(_settings.Scaling == ScalingMode.Integer && _settings.FixedScale > 0 ? $" x{_settings.FixedScale.ToString(CultureInfo.InvariantCulture)}" : "")}).");
        // Confirms whether ALLOW_HIGHDPI took effect (Retina → drawable is 2× the window points).
        {
            int wpt = 0, hpt = 0, wpx = 0, hpx = 0;
            _sdl.GetWindowSize(_window, ref wpt, ref hpt);
            _sdl.GetRendererOutputSize(_renderer, ref wpx, ref hpx);
            double ratio = wpt > 0 ? wpx / (double)wpt : 1.0;
            Console.WriteLine($"[software host] window {wpt}x{hpt} pts, drawable {wpx}x{hpx} px " +
                $"({ratio.ToString("0.##", CultureInfo.InvariantCulture)}× HiDPI).");
        }

        TitleAdapter.Setup(_gameDir);
        _sdl.StartTextInput();
        // OS clipboard bridge for the dialog TextEdit fields' scrap (the modal loop's Ctrl-X/C/V).
        MacToolbox.HostClipboardGet = GetHostClipboard;
        MacToolbox.HostClipboardSet = SetHostClipboard;

        var sw = Stopwatch.StartNew();
        // Matches the game's own TickCount()-capped tick rate (RunMainGameLoop) — the original has no
        // separate present step (QuickDraw drew directly in the same tick loop), so tracking its ~60 Hz
        // cap is the closest approximation. Scheduled against a running due-time (nextFrameDue) rather
        // than "elapsed since this iteration started" so a single overshoot can't compound into
        // permanent drift the way re-measuring from "now" every iteration would.
        const double FrameMs = 1000.0 / 60.0;
        double nextFrameDue = 0;
        while (!_quit)
        {
            PumpEvents();
            if (_quit || EvoGlobals.QuitRequested) break;
            SampleInput();
            RenderFrame();

            nextFrameDue += FrameMs;
            double now = sw.Elapsed.TotalMilliseconds;
            int sleep = (int)(nextFrameDue - now);
            if (sleep > 0) _sdl.Delay((uint)sleep);
            else nextFrameDue = now;  // fell more than a frame behind — resync instead of bursting catch-up frames
        }

        TitleAdapter.Stop();
    }

    // GetClipboardTextS returns "" when the clipboard holds no text.
    private string? GetHostClipboard() => _sdl.GetClipboardTextS();

    private void SetHostClipboard(string s)
    {
        byte[] t = Encoding.UTF8.GetBytes(s + "\0");
        fixed (byte* p = t) _sdl.SetClipboardText(p);
    }

    private void PumpEvents()
    {
        Event e = default;
        while (_sdl.PollEvent(&e) != 0)
        {
            switch ((EventType)e.Type)
            {
                case EventType.Quit:
                    _quit = true;
                    EvoGlobals.QuitRequested = true;
                    break;
                case EventType.Keydown:
                    {
                        // ESC is delivered to the game keymap like any other key (the game uses it for
                        // in-game back-out / dialog cancel); exit is via the window close box
                        // (EventType.Quit above) and the in-game Quit command.
                        // Latch the initial key-down (not OS auto-repeat) so a fast tap released before
                        // the next SampleInput still registers for a frame.
                        if (e.Key.Repeat == 0)
                        {
                            _keysPressedSinceSample.Add((int)e.Key.Keysym.Scancode);
                            // Return/Enter never arrive via SDL TextInput below (SDL doesn't emit it
                            // for control keys), so ModalDialog's typed-char queue — the only path a
                            // dialog's Return-dismiss / filter keyDown ever sees — never got them.
                            // Feed the two distinct Mac charCodes directly: Return sends 0x0D, the
                            // separate numpad Enter sends 0x03 on a real Mac keyboard.
                            Scancode sc = e.Key.Keysym.Scancode;
                            if (sc == Scancode.ScancodeReturn || sc == Scancode.ScancodeReturn2)
                            {
                                MacToolbox.FrameTextInput.Add('\r');
                                MacToolbox.EnqueueTypedChar('\r');
                            }
                            else if (sc == Scancode.ScancodeKPEnter)
                            {
                                MacToolbox.FrameTextInput.Add((char)3);
                                MacToolbox.EnqueueTypedChar((char)3);
                            }
                        }
                        // Dialog TextEdit keys (New Pilot name field / confirm-text alert): editing
                        // keys generate NO SDL TextInput, and SDL suppresses TextInput while Ctrl is
                        // down — synthesize the Mac keyDown charcodes the modal loop's TE path expects,
                        // on auto-repeats too (the Mac autoKey stream). Gated on an open dialog so the
                        // title/game loops' keymap-driven char stream is untouched.
                        if (MacToolbox.HasOpenDialog)
                        {
                            int km = (int)e.Key.Keysym.Mod, sym = (int)e.Key.Keysym.Sym;
                            short mods = 0;
                            if ((km & 0x0003) != 0) mods |= MacToolbox.MacShiftKeyBit;   // KMOD_SHIFT
                            // Dialog edit shortcuts ride physical CTRL (the desktop Ctrl-C/V convention,
                            // per the user's ask) — distinct from the game keymap's physical-position
                            // modifiers, where Alt is the Mac command key (ModifierKeyTable).
                            if ((km & 0x00c0) != 0) mods |= MacToolbox.MacCmdKeyBit;     // KMOD_CTRL
                            if ((mods & MacToolbox.MacCmdKeyBit) != 0)
                            {
                                // Ctrl+letter (the std-filter Cmd-X/C/V/A edit equivalents).
                                if (sym >= 'a' && sym <= 'z' && e.Key.Repeat == 0)
                                    MacToolbox.EnqueueTypedKey((char)sym, mods);
                            }
                            else
                            {
                                char ec = e.Key.Keysym.Scancode switch
                                {
                                    Scancode.ScancodeReturn => '\r',
                                    Scancode.ScancodeKPEnter => (char)3,     // Mac Enter-key charcode
                                    Scancode.ScancodeBackspace => '\b',
                                    Scancode.ScancodeDelete => (char)127,   // forward delete
                                    Scancode.ScancodeTab => '\t',
                                    Scancode.ScancodeLeft => (char)0x1c,
                                    Scancode.ScancodeRight => (char)0x1d,
                                    Scancode.ScancodeUp => (char)0x1e,
                                    Scancode.ScancodeDown => (char)0x1f,
                                    _ => '\0',
                                };
                                if (ec != '\0') MacToolbox.EnqueueTypedKey(ec, mods);
                            }
                        }
                        break;
                    }
                case EventType.Mousebuttondown:
                    if (e.Button.Button == 1)   // SDL_BUTTON_LEFT
                    {
                        _mousePressedSinceSample = true;
                        _mousePressX = e.Button.X;
                        _mousePressY = e.Button.Y;
                    }
                    break;
                case EventType.Textinput:
                    for (int i = 0; i < 32 && e.Text.Text[i] != 0; i++)
                    {
                        char c = (char)e.Text.Text[i];   // ASCII fast path (EVO name fields)
                        if (c is >= ' ' and < (char)127)
                        {
                            MacToolbox.FrameTextInput.Add(c);
                            MacToolbox.EnqueueTypedChar(c);
                        }
                    }
                    break;
            }
        }
    }

    private void SampleInput()
    {
        bool focused = ((WindowFlags)_sdl.GetWindowFlags(_window)).HasFlag(WindowFlags.InputFocus);

        // Mouse → virtual coords; gate on focus (classic Mac delivers clicks only to the front
        // process). Level-sampled button state is OR'd with a latched press so a fast tap released
        // before this sample still registers — at its down point, not wherever the cursor drifted.
        int mx = 0, my = 0;
        uint buttons = _sdl.GetMouseState(ref mx, ref my);
        bool liveHeld = focused && (buttons & 1u) != 0;          // SDL_BUTTON_LMASK
        bool tapped = focused && _mousePressedSinceSample;
        int useX = mx, useY = my;
        if (!liveHeld && tapped) { useX = _mousePressX; useY = _mousePressY; }
        var v = WindowToVirtual(useX, useY);
        MacToolbox.FrameMouse = new MPoint((short)v.Y, (short)v.X);
        MacToolbox.FrameButtonDown = liveHeld || tapped;
        _mousePressedSinceSample = false;

        // FrameModifiers (incl. the shiftKey bit the modal loop's shift-click selection reads) is
        // accumulated below from the keymap walk, which owns the physical-position modifier mapping.

        // Hardware KeyMap (8 shorts, bit = Mac keycode held), indexed by keycode XOR 0x08 (big-endian
        // word byte order). A key is set if it's live-held OR was tap-latched since the last sample.
        Array.Clear(_keymapWords, 0, _keymapWords.Length);
        int mods = 0;
        if (focused)
        {
            byte* ks = _sdl.GetKeyboardState(null);
            // Caps Lock LATCHES on classic Mac hardware: its KeyMap bit stays set while the lock is ON,
            // so the game — which polls the bit every frame (the Caps-Lock 2x game speed in
            // RunMainGameLoop, EVO keycode 0x31) — treats it as a sticky toggle. SDL reports only the
            // MOMENTARY physical press, which made the toggle act as hold-to-speed-up. Drive its bit
            // from the lock state (KMOD_CAPS = 0x2000) instead to restore the Mac latch behaviour.
            bool capsLocked = ((int)_sdl.GetModState() & 0x2000) != 0;
            foreach (var (sc, key) in KeyMapTable)
            {
                bool held = sc == Scancode.ScancodeCapslock
                    ? capsLocked
                    : ks[(int)sc] != 0 || _keysPressedSinceSample.Contains((int)sc);
                if (!held) continue;
                // Mac EventRecord modifier bits for FrameModifiers — cmdKey 0x100 / shiftKey 0x200 /
                // alphaLock 0x400 / optionKey 0x800 / controlKey 0x1000, plus the right-side bits
                // (0x2000/0x4000) an extended ADB keyboard reported for its right shift/option.
                switch (key)
                {
                    case MK.Command: mods |= 0x0100; break;
                    case MK.Shift: mods |= 0x0200; break;
                    case MK.RightShift: mods |= 0x0200 | 0x2000; break;
                    case MK.CapsLock: mods |= 0x0400; break;
                    case MK.Option: mods |= 0x0800; break;
                    case MK.RightOption: mods |= 0x0800 | 0x4000; break;
                    case MK.Control: mods |= 0x1000; break;
                }
                int mac = (int)key;
                if (mac is >= 0 and < 128)
                {
                    mac ^= 0x08;
                    _keymapWords[mac >> 4] |= (ushort)(1 << (mac & 0xf));
                }
            }
        }
        _keysPressedSinceSample.Clear();
        MacToolbox.SetHostKeymap(_keymapWords);
        if (!MacToolbox.FrameButtonDown) mods |= 0x0080;   // btnState: set = button UP (Mac EventRecord semantics)
        MacToolbox.FrameModifiers = mods;
    }

    // SDL scancode → classic Mac virtual keycode. Covers the keys EVO can bind; unmapped scancodes are
    // absent. Modifier rows that differ per host keyboard live in ModifierKeyTable below.
    private static readonly (Scancode, MK)[] CommonKeyTable =
    {
        (Scancode.ScancodeA, MK.A), (Scancode.ScancodeS, MK.S), (Scancode.ScancodeD, MK.D), (Scancode.ScancodeF, MK.F),
        (Scancode.ScancodeH, MK.H), (Scancode.ScancodeG, MK.G), (Scancode.ScancodeZ, MK.Z), (Scancode.ScancodeX, MK.X),
        (Scancode.ScancodeC, MK.C), (Scancode.ScancodeV, MK.V), (Scancode.ScancodeB, MK.B), (Scancode.ScancodeQ, MK.Q),
        (Scancode.ScancodeW, MK.W), (Scancode.ScancodeE, MK.E), (Scancode.ScancodeR, MK.R), (Scancode.ScancodeY, MK.Y),
        (Scancode.ScancodeT, MK.T), (Scancode.ScancodeO, MK.O), (Scancode.ScancodeU, MK.U), (Scancode.ScancodeI, MK.I),
        (Scancode.ScancodeP, MK.P), (Scancode.ScancodeL, MK.L), (Scancode.ScancodeJ, MK.J), (Scancode.ScancodeK, MK.K),
        (Scancode.ScancodeN, MK.N), (Scancode.ScancodeM, MK.M),
        (Scancode.Scancode1, MK.D1), (Scancode.Scancode2, MK.D2), (Scancode.Scancode3, MK.D3), (Scancode.Scancode4, MK.D4),
        (Scancode.Scancode5, MK.D5), (Scancode.Scancode6, MK.D6), (Scancode.Scancode7, MK.D7), (Scancode.Scancode8, MK.D8),
        (Scancode.Scancode9, MK.D9), (Scancode.Scancode0, MK.D0),
        (Scancode.ScancodeMinus, MK.Minus), (Scancode.ScancodeEquals, MK.Equal),
        (Scancode.ScancodeLeftbracket, MK.LeftBracket), (Scancode.ScancodeRightbracket, MK.RightBracket),
        (Scancode.ScancodeBackslash, MK.Backslash), (Scancode.ScancodeSemicolon, MK.Semicolon),
        (Scancode.ScancodeApostrophe, MK.Quote), (Scancode.ScancodeGrave, MK.Grave),
        (Scancode.ScancodeComma, MK.Comma), (Scancode.ScancodePeriod, MK.Period), (Scancode.ScancodeSlash, MK.Slash),
        (Scancode.ScancodeReturn, MK.Return), (Scancode.ScancodeTab, MK.Tab), (Scancode.ScancodeSpace, MK.Space),
        (Scancode.ScancodeBackspace, MK.Delete), (Scancode.ScancodeEscape, MK.Escape),
        (Scancode.ScancodeLshift, MK.Shift), (Scancode.ScancodeRshift, MK.RightShift), (Scancode.ScancodeCapslock, MK.CapsLock),
        (Scancode.ScancodeLctrl, MK.Control), (Scancode.ScancodeRctrl, MK.Control),
        (Scancode.ScancodeF1, MK.F1), (Scancode.ScancodeF2, MK.F2), (Scancode.ScancodeF3, MK.F3), (Scancode.ScancodeF4, MK.F4),
        (Scancode.ScancodeHome, MK.Home), (Scancode.ScancodePageup, MK.PageUp), (Scancode.ScancodeDelete, MK.ForwardDelete),
        (Scancode.ScancodeEnd, MK.End), (Scancode.ScancodePagedown, MK.PageDown),
        (Scancode.ScancodeLeft, MK.LeftArrow), (Scancode.ScancodeRight, MK.RightArrow),
        (Scancode.ScancodeDown, MK.DownArrow), (Scancode.ScancodeUp, MK.UpArrow),
    };

    // Host modifier keys → Mac modifier keycodes. A PC board has no option/command keys, so the Mac
    // modifier row maps by PHYSICAL POSITION — control|option|command sat where Ctrl|Win|Alt sit:
    // Ctrl → control (CommonKeyTable), Win → option (right Win → right option), Alt → command. A Mac
    // host's SDL reports the real keys (Alt = option, GUI = command), so there the labels map directly.
    private static readonly (Scancode, MK)[] ModifierKeyTable = OperatingSystem.IsMacOS()
        ? new[]
        {
            (Scancode.ScancodeLalt, MK.Option), (Scancode.ScancodeRalt, MK.RightOption),
            (Scancode.ScancodeLgui, MK.Command), (Scancode.ScancodeRgui, MK.Command),
        }
        : new[]
        {
            (Scancode.ScancodeLgui, MK.Option), (Scancode.ScancodeRgui, MK.RightOption),
            (Scancode.ScancodeLalt, MK.Command), (Scancode.ScancodeRalt, MK.Command),
        };

    // Must stay textually below the two source tables (static init order).
    private static readonly (Scancode, MK)[] KeyMapTable = [.. CommonKeyTable, .. ModifierKeyTable];

    // Allocate the 6 persistent GWorld buffers at the resolved play-area size. Called once from Run()
    // after the resolution is known (see the field declarations for why they aren't field initializers).
    private void AllocateBuffers(int w, int h)
    {
        _virtualTarget = new Rgba8Image(w, h);
        _backdropTarget = new Rgba8Image(w, h);
        _animTarget = new Rgba8Image(w, h);
        _gameTarget = new Rgba8Image(w, h);
        _compose = new Rgba8Image(w, h);
        _paletteCompose = new Rgba8Image(w, h);
    }

    private void RenderFrame()
    {
        // Drain the title thread's queued draw commands. While the game world is active the in-game
        // frame drains into the offscreen game GWorld (_gameTarget), flushed onto the visible port
        // below (the faithful per-frame offscreen→screen CopyBits); otherwise commands hit _virtualTarget.
        bool inGame = EvoGlobals.GameWorldActive;
        var drainTarget = inGame ? _gameTarget : _virtualTarget;
        MacToolbox.DrainDrawQueue(drainTarget, MacToolbox.ResolveRenderTarget);

        // Flush the offscreen game buffer to screen only once the in-game render has painted a full
        // frame (HUD panel + scene). During the Enter-Ship setup gap — GameWorldActive flips true at
        // the top of the world build, but the sprite-table build runs first and the HUD/radar panel is
        // painted later by RunMainGameLoop's RefreshStatusPanel — _gameTarget is still a half-drawn
        // BLACK buffer; flushing it flashed a black radar box over the bare starfield.
        // RunGameSessionLauncher clears GameSceneReady at entry, RunMainGameLoop sets it once the panel
        // is up; until then we keep presenting the last good frame. Open by default so the direct-render
        // paths (intro/galaxy-map) are unaffected.
        //
        // This flush IS the game loop's per-frame offscreen→screen CopyBits (RepaintGameWindow /
        // FUN_1005ff4c). The original runs that copy only from the game loop — so while an in-game MODAL
        // blocks the loop (the galaxy map), the Mac never repaints the game window from the offscreen.
        // SuspendGameSceneFlush replicates that: the map sets it so we stop copying the offscreen out
        // while it owns the display (frozen game shows; the dialog composites on top).
        if (inGame && MacToolbox.GameSceneReady && !MacToolbox.SuspendGameSceneFlush)
        {
            _hostCanvas.Target = _virtualTarget;
            _hostCanvas.Blit(_gameTarget, new RectI(0, 0, VirtualWidth, VirtualHeight), RgbaColor.White);
        }

        // Window-layer compositor: each open Mac dialog drew into its own backing buffer (registered
        // at handle+2); layer them over the scene back-to-front, blitting each window's screen rect.
        // The blacked play-area surround stays in the scene buffer underneath — only the dialog rects
        // are layers. (No open dialog → empty snapshot → no-op.)
        var layers = MacToolbox.SnapshotVisibleWindows();
        if (layers.Length > 0)
        {
            _hostCanvas.Target = _virtualTarget;
            foreach (var ly in layers)
            {
                var r = new RectI(ly.Left, ly.Top, ly.Right - ly.Left, ly.Bottom - ly.Top);
                _hostCanvas.Blit(ly.Buffer, r, r, RgbaColor.White);
            }
        }

        // Cloak screen-palette remap: while the cloak's preset palette is installed, every presented
        // pixel resolves through the remapped CLUT + inverse table exactly as on the Mac. Applied to
        // the WHOLE composite (scene + dialog layers — the Mac CLUT was device-wide), out-of-place so
        // the persistent _virtualTarget keeps its true colours for later un-cloaked frames. Ordered
        // BEFORE the fade, like the Mac's SetEntries ramp over already-remapped entries.
        var frame = MacToolbox.ApplyScreenPaletteRemap(_virtualTarget, _paletteCompose)
            ? _paletteCompose
            : _virtualTarget;

        // Screen fade: present the virtual buffer at FadeLevel brightness over a FadeColor clear.
        // fade >= 1 is the common case (no compose).
        var fc = MacToolbox.FadeColor;
        float fade = MacToolbox.FadeLevel;
        Rgba8Image present;
        if (fade >= 0.999f)
        {
            present = frame;
        }
        else
        {
            _hostCanvas.Target = _compose;
            _hostCanvas.Clear(fc);
            _hostCanvas.Blit(frame, new RectI(0, 0, VirtualWidth, VirtualHeight),
                RgbaColor.White.Scale(fade));
            present = _compose;
        }

        // Upload + present (letterboxed, centered with FadeColor border).
        fixed (byte* p = present.Pixels)
            _sdl.UpdateTexture(_texture, (Rectangle<int>*)null, p, VirtualWidth * 4);

        // Compute the letterbox in window POINTS (shared with WindowToVirtual's mouse mapping), then
        // stretch that rect to the physical DRAWABLE so the blit renders at full pixel density. On
        // Retina the drawable is 2× the window points, keeping integer scaling crisp; on non-HiDPI
        // hosts drawable == window (identity).
        int winW = 0, winH = 0, pxW = 0, pxH = 0;
        _sdl.GetWindowSize(_window, ref winW, ref winH);
        _sdl.GetRendererOutputSize(_renderer, ref pxW, ref pxH);   // query BEFORE PresentScaled binds a target
        var dst = ToDrawablePixels(ComputeLetterbox(winW, winH), winW, winH, pxW, pxH);
        PresentScaled(dst, fc);
        _sdl.RenderPresent(_renderer);

        MacToolbox.FrameTextInput.Clear();
    }

    // Window/taskbar icon (host substrate — Mac windows had no icons; this is the icon the FINDER
    // showed for the app on the desktop). Resolved exactly as the Finder did, from the app's own
    // fork (never plug-in-shadowed): BNDL → the FREF with file type 'APPL' → its local icon id →
    // the BNDL's ICN# map → icon family id; then icl8 (32×32 8-bit, canonical system CLUT) punched
    // through the ICN# mask (its second 128 bytes). EVO 1.0.2 resolves to family 141, the Override
    // crystal on the black diamond. Channels run through Gamma.Correct like every Mac color shown.
    // Any missing piece just keeps the executable's default icon.
    private void SetWindowIconFromFinderIcon()
    {
        const uint TypeBndl = 0x424E444C, TypeFref = 0x46524546, TypeIcnMask = 0x49434E23,   // 'BNDL' 'FREF' 'ICN#'
                   TypeIcl8 = 0x69636C38, TypeAppl = 0x4150504C;                              // 'icl8' 'APPL'
        byte[]? fork = OpenEV.Platform.EvoData.OverrideDataLoader.LoadResourceFork(_gameDir!, "EV Override");
        if (fork is null) return;

        byte[]? bndl = null;
        var byTypeId = new Dictionary<(uint type, short id), byte[]>();
        foreach (var res in OpenEV.Platform.ResourceFork.MacResourceFork.Read(fork))
        {
            byTypeId[(res.RawType, res.Id)] = res.Data;
            if (res.RawType == TypeBndl) bndl ??= res.Data;
        }
        if (bndl is null || bndl.Length < 8) return;

        static ushort Be16(byte[] d, int o) => (ushort)((d[o] << 8) | d[o + 1]);
        static uint Be32(byte[] d, int o) => ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];

        // BNDL: signature(4) versId(2) typeCount-1(2), then per type: OSType(4) count-1(2)
        // [localId(2) resId(2)]×count. Collect the FREF and ICN# mapping arrays.
        var frefIds = new List<short>();
        var icnPairs = new List<(ushort local, short resId)>();
        int typeCount = Be16(bndl, 6) + 1;
        int p = 8;
        for (int t = 0; t < typeCount && p + 6 <= bndl.Length; t++)
        {
            uint osType = Be32(bndl, p);
            int n = Be16(bndl, p + 4) + 1;
            for (int k = 0; k < n && p + 10 + k * 4 <= bndl.Length; k++)
            {
                if (osType == TypeFref) frefIds.Add((short)Be16(bndl, p + 8 + k * 4));
                else if (osType == TypeIcnMask) icnPairs.Add((Be16(bndl, p + 6 + k * 4), (short)Be16(bndl, p + 8 + k * 4)));
            }
            p += 6 + n * 4;
        }

        short famId = -1;
        foreach (short frefId in frefIds)
        {
            // FREF: fileType(4) localIconId(2) fileName(pstr).
            if (byTypeId.TryGetValue((TypeFref, frefId), out var fref) && fref.Length >= 6 && Be32(fref, 0) == TypeAppl)
            {
                ushort localIcon = Be16(fref, 4);
                foreach (var (local, resId) in icnPairs)
                    if (local == localIcon) { famId = resId; break; }
                break;
            }
        }
        if (famId < 0 ||
            !byTypeId.TryGetValue((TypeIcl8, famId), out var icl8) || icl8.Length < 1024 ||
            !byTypeId.TryGetValue((TypeIcnMask, famId), out var icn) || icn.Length < 256)
            return;

        int clut = MacToolbox.GetCTable(8);
        var rgba = new byte[32 * 32 * 4];
        for (int i = 0; i < 32 * 32; i++)
        {
            MacToolbox.GetColorTableRGB(clut, icl8[i], out short r, out short g, out short b);
            rgba[i * 4 + 0] = Gamma.Correct((byte)((ushort)r >> 8));
            rgba[i * 4 + 1] = Gamma.Correct((byte)((ushort)g >> 8));
            rgba[i * 4 + 2] = Gamma.Correct((byte)((ushort)b >> 8));
            rgba[i * 4 + 3] = (byte)((icn[128 + (i >> 3)] & (0x80 >> (i & 7))) != 0 ? 255 : 0);
        }
        fixed (byte* px = rgba)
        {
            Surface* icon = _sdl.CreateRGBSurfaceWithFormatFrom(px, 32, 32, 32, 32 * 4, (uint)PixelFormatEnum.Abgr8888);
            if (icon != null)
            {
                _sdl.SetWindowIcon(_window, icon);   // SDL copies the surface contents
                _sdl.FreeSurface(icon);
                Console.WriteLine($"[host] window icon set from app-fork Finder icon (family {famId}).");
            }
        }
    }

    private Vector2D<int> WindowToVirtual(int wx, int wy)
    {
        // SDL mouse events are in window POINTS, so invert the point-space letterbox directly — no
        // HiDPI conversion here (that lives only in the present path). Keeps hit-testing exact.
        int outW = 0, outH = 0;
        _sdl.GetWindowSize(_window, ref outW, ref outH);
        var dst = ComputeLetterbox(outW, outH);
        int vx = dst.Size.X <= 0 ? 0 : (int)((wx - dst.Origin.X) * (double)VirtualWidth / dst.Size.X);
        int vy = dst.Size.Y <= 0 ? 0 : (int)((wy - dst.Origin.Y) * (double)VirtualHeight / dst.Size.Y);
        return new Vector2D<int>(vx, vy);
    }

    // Scale a point-space letterbox rect to the physical drawable by the per-axis HiDPI ratio. Scales
    // EDGES with integer multiply-then-divide (exact for any rational ratio) so an integer point rect
    // stays an exact integer PIXEL multiple — PresentScaled's whole-factor Nearest path still fires.
    // winW/winH <= 0 (minimized) or drawable == window → identity.
    private static Rectangle<int> ToDrawablePixels(Rectangle<int> r, int winW, int winH, int pxW, int pxH)
    {
        if (winW <= 0 || winH <= 0 || pxW <= 0 || pxH <= 0 || (pxW == winW && pxH == winH))
            return r;
        int left   = r.Origin.X * pxW / winW;
        int right  = (r.Origin.X + r.Size.X) * pxW / winW;
        int top    = r.Origin.Y * pxH / winH;
        int bottom = (r.Origin.Y + r.Size.Y) * pxH / winH;
        return new Rectangle<int>(left, top, right - left, bottom - top);
    }

    // Map the virtual play-area buffer onto the window per the settings scaling mode, returning the
    // letterbox rect IN WINDOW POINTS (the present path converts it to drawable pixels via
    // ToDrawablePixels; WindowToVirtual inverts this same point rect, so mouse hit-testing stays exact).
    //   integer  FixedScale > 0 → that exact user factor (may crop if it exceeds the window);
    //            FixedScale == 0 → largest whole multiple that fits (fractional if smaller than one virtual)
    //   fit      aspect-preserving, fills up AND down (letterboxed)
    //   stretch  fills the output, ignores aspect ratio
    // PresentScaled then keeps the blit crisp at any of these scales.
    private Rectangle<int> ComputeLetterbox(int outW, int outH)
    {
        if (outW <= 0 || outH <= 0) return new Rectangle<int>(0, 0, VirtualWidth, VirtualHeight);

        if (_settings.Scaling == ScalingMode.Stretch)
            return new Rectangle<int>(0, 0, outW, outH);

        double fit = Math.Min(outW / (double)VirtualWidth, outH / (double)VirtualHeight);
        double scale;
        if (_settings.Scaling == ScalingMode.Integer)
            scale = _settings.FixedScale > 0 ? _settings.FixedScale           // exact user-defined factor
                  : fit >= 1.0 ? Math.Floor(fit) : fit;                        // auto: largest that fits
        else
            scale = fit;   // Fit
        int w = (int)(VirtualWidth * scale);
        int h = (int)(VirtualHeight * scale);
        return new Rectangle<int>((outW - w) / 2, (outH - h) / 2, w, h);
    }

    // Blit the streaming texture into `dst` (PHYSICAL drawable pixels), border-cleared to `border`,
    // keeping pixels crisp at ANY scale. A whole-number scale (dst an exact integer multiple of the
    // virtual size) uses nearest — pixel-perfect. A fractional scale uses sharp-bilinear: prescale by
    // the smallest integer ≥ the per-axis scale with NEAREST into an offscreen target, then
    // LINEAR-downscale that into `dst`. Since the linear stage only ever downsamples from an exact
    // integer multiple, integer-sized pixels stay crisp and only the sub-pixel remainder is antialiased.
    private void PresentScaled(Rectangle<int> dst, RgbaColor border)
    {
        // Per-axis: is dst an exact integer multiple of the virtual size, and the ceil multiple.
        bool integral = dst.Size.X % VirtualWidth == 0 && dst.Size.Y % VirtualHeight == 0;
        int nx = (dst.Size.X + VirtualWidth - 1) / VirtualWidth;    // ceil(scaleX)
        int ny = (dst.Size.Y + VirtualHeight - 1) / VirtualHeight;  // ceil(scaleY)

        // Sharp only when the scale is fractional AND at least one axis is being magnified (a pure
        // sub-1× shrink can't prescale usefully → straight linear). Falls back to a direct copy if the
        // renderer can't render to a texture or the prescale target won't fit.
        bool sharp = !integral && _targetTextureOk && (nx > 1 || ny > 1) && TryEnsurePrescale(nx, ny);

        if (sharp)
        {
            // Pass 1: nearest integer prescale into the render target (full-rect copy fills it, no
            // clear needed). Force nearest here regardless of the mode left by a prior frame.
            _sdl.SetTextureScaleMode(_texture, ScaleMode.Nearest);
            _sdl.SetRenderTarget(_renderer, _prescale);
            _sdl.RenderCopy(_renderer, _texture, (Rectangle<int>*)null, (Rectangle<int>*)null);
            _sdl.SetRenderTarget(_renderer, (Texture*)null);
        }

        // Border + final blit. Draw colour is shared renderer state, so set it AFTER restoring the
        // window target.
        _sdl.SetRenderDrawColor(_renderer, border.R, border.G, border.B, 255);
        _sdl.RenderClear(_renderer);
        if (sharp)
        {
            _sdl.RenderCopy(_renderer, _prescale, (Rectangle<int>*)null, &dst);   // _prescale is Linear
        }
        else
        {
            // Direct: nearest for a whole factor (crisp), linear for a sub-1× shrink (smooth).
            _sdl.SetTextureScaleMode(_texture,
                integral ? ScaleMode.Nearest : ScaleMode.Linear);
            _sdl.RenderCopy(_renderer, _texture, (Rectangle<int>*)null, &dst);
        }
    }

    // Ensure the offscreen prescale render target is (VirtualWidth*nx)×(VirtualHeight*ny). Returns
    // false — caller falls back to a direct copy — if that size would exceed the renderer's max
    // texture (0 = no limit) or creation fails.
    private bool TryEnsurePrescale(int nx, int ny)
    {
        long w = (long)VirtualWidth * nx, h = (long)VirtualHeight * ny;
        if (_maxTexW > 0 && w > _maxTexW) return false;
        if (_maxTexH > 0 && h > _maxTexH) return false;
        if (_prescale != null && _prescaleNx == nx && _prescaleNy == ny) return true;

        if (_prescale != null) { _sdl.DestroyTexture(_prescale); _prescale = null; _prescaleNx = _prescaleNy = 0; }
        var t = _sdl.CreateTexture(_renderer, (uint)PixelFormatEnum.Abgr8888,
            (int)TextureAccess.Target, (int)w, (int)h);
        if (t == null) return false;
        _sdl.SetTextureScaleMode(t, ScaleMode.Linear);   // the smooth downscale stage
        _sdl.SetTextureBlendMode(t, BlendMode.None);     // opaque; no stray alpha bleed
        _prescale = t; _prescaleNx = nx; _prescaleNy = ny;
        return true;
    }

    public void Dispose()
    {
        try { _sdl.StopTextInput(); } catch { }
        if (_prescale != null) { _sdl.DestroyTexture(_prescale); _prescale = null; }
        if (_texture != null) { _sdl.DestroyTexture(_texture); _texture = null; }
        if (_renderer != null) { _sdl.DestroyRenderer(_renderer); _renderer = null; }
        if (_window != null) { _sdl.DestroyWindow(_window); _window = null; }
        try { _sdl.Quit(); } catch { }
    }
}
