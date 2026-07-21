using System;
using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// Mac Toolbox shim — minimal C# representations of the most-frequently-called
// Mac Toolbox APIs that the EVO decompile relies on. Each shim records its
// invocation conceptually so direct ports of dialog/event/sound/resource
// functions can call them as if the real Toolbox were present.
//
// MonoGame / FontStashSharp dependencies have been stripped for the game.
// Any method that previously used SpriteBatch, Texture2D, Color, Rectangle
// (XNA), RenderTarget2D, or FontSystem is now a no-op stub. The method
// signatures are preserved so callers compile; host wiring will restore
// real rendering later.
public static partial class MacToolbox
{
    // Foreground colour index set by ForeColor() (which also caches the resolved
    // RgbaColor); ResolveForeColor reads this index. blackColor by default.
    private static int _foreColor = 0x21;

    /// Host hook to play a Mac snd resource by ID. Registered by the
    /// host against its sound engine. When null, SndPlay is a no-op.
    /// Signature: (sndId, volume[0..1]).
    public static System.Action<int, float>? SndPlayer;

    /// Direct port of FUN_10060288 (EV Override-11.c line 40191). The Mac
    /// signature is (sndPtr, priority, leftVol, rightVol); the side volumes are
    /// 0..0x80 (128 = full scale), NOT 0..0xff — the snd queue (FUN_10074f10)
    /// clamps each side to 0x80, the C# caller mirrors it (EnqueueSoundVoice),
    /// and the mixer treats 0x80 as unity (sample*vol>>7). The function averages
    /// the two side volumes into a mono value and forwards to the snd queue; our
    /// port converts the average into a 0..1 host volume.
    public static void SndPlay(int sndId, short priority = 1, short leftPan = 0x80, short rightPan = 0x80)
    {
        if (sndId == 0 || SndPlayer is null) return;
        // FUN_10060288 line 40200: vol = (left + right) >> 1.
        int vol = ((int)leftPan + (int)rightPan) >> 1;
        // domain is 0..0x80 (0x80 = unity), so /128f not /255f — /255f would halve every volume.
        float volume = System.Math.Clamp(vol / 128f, 0f, 1f);
        SndPlayer(sndId, volume);
    }

    /// 3-arg int overload: shared pan for left+right.
    public static void SndPlay(int sndId, int priority, int pan)
        => SndPlay(sndId, (short)priority, (short)pan, (short)pan);

    /// 4-arg int overload, casts to short.
    public static void SndPlay(int sndId, int priority, int leftPan, int rightPan)
        => SndPlay(sndId, (short)priority, (short)leftPan, (short)rightPan);

    /// Host hook to play the system alert beep. Registered by the host against
    /// its sound engine (a synthesized simple beep through the mixer, so the
    /// master volume scales it like the Mac speaker volume scaled the real
    /// beep). When null, SysBeep is silent.
    public static System.Action? BeepPlayer;

    /// Mac SysBeep — plays the SYSTEM alert sound (the System file's beep,
    /// picked in the Sound control panel), never an application resource.
    /// Do not route through SndPlay(0x80): EVO's snd 128 is 'Warp Up', not a beep.
    public static void SysBeep(short duration)
    {
        BeepPlayer?.Invoke();
    }

    // QuickDraw / Window Manager

    // The current GrafPtr last passed to SetPort — what GetPort() must report so the
    // save→restore idiom (GetPort(savedPort);…;SetPort(savedPort[0])) stays transparent.
    // A 0 stub here would make restore SetPort(0)→CurrentDrawTarget=2 (unregistered), routing
    // subsequent draws to the SCREEN target instead of any active offscreen GWorld.
    private static int _currentPort;

    public static void SetPort(int port)
    {
        _currentPort = port;
        // Multi-GWorld routing. A GWorld's pixmap key is
        // `port + 2` (the ported CopyBits sites compute srcBits/dstBits as
        // `ReadInt(gworldSlot) + 2`). Tag subsequent draw-queue commands with
        // that key; DrainDrawQueue binds the matching RenderTarget — the
        // SCREEN, the offscreen BACKDROP (MacScratch.BackdropScratchPixmap) or the ANIM
        // scratch (MacScratch.AnimScratchPixmap) — and any unregistered key falls back to
        // the screen target. This is the faithful Mac model: DrawPicture/PaintRect/
        // DrawString land in whichever GWorld is the current port, and the
        // title composes the backdrop offscreen then CopyBits it to screen.
        CurrentDrawTarget = port + 2;
    }
    public static int  GetPort() => _currentPort;
    // NO-OP: there is no active-GDevice concept in the true-colour host (colour ops
    // take an explicit device arg where they need one), so the Palette save/set/restore
    // pattern GetGDevice()==0 → SetGDevice(dev) → SetGDevice(saved) is Mac-invisible
    // plumbing. GetGDevice always reports 0 (the "no device" sentinel).
    public static void SetGDevice(int gdevice) { }
    public static void SetGDevice() { }   // arg-less form the mechanical transcription emitted
    public static int  GetGDevice() => 0;
    public static int  GetCWMgrPort() => 0;
    public static int  GetGrayRgn() => 0;
    public static int  NewRgn() => 0;
    public static void DisposeRgn(int rgn) { /* no-op shim */ }
    public static int  FrontWindow() => _frontWindowHandle;   // set by GetNewCWindow (MacToolbox.RegisterAbsorbers.cs)
    public static void InitCursor() => HostInitCursor?.Invoke();
    public static void ShowCursor() { /* no-op shim */ }
    public static void HideCursor() { /* no-op shim */ }
    public static void DrawMenuBar() { /* no-op shim */ }
    public static int  GetMainDevice() => _mainScreenDevice;

    // Synthetic main-screen GDevice
    // The game has no Mac display hardware, so GetMainDevice is built from the host's
    // display size (OverrideGameV2.VirtualWidth/Height), injected via
    // InitMainScreenDevice — the actual dimensions live in the host, not in any
    // port. We build a minimal but real GDevice/PixMap/GDHandle so the QuickDraw
    // readers resolve faithfully: gdRect at GDevice+0x22 (top,left,bottom,right)
    // and 8-bit depth at gdPMap(+0x16)→pixmap+0x20 (EVO's native screen depth).
    private static int _mainScreenDevice;  // GDHandle (*handle = GDevice record); 0 until init
    private static int _mainScreenWidth;   // host display size, mirrored for managed readers
    private static int _mainScreenHeight;

    /// Main-screen gdRect as managed values (top,left = 0; bottom,right = host size), so
    /// port code can read the screen bounds without dereferencing the EvoMemory GDevice.
    public static void GetMainDeviceBounds(out short top, out short left, out short bottom, out short right)
    {
        top = 0;
        left = 0;
        bottom = (short)_mainScreenHeight;
        right = (short)_mainScreenWidth;
    }

    /// Resolve a GDevice's screen pixmap (gdPMap) bounds + pixel depth — the
    /// fields CacheCurrentDeviceFields copies into the render context. Walks the
    /// Mac structs at the toolbox boundary: GDHandle → GDevice → gdPMap handle →
    /// PixMap, then reads bounds{top,left}@+6, bounds{bottom,right}@+10 and
    /// pixelSize@+0x20 (all packed exactly as QuickDraw stores them).
    public static void GetDevicePixMapFields(int gdHandle, out int boundsTopLeftPacked,
                                             out int boundsBotRightPacked, out short pixelSize)
    {
        var pixMap = MacPixMaps.At(GetDevicePMapHandle(gdHandle));
        boundsTopLeftPacked  = pixMap.BoundsTopLeftPacked;
        boundsBotRightPacked = pixMap.BoundsBotRightPacked;
        pixelSize            = pixMap.PixelSize;
    }

    /// GDevice → gdPMap pixmap handle (managed MacGDevices field). Every GDevice is
    /// managed now (InitMainScreenDevice + NewGDeviceForPixmap both build MacGDevices),
    /// so the raw GDHandle→GDevice→+0x16 EvoMemory walk is gone; MacGDevices.At throws on
    /// a stale/foreign handle (the migration tripwire).
    private static int GetDevicePMapHandle(int gdHandle)
        => MacGDevices.At(gdHandle).PMapHandle;

    /// Resolve a GDevice's screen PixMap baseAddr + rowBytes — used by the render
    /// window to build the per-row pixel table. Walks GDHandle → GDevice → gdPMap
    /// handle → PixMap at the toolbox boundary; rowBytes masks off the high
    /// flag bits (&0x3fff) exactly as QuickDraw stores them.
    public static void GetDeviceScreenPixMap(int gdHandle, out int baseAddr, out short rowBytes)
    {
        var pixMap = MacPixMaps.At(GetDevicePMapHandle(gdHandle));
        // MANAGED PIXELS: the buffer is a byte[] now (EvoMemory itself was fully
        // retired) — baseAddr is 0 unless the pixmap was aimed at an external raw
        // buffer (LegacyBaseAddr; see Install*GWorldPort — a raw sprite-pipeline block
        // address, not an EvoMemory one). The screen pixmap never had a pixel buffer
        // in this port anyway; the real screen is the host RenderTarget bridge.
        baseAddr = pixMap.LegacyBaseAddr;
        rowBytes = (short)pixMap.RowBytes;
    }

    /// GDevice's inverse-table handle (gdITable, managed MacGDevices field).
    public static int GetDeviceITable(int gdHandle)
        => MacGDevices.At(gdHandle).ITableHandle;

    /// Clear a GDevice's gdPMap field (managed MacGDevices field) — done before
    /// DisposeGDevice so the device teardown does not touch the (separately owned) pixmap.
    public static void ClearDevicePixMap(int gdHandle)
    {
        if (MacGDevices.IsHandle(gdHandle)) MacGDevices.At(gdHandle).PMapHandle = 0;
    }

    /// PixMap handle → pmTable colour-table handle (managed MacPixMap field).
    public static int GetPixMapColorTable(int pixMapHandle)
        => MacPixMaps.At(pixMapHandle).ColorTableHandle;

    /// PixMap handle → pixelSize (managed MacPixMap field).
    public static short GetPixMapPixelSize(int pixMapHandle)
        => MacPixMaps.At(pixMapHandle).PixelSize;

    /// PixMap handle → rowBytes (managed MacPixMap field; the 0x8000 PixMap
    /// flag the Mac stores in the high bits is implicit now).
    public static short GetPixMapRowBytes(int pixMapHandle)
        => (short)MacPixMaps.At(pixMapHandle).RowBytes;

    /// PixMap handle → bounds.right write (managed MacPixMap field).
    public static void SetPixMapBoundsRight(int pixMapHandle, short right)
        => MacPixMaps.At(pixMapHandle).BoundsRight = right;

    /// Build the synthetic main-screen GDevice from the host display size and
    /// return the indirection cell the boot code stores at _DAT_10081100
    /// (cell → GDHandle → GDevice → gdRect), matching the Mac's cached main device.
    public static int InitMainScreenDevice(int width, int height)
    {
        _mainScreenWidth = width;
        _mainScreenHeight = height;
        // The screen PixMap is a managed object now (no pixel buffer — the real
        // screen is the host RenderTarget bridge; baseAddr was always 0 here).
        int pixMapHandle = MacPixMaps.Register(new MacPixMap
        {
            PixelSize = 8,                       // screen depth
            BoundsBottom = (short)height,
            BoundsRight  = (short)width,
        });

        // The GDevice is a managed MacGDevices object (was a NewPtrClear(0x40)
        // record + a NewPtr(4) GDHandle indirection — the registry handle IS the
        // device, the accessors dual-dispatch).
        var dev = MacGDevices.New();
        dev.PMapHandle = pixMapHandle;
        dev.RectBottom = (short)height;
        dev.RectRight  = (short)width;
        _mainScreenDevice = dev.Handle;

        // Return the managed GDevice handle directly. The Mac parked it behind a
        // NewPtr(4) GDHandle cell (*_DAT_10081100) that its one consumer derefed
        // once; nothing else reads that cell, so the indirection is dropped.
        return _mainScreenDevice;
    }
    /// QuickDraw BackColor — Mac indexed background colour. TETextBoxCore erases its rect
    /// to this before drawing (white-text-on-black alerts, the invert-trick spaceport
    /// description), reading _activeBackColor. Default = white.
    public static void BackColor(int color) => _activeBackColor = MapQuickDrawColorIndex(color);
    /// QuickDraw PenSize — sets the pen rect that Line/LineTo strokes with; DrawLineSegment
    /// reads _penW/_penH. Beam lasers (DrawLaserTrails), hyperspace lanes (DrawHyperspaceLanes),
    /// and the galaxy-map nav route (DrawGalaxyMap) PenSize(wide) then stroke, so a no-op here
    /// renders them as 1px hairlines. (FrameRoundRect bakes its own const pen=3.) FUN_1006f6d4 class.
    public static void PenSize(int w, int h) { _penW = System.Math.Max(1, w); _penH = System.Math.Max(1, h); }
    public static void PenNormal() { _penW = 1; _penH = 1; }
    // InvertRect(short×4) + InvertRect(int) — real XOR-invert impls live in
    // MacToolbox.QuickDraw.cs (selection highlights).
    /// InvalRect — Mac Window Manager: add rect to the window's update
    /// region so an updateEvt later redraws that area. The port models the
    /// region as a single pending-update flag (no rect granularity — the
    /// title's updateEvt handler repaints the whole port anyway): each call
    /// marks the game window invalidated, and WaitNextEvent delivers ONE
    /// updateEvt for the accumulated batch (MacToolbox.Modal.cs). The title
    /// screen depends on this — its pilot-info panel paints ONLY via
    /// InvalRect→updateEvt→DrawPilotInfo, same as the Mac.
    public static void InvalRect(int rectPtr) => NoteWindowInvalidated();
    public static void ClipRect(int rectPtr) { /* no-op shim */ }
    public static void ClipRect(short[] rect) { /* no-op shim */ }
    /// RectRgn — managed MacRegions carry the rect-region BBox; raw stub
    /// handles (NewHandleClear(10) regions on window records) stay no-ops.
    public static void RectRgn(int rgn, short[] rect)
    {
        if (!MacRegions.IsHandle(rgn) || rect is null || rect.Length < 4) return;
        MacRegions.At(rgn).SetBBox(rect[0], rect[1], rect[2], rect[3]);
    }
    // FrameRoundRect(short×4, short×2) — real impl lives in QuickDraw.cs.
    /// Mac SetRect arg order: (Rect*, left, top, right, bottom) — the decompiled
    /// transcriptions emit args in that order because the decompiler knew Mac's SetRect
    /// proto. The rect's memory layout is still (top@+0, left@+2, bottom@+4, right@+6),
    /// but the PARAMETER ORDER coming in matches Mac, not the layout.
    public static void SetRect(short[] rect, short left, short top, short right, short bottom)
    {
        if (rect is null || rect.Length < 4) return;
        rect[0] = top; rect[1] = left; rect[2] = bottom; rect[3] = right;
    }
    public static void OffsetRect(short[] rect, short dx, short dy)
    {
        if (rect is null || rect.Length < 4) return;
        rect[0] = (short)(rect[0] + dy); rect[1] = (short)(rect[1] + dx);
        rect[2] = (short)(rect[2] + dy); rect[3] = (short)(rect[3] + dx);
    }
    public static void InsetRect(short[] rect, short dx, short dy)
    {
        if (rect is null || rect.Length < 4) return;
        rect[0] = (short)(rect[0] + dy); rect[1] = (short)(rect[1] + dx);
        rect[2] = (short)(rect[2] - dy); rect[3] = (short)(rect[3] - dx);
    }
    public static bool PtInRect(int hPoint, int vPoint, short[] rect)
    {
        if (rect is null || rect.Length < 4) return false;
        return hPoint >= rect[1] && hPoint < rect[3]
            && vPoint >= rect[0] && vPoint < rect[2];
    }
    public static bool SectRect(short[] a, short[] b, short[] result)
    {
        if (a is null || b is null || a.Length < 4 || b.Length < 4) return false;
        short top = Math.Max(a[0], b[0]);
        short left = Math.Max(a[1], b[1]);
        short bottom = Math.Min(a[2], b[2]);
        short right = Math.Min(a[3], b[3]);
        bool empty = top >= bottom || left >= right;
        if (result is not null && result.Length >= 4)
        {
            // Inside Mac: SectRect zeroes destRect when the intersection is empty.
            result[0] = empty ? (short)0 : top;
            result[1] = empty ? (short)0 : left;
            result[2] = empty ? (short)0 : bottom;
            result[3] = empty ? (short)0 : right;
        }
        return !empty;
    }
    /// UnionRect for managed {top,left,bottom,right} short[4] rects.
    public static void UnionRect(short[] a, short[] b, short[] result)
    {
        if (a is null || b is null || result is null
            || a.Length < 4 || b.Length < 4 || result.Length < 4) return;
        // Mac UnionRect: an empty operand contributes nothing (the union is the other
        // rect); two empties give an empty rect. Read all coords before writing so
        // result may safely alias a or b.
        bool aEmpty = a[0] >= a[2] || a[1] >= a[3];
        bool bEmpty = b[0] >= b[2] || b[1] >= b[3];
        short t, l, bt, r;
        if (aEmpty && bEmpty)      { t = l = bt = r = 0; }
        else if (aEmpty)           { t = b[0]; l = b[1]; bt = b[2]; r = b[3]; }
        else if (bEmpty)           { t = a[0]; l = a[1]; bt = a[2]; r = a[3]; }
        else { t = Math.Min(a[0], b[0]); l = Math.Min(a[1], b[1]); bt = Math.Max(a[2], b[2]); r = Math.Max(a[3], b[3]); }
        result[0] = t; result[1] = l; result[2] = bt; result[3] = r;
    }

    // Resource Manager

    // GetResource(uint,int) + GetIndString(int,short,short) now live in
    // MacToolbox.Resource.cs (real Resource Manager backed by the arena).
    /// ReleaseResource — Mac Resource Manager: free a resource and drop it from the map.
    /// Intentional no-op (returns noErr): GetResource materialises a resource ONCE into the
    /// arena and caches its handle (MacToolbox.Resource.cs), so a repeat GetResource returns
    /// the same handle; the arena is bump-only so there is nothing to free. Callers that want
    /// a slot cleared do so themselves (e.g. AboutEvoModal writes the handle slot back to 0).
    public static short ReleaseResource(int handle) => 0;  // noErr
    public static void HLock(int handle) { /* no-op shim */ }
    public static void HUnlock(int handle) { /* no-op shim */ }
    public static void MoveHHi(int handle) { /* no-op shim */ }
    /// MaxMem — Mac Memory Manager "largest free block + grow room". Reports a
    /// fixed 256MB: Windows has the RAM, and MemoryCheckOnStartup ExitToShells
    /// with the low-memory alert if this returns < ~9.2MB (a 0 return put every
    /// boot through that teardown branch).
    public static int MaxMem(int growPtr) => 0x10000000;
    // No-scratch form for callers that never read the grow out-param.
    public static int MaxMem() => 0x10000000;
    /// HPurge — Mac Memory Manager: mark a relocatable block purgeable so
    /// the heap may reclaim it under pressure. Intentional no-op in the game:
    /// there is no Mac heap / Memory Manager, so purgeability is meaningless
    /// (the .NET GC owns lifetime). No observable effect.
    public static void HPurge(int handle) { /* intentional no-op — see above */ }
    public static void HNoPurge(int handle) { /* no-op shim */ }
    // Memory Manager: GetHandleSize / NewHandle / NewPtr / NewPtrClear /
    // DisposePtr / DisposeHandle now live in MacToolbox.Resource.cs (real
    // free-list allocator over the arena).
    // BlockMoveData(int,int,int) also lives in MacToolbox.Resource.cs (real
    // EvoMemory byte copy).
    /// PBHGetVInfoSync trap — stubbed. Returns 0 (noErr) on Windows.
    public static short PBHGetVInfo(int volNamePtr, short vRefNum, int paramBlock) => 0;
    /// PBHGetVInfoSync called with a pre-populated paramBlock. Stubbed.
    public static short PBHGetVInfoSync(int paramBlock) => 0;
    /// PBCatSearchSync trap — stubbed. Returns 0 (noErr) on Windows.
    public static short PBCatSearchSync(int paramBlock) => 0;
    /// PBHGetVolParmsSync trap — stubbed. Returns 0 (noErr).
    public static void PBHGetVolParmsSync(int ioCompletion, short ioVRefNum,
        int ioBuffer, int ioReqCount, out int ioActCount)
    {
        ioActCount = ioReqCount;
    }
    /// SndChannelStatus trap — stubbed. Returns (success, idle).
    public static (bool ok, byte isBusy) SndChannelStatus(int chPtr) => (true, 0);
    /// Window Manager CalcVisBehind trap — stubbed.
    public static void CalcVisBehind(int window, int rgn) { }
    /// Window Manager PaintBehind trap — stubbed.
    public static void PaintBehind(int window, int rgn) { }
    /// Palette Manager ProtectEntry trap — stubbed.
    public static void ProtectEntry(int index, int protect) { }
    /// Palette Manager ReserveEntry trap — stubbed.
    public static void ReserveEntry(int index, int reserve) { }
    // Mac classic epoch: midnight, 1 January 1904, LOCAL time. GetDateTime returns
    // the local wall-clock seconds since that epoch as a 32-bit unsigned count
    // (it wraps in 2040, exactly as on a real Mac — kept bug-for-bug).
    private static readonly System.DateTime MacEpoch =
        new System.DateTime(1904, 1, 1, 0, 0, 0, System.DateTimeKind.Unspecified);

    /// Current local time as Mac epoch seconds (since 1904-01-01).
    public static int GetDateTimeSeconds()
        => unchecked((int)(uint)(long)(System.DateTime.Now - MacEpoch).TotalSeconds);

    /// TextEdit TEUpdate trap — stubbed.
    public static void TEUpdate(int rectVal, int teH) { }
    /// TEUpdate with a managed update rect (the styled-TE walkers' stack copy of
    /// destRect) — stubbed like the pointer form.
    public static void TEUpdate(short[] updateRect, int teH) { }
    /// TERec.destRect accessor (te+0..+7, {top,left,bottom,right}) for the
    /// styled-TE list walkers (UpdateAllTextEdits / DisposeAllTextEditList).
    /// TextEdit is not wired in the game (TEStyleNew returns handle 0, every TE trap is
    /// a stub), so this returns an empty rect; a real TE manager would read its
    /// record here.
    public static short[] TEGetDestRect(int teH) => new short[4];
    /// TERec.inPort accessor (te+0x52, the GrafPtr the record draws into) — same
    /// unwired-TextEdit stub as TEGetDestRect (0 = no port).
    public static int TEGetInPort(int teH) => 0;
    /// TextEdit TEDispose trap — stubbed.
    public static void TEDispose(int teH) { }
    /// Memory Manager FreeMem trap — stubbed to ~256 MB.
    public static int FreeMem() => 0x10000000;
    /// Memory Manager MaxBlock trap — stubbed.
    public static int MaxBlock() => 0x10000000;
    /// HLockHi trap — stubbed; same as HLock.
    public static void HLockHi(int handle) { HLock(handle); }
    /// HGetState trap — stubbed: always returns 0.
    public static byte HGetState(int handle) => 0;
    /// HSetState trap — stubbed.
    public static void HSetState(int handle, byte state) { }
    /// SetHandleSize trap — stubbed.
    public static void SetHandleSize(int handle, uint newSize) { }
    /// MemError trap — stubbed to 0 (noErr).
    public static short MemError() => 0;
    // Managed overload: catalog info for a folder FSSpec without a Mac paramBlock. No file
    // system in the game → pass the input volume/dir straight back (the no-op the original yields).
    public static short PBGetCatInfoSync(short inVRefNum, int inDirID, out short vRefNum, out int dirID)
    {
        vRefNum = inVRefNum;
        dirID = inDirID;
        return 0;  // noErr
    }
    /// SetResLoad trap — stubbed.
    public static void SetResLoad(bool autoLoad) { }
    /// CallUniversalProc trap (no-arg variant) — stubbed.
    public static void CallUniversalProc() { }
    /// CallUniversalProc (multi-arg) — stubbed.
    public static void CallUniversalProc(int arg1) { }
    public static void CallUniversalProc(short arg1, int arg2, int arg3) { }
    /// Acquire sound-mixer lock — stubbed. Returns saved interrupt level.
    public static int AcquireMixerLock() => 0;
    /// Release sound-mixer lock — stubbed.
    public static void ReleaseMixerLock(int savedIpl) { }
    /// EqualString trap (int Str255 addrs) — stubbed (0). No live caller; kept as the
    /// raw-pointer signature for any earlier-transcription caller still typed that way.
    public static byte EqualString(int strA, int strB, byte caseSensitive, byte diacrits) => 0;
    /// Managed form: compare `strA` against the leading Pascal Str255 in `strB`.
    /// Mac EqualString returns NON-ZERO when the strings ARE EQUAL (not C-style 0-equal).
    /// LIVE: FindFolder resolves to a real folder (MacToolbox.HfsDataFork.cs), so the pilot-prefs
    /// owner scan depends on this compare — an inverted result re-inits / duplicates the record.
    /// EV always passes caseSensitive=1, diacrits=1 (exact compare).
    public static byte EqualString(string strA, byte[] strB, byte caseSensitive, byte diacrits)
    {
        if (strB is null || strB.Length == 0) return 0;
        string other = PascalToString(strB);   // Str255 length byte + Mac-Roman body
        var cmp = caseSensitive != 0 ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;
        return (byte)(string.Equals(strA ?? "", other, cmp) ? 1 : 0);
    }
    /// Gestalt trap returning OSErr. Stubbed to noErr (0).
    public static short GestaltErr(int selector, out int dataOut) { dataOut = 0; return 0; }
    /// Gestalt trap — stubbed for typical EV queries.
    public static uint Gestalt(int selector) => 0;

    // Dialog Manager

    // GetNewDialog / DrawDialog / DisposeDialog / GetDialogItem(int5) /
    // ModalDialog(ref short) now have REAL implementations in
    // MacToolbox.DialogManager.cs.
    /// Dialog Manager InitDialog helper — stubbed.
    public static void InitDialog(int dialog, int filterProc) { }
    // ShowWindow / HideWindow are REAL for dialog windows (visible-flag +
    // compositor republish) — see MacToolbox.DialogManager.cs.
    public static void SelectWindow(int window) { /* no-op shim */ }
    // Managed-array form: captures into the caller's arrays (the int-address form's
    // managed mirror). rectOut = {top,left,bottom,right}. Null array = skip.
    public static void GetDialogItem(int dialog, int itemNo,
                                      short[]? typeOut, int[]? handleOut, short[]? rectOut)
    {
        var rec = FindDialog(dialog);
        var it = rec?.Items.Find(i => i.ItemNo == itemNo);
        if (it is null)
        {
            if (handleOut is { Length: > 0 }) handleOut[0] = 0;
            return;
        }
        if (typeOut is { Length: > 0 })   typeOut[0] = (short)it.Kind;
        if (handleOut is { Length: > 0 }) handleOut[0] = it.Handle;
        if (rectOut is { Length: >= 4 })
        {
            rectOut[0] = (short)it.Top;
            rectOut[1] = (short)it.Left;
            rectOut[2] = (short)it.Bottom;
            rectOut[3] = (short)it.Right;
        }
    }
    public static void SetDialogDefaultItem(int dialog, int itemNo)
    {
        // Record which item the Return/Enter key fires (the OK button). The
        // Dialog Manager's ModalDialog reads rec.DefaultItem on a Return.
        var rec = FindDialog(dialog);
        if (rec is not null) rec.DefaultItem = itemNo;
    }
    // Begin/EndUpdate clear the Mac update region; the port's single-flag
    // region model clears on updateEvt DELIVERY instead (WaitNextEvent), so
    // these stay no-ops.
    public static void BeginUpdate(int window) { /* no-op shim */ }
    public static void EndUpdate(int window) { /* no-op shim */ }
    public static void InvalRect(short[] rect) => NoteWindowInvalidated();   // see the int overload

    // Event Manager

    /// FlushEvents — Mac Event Manager: discard pending queued events whose
    /// type is in `mask`, stopping if an event in `stopMask` is reached.
    /// The game has no retained event queue; the "pending" state is the per-frame
    /// input snapshot (MacToolbox.Modal.cs). We flush that snapshot coarsely
    /// by event class so input that piled up during a modal (clicks/keys)
    /// doesn't immediately leak into the next screen. stopMask is moot here
    /// (no ordered queue to stop within) and is ignored.
    ///
    /// OGB: many decompiled call sites pass a raw MacEventType code (1-5, 8)
    /// instead of the shifted EventMask bit — see ORIGINAL_GAME_BUGS.md. Kept
    /// bug-for-bug: EventMask.NullEventMask (bit 0) flushes nothing for real,
    /// so those calls only ever flush mouseDown/mouseUp, never key events.
    ///
    /// THREADING: FlushEvents runs on the TITLE thread. It must only touch
    /// title-thread-local state (_prevButtonDownOnTitleThread) and volatile
    /// reads (FrameButtonDownBridge). It must NOT mutate FrameTextInput —
    /// that List<char> is owned by the MonoGame thread (Add in the TextInput
    /// handler, Clear in Draw each frame), so a title-thread Clear() is a
    /// data race that can throw and kill the title thread.
    public static void FlushEvents(EventMask mask, EventMask stopMask)
    {
        // Mouse class → drop a pending button edge so a click made during
        // the just-closed modal doesn't fire on the next frame. Rebasing
        // the title thread's edge tracker to the current state means the
        // next rising edge requires a fresh release-then-press.
        if ((mask & (EventMask.MouseDownMask | EventMask.MouseUpMask)) != 0)
            _prevButtonDownOnTitleThread = FrameButtonDownBridge;

        // Key class → drop queued keystrokes. The real Mac FlushEvents removes matching
        // records from the OS event queue; the port's analogue is the durable _typedBuf
        // (WaitNextEvent pops from it — see its comment), which nothing else drains during
        // gameplay. RunMainGameLoop's exit flush passes a shifted mask (decompile
        // FlushEvents(0xff7f,0)) that includes keyDownMask/autoKeyMask, so _typedBuf must be
        // drained here or stray gameplay keystrokes replay as title-screen keyDown events after
        // death/ESC (TitleKeyToButton maps n/o/q/e/p/a/x to New Pilot/Enter Ship/Quit/etc.).
        // OGB-42 call sites (raw ordinals 1/2/3/5) don't set these bits, so they correctly keep
        // NOT flushing keys — bug-for-bug preserved.
        if ((mask & (EventMask.KeyDownMask | EventMask.AutoKeyMask)) != 0)
            ClearTypedChars();

        // Transient event-code slot (title-thread-only) is consumed always.
    }
    // Toolbox bridge: Button / StillDown read the volatile FrameButtonDown
    // updated each host Update() frame. The title thread sees real mouse state
    // and `while (Button()==0)` loops terminate on click.
    public static bool Button()    => PollMouseButton();
    public static bool StillDown() => PollMouseButton();

    // Shared button poll: the live per-frame bridge flag.
    //
    // A held button is PACED ~8 ms here. The Mac sampled GetMouse/StillDown at
    // VBL cadence (~60 Hz), but the port's bridge flag is a plain volatile field, so a
    // tight `while (StillDown())` tracking loop that enqueues a draw every
    // iteration — the galaxy-map drag-pan (RunGalaxyMapDialog, one ScrollGalaxyMapArea
    // blit per turn) and HitTestTitleButton's orb blit — busy-reads it millions of
    // times/sec and floods the UNBOUNDED software draw queue. DrainDrawQueue then
    // grinds through the backlog for seconds = "the galaxy map freezes when you
    // click/drag". 8 ms caps the loop at ~120 Hz (a ~120 Hz cap)
    // so the producer can't outrun the renderer. Paces ONLY while DOWN, so a
    // `while (Button()==0)` wait-for-press is unchanged.
    private static bool PollMouseButton()
    {
        bool down = FrameButtonDownBridge;
        if (down) System.Threading.Thread.Sleep(8);
        return down;
    }
    public static void GetMouse(int[] pointOut)
    {
        if (pointOut is null || pointOut.Length < 1) return;
        var p = FrameMouseBridge;
        pointOut[0] = ((p.V & 0xffff) << 16) | (p.H & 0xffff);
    }
    /// Packed-Point overload — returns (v << 16 | h).
    public static int GetMouse()
    {
        var p = FrameMouseBridge;
        return ((p.V & 0xffff) << 16) | (p.H & 0xffff);
    }
    // Mac Delay trap: block the calling (producer) thread for `ticks` × 1/60 s, then
    // report the tick count at wake in finalTicksOut. A real blocking wait, faithful to the
    // trap — the boot progress bar (Delay(2)/pass) and the zoom-window open (Delay(1)×17) are
    // paced solely by Delay (the host renders on a separate thread), so a no-op here collapses
    // them to only the final frame. 1 tick = 1000/60 ms; clamped so a bad tick count can't hang.
    public static void Delay(int ticks, int[] finalTicksOut)
    {
        SleepTicks(ticks);
        if (finalTicksOut is { Length: > 0 }) finalTicksOut[0] = (int)TickCount();
    }
    private static void SleepTicks(int ticks)
    {
        if (ticks <= 0) return;
        if (ticks > 600) ticks = 600;   // 10 s ceiling — no real caller waits longer
        System.Threading.Thread.Sleep(ticks * 1000 / 60);
    }
    public static uint TickCount() => Components.TickCount.Get();
    /// GetDblTime — the double-click interval in ticks. Not modeled by the host; returns the
    /// classic Mac default (30 ticks ≈ 0.5s), used by the TextEdit click handler's word-select test.
    public static int GetDblTime() => 30;

    // Sound Manager

    public static int SndNewChannel(int[] channelOut, int synthType, int initFlags,
                                     int userProc) => -1;
    /// Sound-init form (BootSoundSubsystem). The channel is host-bridged (the SoundMixer
    /// pump), and `channelHandle` already IS the 'Schn' sentinel (AllocSoundChannelControlBlock
    /// returns MakeSoundChannelHandle()), so there is nothing to store — just report noErr.
    public static short SndNewChannel(int channelHandle, short synthType, byte initFlags,
                                       int userProc) => 0;
    /// Probe form.
    public static short SndNewChannel(int channelHandle, short synthType, int initFlags) => 0;
    /// Managed-slot form (B2 channel-layer migration) — the channel slot is a
    /// SoundChannels.Channels[i].Handle field, not an EvoMemory address. Same
    /// behaviour as the pointer-handle overloads: writes the 'Schn' sentinel
    /// (channel playback is host-bridged) and reports noErr.
    public static short SndNewChannel(out int channelHandle, short synthType, int initFlags,
                                       int userProc)
    {
        channelHandle = SoundChannelHandle;
        return 0;
    }
    public static int SndDisposeChannel(int channel, bool quietNow) => 0;
    /// One-arg shim — quietNow defaults true.
    public static int SndDisposeChannel(int channel) => SndDisposeChannel(channel, true);
    // NO-OP: the Sound Manager command queue (ampCmd/quietCmd/freqCmd/…) isn't modelled —
    // the host plays whole snd resources through SndPlay + the software mixer, not the Mac
    // per-channel command stream. Callers issue these around SndPlay for pacing/volume that
    // the mixer already handles, so dropping them is faithful.
    public static int SndDoCommand(int channel, short[] cmd, bool noWait) => 0;
    public static int SndDoImmediate(int channel, short[] cmd) => 0;
    public static void SndDoCommand(int channel, ushort cmd, ushort param1, int param2) { }
    /// SetDefaultOutputVolume — the Mac hardware-channel volume trap. NO-OP: the
    /// host has no hardware mixer; SetMasterVolume applies the level through the
    /// MasterVolumeSetter host bridge instead, so this path is a faithful no-op.
    public static void SetDefaultOutputVolume(int volume) { }
    public static void SndStopPlay(int channel, bool quietNow) { /* no-op shim */ }
    /// SndStartFilePlay trap. Mac args: (chan, fileRefNum, resNum=sndId,
    /// bufferSize, completionUPP, ...). StartSoundFilePlay (FUN_1004227c)
    /// calls it with p3 = 30000 (the looping title-music stream). Bridge
    /// the resource id to the host file-music player.
    public static void SndStartFilePlay(int channel, int p2, int p3, int p4,
                                         int p5, int p6, int p7, int p8)
        => FileMusicPlayer?.Invoke(p3);
    /// SndStopFilePlay trap. DisposeSoundFileChannel (FUN_10042320) calls
    /// it to tear down the title-music stream. Returns noErr.
    public static int SndStopFilePlay(int channel, int quietNow)
    {
        FileMusicStopper?.Invoke();
        return 0;
    }
    public static short SndFlush(int channel) => 0;
    public static short SndQuiet(int channel) => 0;
    public static short SndPlayDoubleBuffer(int channel, int header) => 0;
    public static short SndGetInfo(int channel, uint selector, int destPtr) => 0;

    // Misc helpers

    /// Look up Mac PICT id. Returns the pict id itself (the DrawPicture/PictResolver
    /// token) when the host's GetPictureImpl confirms the PICT exists, or 0 if missing.
    /// The Mac GetPicture trap returns a PicHandle the caller can deref for the PICT
    /// header (picSize@+0, picFrame{top,left,bottom,right}@+2..+9). To honour that
    /// contract, also register the raw 'PICT' resource bytes under the id key so
    /// ReadResource*(id, off) reads the genuine picFrame: the commodity-trade dialog
    /// (PICT 0x157c) and shipyard icon-strip (PICT 0x17d4) backdrops size their draw
    /// rect from it — previously the deref hit no registry entry, returned 0, the rect
    /// collapsed to {0,0,0,0}, and DrawPicture bailed (backdrop never drawn). The id
    /// (< ResourceHandleBase) is distinct from synthetic resource handles, so no
    /// DrawPicture caller — which still passes the id as its token — changes.
    public static int GetPicture(int id)
    {
        if ((GetPictureImpl?.Invoke(id) ?? 0) == 0) return 0;
        if (!_resourceData.ContainsKey(id))
        {
            byte[]? bytes = GetResourceImpl?.Invoke((uint)MacResType.Pict, id);
            if (bytes is not null) _resourceData[id] = bytes;
        }
        return id;
    }

    public static void SetBackdrop(int pictId) { /* no-op: MonoGame dep stripped for the game; host wiring will restore */ }
    public static void ClearBackdrop() { /* no-op: MonoGame dep stripped for the game; host wiring will restore */ }

    // GetCIcon / PlotCIcon / DisposeCIcon — real cicn decoder in MacToolbox.ColorIcons.cs.
    // CopyBits short[]-rect overload — real impl in MacToolbox.QuickDraw.cs.

    public static void ExitToShell()
    {
        // Mac "quit to Finder" — the real trap never returns to its caller; several
        // decompiled call sites have dead statements after calling it (e.g. TickShipAI's
        // HideCursor/RepaintGameWindow after GracefulExit.Run()), preserved faithfully
        // because on real hardware they're simply never reached. Environment.Exit is the
        // closest .NET analogue: unconditional, immediate process termination.
        Environment.Exit(0);
    }
    public static int  CurResFile() => 0;
    /// Mac c2pstr trap — no-op.
    public static void c2pstr(byte[] buffer) { /* no-op shim */ }
    /// Mac p2cstr trap — no-op.
    public static void p2cstr(byte[] buffer) { /* no-op shim */ }
    /// Mac ParamText trap (un-collapsed managed Str255 buffers). Funnels into
    /// the Dialog Manager's ^0..^3 store (see MacToolbox.DialogManager.cs).
    public static void ParamText(byte[] s1, byte[] s2, byte[] s3, byte[] s4)
        => ApplyParamTextBytes(s1, s2, s3, s4);
    /// Mac GetTime trap — no-op write.
    public static void GetTime(int destPtr) { /* no-op shim */ }
    /// Mac WaitNextEvent trap. Shim: returns event code 0 (nullEvent).
    public static int WaitNextEvent() => 0;
    /// Mac NewRoutineDescriptor (Mixed Mode Manager). Returns the proc
    /// pointer itself as the descriptor token — ModalDialog dispatches the
    /// modal-filter delegate registered under that pointer (see
    /// RegisterModalFilter). 0 in → 0 out (no filter).
    public static int NewRoutineDescriptor(int proc, int procInfo, int isa) => proc;
    /// NewRoutineDescriptor overload.
    public static int NewRoutineDescriptor(int proc, ushort procInfo, byte isa) => proc;
    /// Mac DisposeRoutineDescriptor. Shim: no-op.
    public static void DisposeRoutineDescriptor(int desc) { /* no-op shim */ }
    /// FUN_1007bf40 lazy-init for the Toolbox shim globals. Shim: no-op.
    public static void EnsureToolboxShimReady() { /* no-op shim */ }
    /// Dialog Manager — draws the standard 3-pixel rounded outline around a dialog's default button. Shim: no-op.
    public static void OutlineDefaultButton(int dialog) { /* no-op shim */ }
    /// EVO custom: paints the alert-dialog background. Shim: no-op.
    public static void PaintAlertBackground(int dialog) { /* no-op shim */ }
    /// Mac low-memory global — current menu bar height. Shim returns 0.
    public static short MBarHeight() => 0;
    /// Writes to the MBarHeight low-memory global. Shim: no-op.
    public static void SetMBarHeight(short height) { /* no-op shim */ }
    /// EVO global pause flag. Shim: false.
    public static bool PauseRequested() => false;
    /// EVO global quit flag. Shim: false.
    public static bool QuitRequested() => false;
    /// Writes the global quit flag. Shim: no-op.
    public static void SetQuitFlag(bool value = true) { /* no-op shim */ }

    /// Mac GlobalToLocal — shim: returns the point unchanged.
    public static int GlobalToLocal(int packedPoint) => packedPoint;
    /// PtInRect overload taking a packed 32-bit (v<<16|h) point.
    public static bool PtInRect(int packedPoint, short[] rect)
    {
        int h = (short)(packedPoint & 0xffff);
        int v = (short)((uint)packedPoint >> 16);
        return PtInRect(h, v, rect);
    }
    public static int BitAnd(int a, int b) => a & b;
    public static int BitOr(int a, int b) => a | b;
    // TextEdit shims — all stubbed (TextEdit is not wired in the game).
    public static int TEStyleNew(int destRect, int viewRect) => 0;
    public static void TEActivate(int teH) { }
    public static void TEStyleInsert(int text, int length, int stylHandle, int teH) { }
    public static void TECalText(int teH) { }
    public static void TESetSelect(int selStart, int selEnd, int teH) { }
    public static void TEDelete(int teH) { }
    public static void TEInsert(int textPtr, int length, int teH) { }
    public static void TEDeactivate(int teH) { }
    // Region shims — BBox-real for managed MacRegions (rect regions are all the
    // game ever modelled), no-ops for raw stub handles. Clip TESTS stay always-true.
    /// CopyRgn — copy the rect-region BBox.
    public static void CopyRgn(int srcRgn, int dstRgn)
    {
        if (MacRegions.IsHandle(srcRgn) && MacRegions.IsHandle(dstRgn))
            MacRegions.At(dstRgn).CopyFrom(MacRegions.At(srcRgn));
    }
    /// OffsetRgn — offset the rect-region BBox.
    public static void OffsetRgn(int rgn, int dh, int dv)
    {
        if (MacRegions.IsHandle(rgn)) MacRegions.At(rgn).Offset(dh, dv);
    }
    /// SectRgn — intersect rect-region BBoxes (empty → all-zero BBox).
    public static void SectRgn(int rgnA, int rgnB, int dstRgn)
    {
        if (!MacRegions.IsHandle(rgnA) || !MacRegions.IsHandle(rgnB) || !MacRegions.IsHandle(dstRgn))
            return;
        var a = MacRegions.At(rgnA);
        var b = MacRegions.At(rgnB);
        short top = Math.Max(a.BBoxTop, b.BBoxTop);
        short left = Math.Max(a.BBoxLeft, b.BBoxLeft);
        short bottom = Math.Min(a.BBoxBottom, b.BBoxBottom);
        short right = Math.Min(a.BBoxRight, b.BBoxRight);
        if (top >= bottom || left >= right) { top = left = bottom = right = 0; }
        MacRegions.At(dstRgn).SetBBox(top, left, bottom, right);
    }
    // QuickDraw blit (all-int-address forms)
    /// CopyMask — masked sprite blit (Mac: copy srcBits→dstBits through a 1-bit
    /// mask). The sprite blitter FUN_100779c8 routes every ship/planet/object
    /// draw through this. Modeled on the real CopyBits: resolve srcBits to a
    /// (registered) sprite texture and enqueue a draw into the dstBits GWorld.
    /// The mask is handled by the sprite texture's own alpha channel (sprites
    /// decode WITH transparency), so the separate 1-bit maskBits pixmap is not
    /// needed in the true-colour renderer — SpriteBatch alpha-blends. No-op
    /// until the sprite is registered as a texture (then it becomes visible),
    /// matching CopyBits' "unregistered src → silent no-op" behaviour.
    public static void CopyMask(int srcBits, int maskBits, int dstBits,
                                 int srcRect, int maskRect, int dstRect)
        => CopyBits(srcBits, dstBits, srcRect, dstRect, 0, 0);
    // CGrafPort
    /// CloseCPort — the raw-EvoMemory CGrafPort dispose. Reached only from
    /// DisposeOffscreenGWorld's NON-managed (raw-port) branch, which is dead now that every
    /// offscreen port is a managed MacGrafPort (disposed via MacGrafPorts.Dispose). Tripwire:
    /// if a genuine raw port ever flows here, throw rather than silently do nothing.
    public static void CloseCPort(int portPtr)
    {
        if (portPtr == 0) return;
        throw new System.InvalidOperationException(
            $"CloseCPort on un-migrated raw port 0x{portPtr:x8} — every port should be a managed MacGrafPort");
    }
    /// The game's screen/game pixmap SENTINEL keys:
    /// no real CGrafPort record exists behind them, so the port-field getters must
    /// not dereference (+2/+0x10../+0x18/+0x1c land on unrelated globals). Field
    /// reads on a sentinel return 0 (no pixmap/region handle).
    public const int ScreenPixmapSentinel = 0x1008f720;
    public const int GamePixmapSentinel   = 0x1008f724;
    public static bool IsPixmapSentinel(int port) => port == ScreenPixmapSentinel || port == GamePixmapSentinel;

    // The port-field accessors dual-dispatch: managed MacGrafPorts handles use the typed
    // object; everything else (sentinels, dialogs, any un-migrated raw address) returns the
    // empty/no-record value. Every port the game builds is managed/sentinel/dialog now
    // (NewPort/RegisterAt/NewCWindow).
    /// CGrafPort portPixMap handle.
    public static int GetPortPixMap(int cPort)
        => MacGrafPorts.IsHandle(cPort) ? MacGrafPorts.At(cPort).PixMapHandle : 0;
    /// CGrafPort visRgn handle.
    public static int GetPortVisRgn(int cPort)
        => IsDialogHandle(cPort) ? GetDialogVisRgn(cPort)
         : MacGrafPorts.IsHandle(cPort) ? MacGrafPorts.At(cPort).VisRgn : 0;
    /// CGrafPort clipRgn handle.
    public static int GetPortClipRgn(int cPort)
        => MacGrafPorts.IsHandle(cPort) ? MacGrafPorts.At(cPort).ClipRgn : 0;
    /// CGrafPort portVersion — 0xc000 marks a colour port (the `BitAnd(version, 0xffff8000)`
    /// colour-QD test in the decompile).
    public static short GetPortVersion(int cPort)
        => MacGrafPorts.IsHandle(cPort) ? MacGrafPorts.At(cPort).PortVersion : (short)0;
    /// CGrafPort grafProcs (@ port + 0x68) — custom QD bottleneck-procs record
    /// pointer; 0 restores the standard procs.
    public static void SetPortGrafProcs(int cPort, int procs)
    {
        if (MacGrafPorts.IsHandle(cPort)) MacGrafPorts.At(cPort).GrafProcs = procs;
        // sentinel/dialog/raw: no record to stamp.
    }
    /// CGrafPort portRect (packed {top,left}/{bottom,right}).
    public static void SetPortRect(int cPort, int topLeftPacked, int botRightPacked)
    {
        if (MacGrafPorts.IsHandle(cPort))
            MacGrafPorts.At(cPort).SetPortRectPacked(topLeftPacked, botRightPacked);
        // dialog rect is DlgRecord-owned; sentinel/raw have no record.
    }
    public static void GetPortRect(int cPort, out int topLeftPacked, out int botRightPacked)
    {
        if (IsDialogHandle(cPort))
        {
            var r = GetDialogPortRect(cPort);
            topLeftPacked  = (r[0] << 16) | (r[1] & 0xffff);
            botRightPacked = (r[2] << 16) | (r[3] & 0xffff);
            return;
        }
        if (MacGrafPorts.IsHandle(cPort))
        {
            var port = MacGrafPorts.At(cPort);
            topLeftPacked  = port.PortRectTopLeftPacked;
            botRightPacked = port.PortRectBotRightPacked;
            return;
        }
        topLeftPacked = 0; botRightPacked = 0;   // sentinel/raw: empty rect
    }
    /// CGrafPort portRect as a managed {top,left,bottom,right} short[4].
    public static short[] GetPortRectShorts(int cPort)
    {
        if (IsDialogHandle(cPort)) return GetDialogPortRect(cPort);
        if (MacGrafPorts.IsHandle(cPort)) return MacGrafPorts.At(cPort).PortRectShorts();
        return new short[4];   // sentinel/raw: empty rect
    }
    /// CGrafPort portRect.right (@ port + 0x16) — the scroll code stamps the content
    /// width into just the right edge.
    public static void SetPortRectRight(int cPort, short right)
    {
        if (MacGrafPorts.IsHandle(cPort)) MacGrafPorts.At(cPort).RectRight = right;
        // dialog rect is DlgRecord-owned; sentinel/raw have no record.
    }
    // GetPort overload writing into an int[] — the save half of the
    // GetPort(savedPort);…;SetPort(savedPort[0]) restore idiom; captures the
    // CURRENT port so restore doesn't fall back to the screen target.
    public static void GetPort(int[] portOut)
    {
        if (portOut is not null && portOut.Length > 0) portOut[0] = _currentPort;
    }
    // NO-OP: int-handle boundary marker — InitRenderWindow's else-branch already
    // holds the saved port in `savedPort`, so there is nothing to capture. Keeps
    // the decompile's GetPort(savedPort) call without a fabricated writeback.
    public static void GetPort(int savedPort) { }
    /// Gestalt(selector, out data) — stubbed to noErr (0). Returns Mac
    /// OSErr (short) so callers that capture the return ABI-correctly
    /// don't trip CS0029 void→short.
    public static short Gestalt(int selector, out int dataOut) { dataOut = 0; return 0; }

    // Managed parallel of StringWidth — measure a C# string directly (no Mac-memory
    // read). Use this when the text comes from a managed record (e.g. a name byte[]
    // decoded to string) — EvoMemory, which used to back those records, has since
    // been fully retired, so there's no raw address left to read from.
    public static int StringWidth(string s)
    {
        var fontSys = ResolveFont();
        if (fontSys is null || string.IsNullOrEmpty(s)) return 0;
        int pixelSize = _textSize > 0 ? _textSize : 12;
        return fontSys.MeasureWidth(s, pixelSize);
    }

    /// Decode raw Mac-Roman bytes (code page 10000) to a C# string, falling back to Latin-1 if
    /// the code-page provider is unavailable. This is the CORRECT decoder for ALL Mac resource
    /// text (STR#, TEXT, syst/ship/pers names, descriptions, chatter): Mac-Roman differs from
    /// Windows-1252 in the high bytes — curly quotes 0xD2-0xD5, ellipsis 0xC9, en/em dash
    /// 0xD0/0xD1, bullet 0xA5, the accented Latin set — so decoding Mac text as Windows-1252
    /// mangled apostrophes/quotes/dashes (the garbled chatter + shareware-nag characters).
    public static string MacRomanToString(byte[] bytes, int start, int len)
    {
        if (bytes is null || len <= 0) return string.Empty;
        if (start < 0) start = 0;
        if (start + len > bytes.Length) len = bytes.Length - start;
        if (len <= 0) return string.Empty;
        try { return System.Text.Encoding.GetEncoding(10000).GetString(bytes, start, len); }
        catch { return System.Text.Encoding.Latin1.GetString(bytes, start, len); }
    }
    public static string MacRomanToString(byte[] bytes)
        => bytes is null ? string.Empty : MacRomanToString(bytes, 0, bytes.Length);

    // Encode a C# string as a Mac Pascal string (length byte + Mac-Roman body, clamped to
    // 255 chars) — the inverse of PascalToString, used to stamp names/codes into managed
    // record byte[]s (e.g. the registration placeholder record).
    public static byte[] StringToPascalBytes(string s)
    {
        byte[] body;
        try { body = System.Text.Encoding.GetEncoding(10000).GetBytes(s ?? ""); }
        catch { body = System.Text.Encoding.Latin1.GetBytes(s ?? ""); }
        int len = Math.Min(body.Length, 255);
        var pstr = new byte[len + 1];
        pstr[0] = (byte)len;
        Array.Copy(body, 0, pstr, 1, len);
        return pstr;
    }

    // Decode a managed Mac Pascal string (length byte at [0] + Mac-Roman chars) to a
    // C# string — the bridge for drawing names/labels held in managed record byte[]s.
    public static string PascalToString(byte[] pstr)
    {
        if (pstr is null || pstr.Length == 0) return string.Empty;
        int len = pstr[0];
        if (len == 0) return string.Empty;
        if (len > pstr.Length - 1) len = pstr.Length - 1;
        return MacRomanToString(pstr, 1, len);
    }
    // TETextBox(int,...) real impl lives in MacToolbox.QuickDraw.cs; the byte[]
    // overload stays a no-op there for un-collapsed early-transcription buffers.
    // RectInRgn — Mac: does the rect intersect the region? The game has no real
    // regions (clipping is unbounded), so report "yes" for any valid rect.
    // The prefs keybind-grid redraw (FUN_10044ef4) gates each slot's draw on
    // RectInRgn(slotRect, dialog.visRgn) — returning false hid every slot.
    public static bool RectInRgn(int rectPtr, int rgn) => rectPtr != 0;
    /// Managed-rect mirror of the shim above (rect always "intersects" the visRgn).
    public static bool RectInRgn(short[] rect, int rgn) => rect is { Length: >= 4 };
    /// Managed-string LSetCell — set cell (row = theCell>>16, col 0) to `text`.
    public static void LSetCell(string text, int theCell, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is null) return;
        int row = theCell >> 16;
        if (row >= 0 && row < l.Cells.Length) l.Cells[row] = text;
    }
    /// Select (setIt!=0) or deselect cell (row = theCell>>16). lOnlyOne → single
    /// selection; selecting scrolls the row into view (keyboard navigation).
    public static void LSetSelect(int setIt, int theCell, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is null) return;
        int row = theCell >> 16;
        if (setIt != 0) { l.SelectedRow = row; l.EnsureSelectedVisible(); }
        else if (l.SelectedRow == row) l.SelectedRow = -1;
    }
    public static void LSetDrawingMode(int mode, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is not null) l.DrawingMode = mode;
    }
    /// Creates a managed single-column list of (dataBounds.bottom-top) rows. rView/dataBounds
    /// are {top,left,bottom,right} shorts. cSize/theProc/drawIt/hasGrow/scrollHoriz/scrollVert
    /// match the real 9-arg LNew signature but are unused — the list is always a single
    /// managed column with an always-drawn vertical scrollbar (ListDrawScrollBar).
    public static int LNew(short[] rView, short[] dataBounds, int cSize, int theProc,
                           int theWindow, int drawIt, int hasGrow, int scrollHoriz, int scrollVert)
        => ListNew(rView, dataBounds, theWindow);
    /// List Manager — row count.
    public static short LGetRowCount(int lHandle) => (short)(ResolveList(lHandle)?.RowCount ?? 0);
    /// List Manager — set the selFlags byte (e.g. lOnlyOne 0x80).
    public static void LSetSelFlags(int lHandle, byte flags)
    {
        var l = ResolveList(lHandle); if (l is not null) l.SelFlags = flags;
    }
    public static short HGetVol(int volName, out short vRefNum, out int dirID)
        { vRefNum = 0; dirID = 0; return 0; }
    public static int  LaunchApplication(int launchParams) => 0;
    public static int  GetCursor(int cursorId) => cursorId;   // the standard CURS resources (1, 4, ...) are always present
    public static void SetCursor(int cursorPtr) => HostSetCursor?.Invoke(cursorPtr);
    public static int  GetFInfo(int specPtr) => 0;
    public static int  GetFCreator(int specPtr) => 0;
    public static ushort GetFFlags(int specPtr) => 0;
    public static short HandToHand(ref int handle) => 0;
    public static void DisposeCTable(int ctHandle) => UnregisterColorTable(ctHandle);
    // NewHandleClear now lives in MacToolbox.Resource.cs (real allocator).
    public static void SetDeviceAttribute(int gdHandle, int attribute, int value) { /* no-op shim */ }
    public static void MakeITable(int ctHandle, int itHandle, int res) { /* no-op shim */ }
    public static short QDError() => 0;
    public static short PBGetFCBInfoSync(int paramBlock) => 0;
    // Managed overload: report the current resource file's FCB volume/dir without a Mac
    // paramBlock. No real file system in the game → empty (0/0), success so the caller proceeds.
    public static short PBGetFCBInfoSync(out short vRefNum, out int dirID)
    {
        vRefNum = 0;
        dirID = 0;
        return 0;  // noErr
    }
    // No file system in the game → fnfErr(-43), matching the no-FS convention
    // (PBGetCatInfoSync/FSpDelete/FSMakeFSSpec). This is the form all live callers bind.
    public static short FSpGetFInfo(int vRefNum, out int fileType)
        { fileType = 0; return -43; }
    public static short FSpOpenResFile(int vRefNum, int permission)
        => Mgr_FSpOpenResFile(vRefNum, out short r) ? r : (short)-1;

    // Managed File/Resource-Manager overloads (no Mac paramBlock / Str255 buffer).
    // The game has no real file system, so these report the no-op outcome via managed values.
    /// GetVol — default volume's vRefNum (0 in the game), success.
    public static short GetVol(out short vRefNum) { vRefNum = 0; return 0; }
    /// FSMakeFSSpec with a managed file-name string AND a real spec out: writes the name
    /// into a private Str255 scratch and runs the real spec-builder (for callers whose
    /// name field is a C# string now, e.g. PilotIdentity.Name).
    public static short FSMakeFSSpec(int vRefNum, int dirID, string fileName, int specPtr)
        => Mgr_FSMakeFSSpecByName(fileName, specPtr, out short r) ? r : (short)-43;  // Mgr now always handles it; -43 (fnfErr) if it ever declines, never a false noErr
    /// A host may claim specific FSMakeFSSpec names: return noErr(0)/fnfErr(-43) for a name it
    /// owns, or null to decline (the trap then keeps the no-FS noErr). Override maps ":Pilots" to
    /// the real pilots folder's existence; every other name (e.g. an app-launch FSSpec) is declined.
    public static Func<string, short?>? FsSpecByNameProbe;
    /// FSMakeFSSpec by file name (C# string), no spec out. Consults FsSpecByNameProbe; unclaimed
    /// names fall through to noErr (the spec is unused by those callers).
    public static short FSMakeFSSpec(int vRefNum, int dirID, string fileName)
        => FsSpecByNameProbe?.Invoke(fileName) ?? 0;
    /// OpenResFile by file name. When a host provider is installed (Override wires one from the
    /// data loader's actually-opened forks) it returns the real outcome — a positive refNum for a
    /// present fork, -1 for an absent one. Unset ⇒ the historical no-FS success sentinel (0).
    public static Func<string, int>? ResFileOpener;
    public static int OpenResFile(string fileName) => ResFileOpener?.Invoke(fileName) ?? 0;
    /// PBGetCatInfoSync dir-scan step: report the entry at ioFDirIndex. No FS in the game, so any
    /// index past the (empty) start returns fnfErr(-43) — the scan terminates on the first read.
    public static short PBGetCatInfoSync(short ioFDirIndex, short vRefNum, int dirID, out short entryVRefNum)
    {
        entryVRefNum = vRefNum;
        return ioFDirIndex > 0 ? (short)-43 : (short)0;  // fnfErr past the last entry
    }
    public static short ResolveAliasFileShort(short vRefNum, int resolveAliasChains,
        out byte targetIsFolder, out byte wasAliased)
        { targetIsFolder = 0; wasAliased = 0; return 0; }

    // HiliteControl / SetWRefCon — real impls in MacToolbox.DialogManager.cs.
    /// Dialog Manager SetDialogItem — no-op.
    public static void SetDialogItem(int dialog, int itemNo, int itemType, int itemHandle, int rectPtr) { /* no-op shim */ }
    /// Draw the list rows into the current port (the window). The caller has already
    /// blitted the dialog backdrop and SetPort(window) + TextFont/TextSize.
    public static void LUpdate(int visRgn, int listHandle)
    {
        var l = ResolveList(listHandle); if (l is not null) ListDraw(l);
    }
    /// Dialog Manager Alert — shim returns 1.
    public static short Alert(int alertId, int filterProc) => 1;
    /// Window Manager NewCWindow — allocate a real (minimal) colour window record:
    /// a zeroed 0x9c-byte CWindowRecord whose embedded CGrafPort is initialised by
    /// OpenCPort (portPixMap/visRgn/clipRgn handles, portVersion) with portRect set
    /// to the bounds in window-local coords {0,0,h,w}, the Mac convention. Enough
    /// structure for the render-window chain (SetPort target, portRect reads,
    /// pixmap-handle walks); title/proc/behind are not consumed by any port.
    /// Must build a real port: a return-0 shim leaves ActivePortPixmap 0 and boot dies in
    /// SetScrollViewPosition ("Internal error. (No window?)").
    public static int NewCWindow(int storage, short[] boundsRect, int title,
        int visible, int theProc, int behind, int goAwayFlag, int refCon)
    {
        // Managed CWindow: a MacGrafPort holds the window's embedded colour port
        // (portVersion/pixmap/visRgn/clipRgn/portRect). The raw 0x9c CWindowRecord's
        // non-port fields — visible @ +0x6e, refCon @ +0x98 — had NO Ports reader, so they
        // are dropped. Callers hold the handle as a port (SetPort/GetPortRect/CopyBits dst),
        // which dual-dispatch to the managed branch; an unregistered CopyBits dst key falls
        // back to the screen target (the window IS the screen). `storage` is always 0 here.
        var port = MacGrafPorts.NewPort();
        short h = (short)(boundsRect[2] - boundsRect[0]);
        short w = (short)(boundsRect[3] - boundsRect[1]);
        port.SetPortRectPacked(0, (h << 16) | (w & 0xffff));
        return port.Handle;
    }
    /// Window Manager CloseWindow — no-op.
    public static void CloseWindow(int window) { /* no-op shim */ }
    /// String Manager UpperString — no-op.
    public static void UpperString(int strPtr, int diacSensitive) { /* no-op shim */ }
    // QuickTime Movie shims — EnterMovies reports unavailable (-1) so movie paths bail;
    // IsMovieDone reports done. Everything else is a no-op.
    public static short EnterMovies() => -1;
    public static void ExitMovies() { /* no-op shim */ }
    public static short OpenMovieFile(int fss, out short refNum, byte permission) { refNum = 0; return -1; }
    public static void NewMovieFromFile(int[] movie, int refNum, out short resId,
        int resName, int newActive, int active) { resId = 0; }
    public static void CloseMovieFile(int refNum) { /* no-op shim */ }
    public static void GetMovieBox(int movie, out short topOut) { topOut = 0; }
    public static void SetMovieBox(int movie, int rectPtr) { /* no-op shim */ }
    public static void SetMovieGWorld(int movie, int port, int device) { /* no-op shim */ }
    public static void GoToBeginningOfMovie(int movie) { /* no-op shim */ }
    public static void SetMovieRate(int movie, int rate) { /* no-op shim */ }
    public static void StartMovie(int movie) { /* no-op shim */ }
    public static bool IsMovieDone(int movie) => true;
    public static void MoviesTask(int movie, int maxMillisecs) { /* no-op shim */ }
    public static void DisposeMovie(int movie) { /* no-op shim */ }

    // List Manager
    /// Hit-test a local mouse point: a click in the scrollbar strip scrolls (line/page),
    /// otherwise it selects the row under the cursor (offset by the scroll position),
    /// then — like the real trap — tracks the mouse for as long as the button stays
    /// down: dragging to a different row follows the drag, and dragging past the top/
    /// bottom edge auto-scrolls. LClick redraws the hilite itself as it changes, which
    /// is why callers never re-invalidate the list rect after calling it.
    /// localPoint is packed (v<<16 | h).
    public static void LClick(int localPoint, int modifiers, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is null) return;
        int v = (short)(localPoint >> 16);   // y
        int h = (short)localPoint;           // x
        int top = l.ViewRect[0], bottom = l.ViewRect[2], right = l.ViewRect[3];
        int rowH = l.RowHeight > 0 ? l.RowHeight : 12;
        if (h >= right)
        {
            // The scrollbar strip (outside the cells): stacked bottom arrows =
            // ±1 line, track = ±page vs the thumb; inert when the list fits
            // (disabled control). Redraw like the real control tracking does.
            int delta = ListScrollBarHit(l, h, v);
            if (delta != 0) { l.TopRow += delta; l.ClampTop(); ListDraw(l); }
            return;
        }
        int row = l.TopRow + (v - top) / rowH;
        if (row >= 0 && row < l.RowCount && row != l.SelectedRow) { l.SelectedRow = row; ListDraw(l); }
        while (StillDown())
        {
            int mouse = GetMouse();
            int mv = (short)(mouse >> 16);
            if (mv < top) { if (l.TopRow > 0) { l.TopRow--; l.ClampTop(); ListDraw(l); } }
            else if (mv >= bottom) { if (l.TopRow < l.MaxTopRow) { l.TopRow++; l.ClampTop(); ListDraw(l); } }
            int dragRow = l.TopRow + (Math.Clamp(mv, top, bottom - 1) - top) / rowH;
            if (dragRow >= 0 && dragRow < l.RowCount && dragRow != l.SelectedRow)
            {
                l.SelectedRow = dragRow;
                ListDraw(l);
            }
        }
    }

    /// next==0: is the cell at (theCell>>16) selected? next!=0: advance theCell to the
    /// next selected row at/after it. Single-selection model.
    public static bool LGetSelect(int next, ref int theCell, int lHandle)
    {
        var l = ResolveList(lHandle); if (l is null || l.SelectedRow < 0) return false;
        int row = theCell >> 16;
        if (next == 0) return row == l.SelectedRow;
        if (l.SelectedRow >= row) { theCell = (l.SelectedRow << 16) | (theCell & 0xffff); return true; }
        return false;
    }

    // ModalDialog(int, ref short) — real modal loop in MacToolbox.DialogManager.cs.

    // Resource-fork writers

    /// FSpCreateResFile — real for a managed fork file (marks it for a fresh
    /// empty open so the following AddResource calls rewrite it); else no-op.
    public static void FSpCreateResFile(int specPtr, int creator, int fileType, int scriptTag)
        => Mgr_FSpCreateResFile(specPtr);

    /// FSpDelete — real for a managed fork file: removes it on disk so a re-probing
    /// FSMakeFSSpec returns fnfErr. PilotSave (FUN_1001a778) gates the writer on that fnfErr
    /// (so a no-op here drops every re-save of an existing pilot); also drives the permadeath
    /// pilot-file delete (DeletePilotFileIfExists) and the prefs delete-then-recreate. No-op
    /// (noErr) for unmanaged specs, so deferred paths stay inert. FUN_1006f6d4 class.
    public static short FSpDelete(int specPtr) => Mgr_FSpDelete(specPtr);

    /// UseResFile — sets the current resource file (so AddResource targets a
    /// managed open fork); no-op for unmanaged refnums.
    public static void UseResFile(int refNum) => Mgr_UseResFile(refNum);

    /// AddResource — appends/replaces a resource in the current managed open fork (read from
    /// the Handle's data) with a managed resource-name string; no MacScratch Str255 round-trip.
    public static void AddResource(int handle, int resType, int resId, string name)
        => Mgr_AddResourceByName(handle, resType, resId, name);

    /// UpdateResFile — flushes a dirty managed open fork to disk; else no-op.
    public static void UpdateResFile(int refNum) => Mgr_UpdateResFile(refNum);

    /// CloseResFile — flushes + closes a managed open fork; else no-op.
    public static void CloseResFile(int refNum) => Mgr_CloseResFile(refNum);

    /// FlushVol — no-op. Returns OSErr (0 = noErr).
    public static int FlushVol(int namePtr, int vRefNum) { return 0; }

    // QuickDraw LineTo(x,y) / Line(dh,dv) — real absolute/relative strokes live in
    // MacToolbox.QuickDrawLines.cs. FUN_1006f6d4 class.

    // int-address overloads for transcribed callers that still pass collapsed (int) stack
    // addresses. No-ops, or delegate to the address-form where one exists.
    // Delay with a raw out-ptr param: every real call site passes literal 0
    // (DispatchTitleEvent/PrefsDialogInit), so the elapsed-ticks writeback is discarded;
    // the blocking wait itself is real, same as the int[] form.
    public static void Delay(int ticks, int finalTicksOutPtr) => SleepTicks(ticks);
    public static int  SndDoCommand(int channel, int cmdPtr, bool noWait) => 0;
    public static int  SndDoImmediate(int channel, int cmdPtr) => 0;
    // DrawPicture(int, int) and CopyBits(int×6) — real impls live in MacToolbox.QuickDraw.cs.
    public static void NewMovieFromFile(int moviePtr, int refNum, out short resId,
                                         short newMovieActive, out byte dataRefWasChanged,
                                         int dataRef, int dataRefType)
    {
        resId = 0; dataRefWasChanged = 0;
    }

    // Inverse direction: a few int-taking stubs are invoked with managed byte[]
    // (un-collapsed `local_xxx` arrays). The byte[] overload is a no-op so the call compiles.
    /// NumToString into a managed Str255 byte[] (length byte at [0], ASCII decimal chars
    /// after). Must stay real: FormatCredits' thousand/million number segments and
    /// ResolveShareWarePlaceholder's day/hour counts draw via the following DrawString(buf).
    public static void NumToString(int value, byte[] bufPtr)
    {
        if (bufPtr is null || bufPtr.Length == 0) return;
        string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int max = bufPtr.Length - 1;
        if (s.Length > max) s = s.Substring(0, max);
        bufPtr[0] = (byte)s.Length;
        for (int i = 0; i < s.Length; i++) bufPtr[1 + i] = (byte)s[i];
    }

    /// GetIndString into a managed Str255 byte[] (length byte + chars). Must stay real:
    /// ResolveShareWarePlaceholder's STR# 900 owner-name/reg-code lookups read this buffer.
    public static void GetIndString(byte[] destPtr, short listId, short index)
    {
        if (destPtr is null || destPtr.Length == 0) return;
        string s = GetIndString(listId, index);
        int max = destPtr.Length - 1;
        // Encode back to Mac-Roman bytes (StringToPascalBytes → cp 10000), NOT (byte)s[i] — a
        // Latin-1 encode that would turn a curly apostrophe U+2019 into 0x19 (same decode/encode
        // Mac-Roman-vs-Latin-1 class as the dësc/STR loaders). StringToPascalBytes returns
        // [len][Mac-Roman body]; clamp the body to the caller's Str255 capacity.
        byte[] pstr = StringToPascalBytes(s);
        int len = Math.Min(pstr[0], max);
        destPtr[0] = (byte)len;
        Array.Copy(pstr, 1, destPtr, 1, len);
    }
    public static void TETextBox(byte[] textPtr, int length, int rectPtr, int align) { /* no-op shim */ }
    public static void SetPort(byte[] port) { /* no-op shim */ }
    public static void PaintRect(byte[] rectPtr) { /* no-op shim */ }

    public static class Components
    {
        // Subset namespace so callers can write `MacToolbox.Components.TickCount.Get()`
        public static class TickCount
        {
            public static uint Get() => OpenEV.Platform.Toolbox.TickCount.Get();
        }
    }
}
