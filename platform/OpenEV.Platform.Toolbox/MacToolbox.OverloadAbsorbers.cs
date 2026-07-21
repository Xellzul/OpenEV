// Overloads that satisfy C# overload resolution for MacToolbox stubs whose
// transcribed callers pass un-collapsed byte[]/int[] stack buffers or extra
// args. Some are REAL (PaintRect/FrameRect/DrawString drive the in-game HUD,
// radar, and text); others are no-op absorbers, marked as such. When a call
// site graduates to real wiring, rewrite it to the concrete typed overload; a
// then-dead absorber is safe to delete.

using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

public static partial class MacToolbox
{
    // PaintRect / FrameRect called with short[] Rect (collapsed from 4-short
    // stack locals). Real impls: needed for the in-game HUD/radar, which build
    // their Rects on the stack (DrawRadarHud's radar-rect fill + blip frames).
    public static void PaintRect(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) return;
        var color = _activeForeColor;   // honour RGBForeColor (see QuickDrawLines note)
        EnqueueDraw(c => c.FillRect(rc, color));
    }
    public static void FrameRect(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var r = RectFromShorts(rect);
        if (r.Width <= 0 || r.Height <= 0) return;
        var color = _activeForeColor;   // honour RGBForeColor (radar ship blips)
        EnqueueDraw(c =>
        {
            c.FillRect(new RectI(r.X, r.Y, r.Width, 1), color);
            c.FillRect(new RectI(r.X, r.Bottom - 1, r.Width, 1), color);
            c.FillRect(new RectI(r.X, r.Y, 1, r.Height), color);
            c.FillRect(new RectI(r.Right - 1, r.Y, 1, r.Height), color);
        });
    }

    // GetDialogItem callers pass any mix of (byte[], int[], int) for
    // the last three params. The two existing typed overloads cover
    // the common cases — this absorbs everything else.
    public static void GetDialogItem(int dialog, int itemNo, params object?[] _) { }

    // SetDialogItem with a managed short[] rect (dialog 4-rules B9 —
    // ShowSharewareNagDialog installs its userItem draw procs through this).
    // No-op like the int-rectPtr form in MacToolbox.cs.
    public static void SetDialogItem(int dialog, int itemNo, int itemType, int itemHandle, short[] rect) { /* no-op shim */ }

    // DrawString called with byte[] (Pascal string buffer).
    public static void DrawString(byte[] pstr)
    {
        var fontSys = ResolveFont();
        if (fontSys is null || pstr is null || pstr.Length == 0) return;
        int len = pstr[0] & 0xff;
        if (len == 0) return;
        var bytes = new byte[len];
        for (int i = 0; i < len && i + 1 < pstr.Length; i++) bytes[i] = pstr[i + 1];
        string s = MacRomanToString(bytes);
        int pixelSize = _textSize > 0 ? _textSize : 12;
        int penX = _penX, penY = _penY;
        var color = _activeForeColor;
        bool bold = (_textFace & 1) != 0 || (_textFont == 0 && SystemFont is null);   // TextFace(1) bold + systemFont faux-bold only when no real Chicago is wired
        // Baseline at the pen (Mac MoveTo semantics) — top = penY - ascent, matching the
        // string overload (was penY - pixelSize, which rode high when ascent < size).
        int top = penY - fontSys.Ascent(pixelSize);
        EnqueueDraw(c =>
        {
            fontSys.DrawText(c, s, penX, top, color, pixelSize);
            if (bold) fontSys.DrawText(c, s, penX + 1, top, color, pixelSize);
        });
        // Mac _DrawString advances the pen by the text width (same contract as the
        // string overload) — mutate _penX synchronously so consecutive no-MoveTo
        // DrawString calls don't overprint.
        _penX += fontSys.MeasureWidth(s, pixelSize);
    }

    // GlobalToLocal called with un-collapsed int[2] point buffer.
    public static void GlobalToLocal(int[] pointBuf) { }
    public static void GlobalToLocal(short[] pointBuf) { }
    public static void GlobalToLocal(byte[] pointBuf) { }

    // ParamText called with mixed/un-collapsed args (e.g. RunMultiButtonModalDialog
    // passes four int[] buffers). Funnels into the Dialog Manager's ^0..^3 store;
    // int=Str255 ptr, byte[]=buffer, other shapes leave the slot unchanged.
    public static void ParamText(params object?[] args) => ApplyParamTextArgs(args);

    // Gestalt called with (selector, out byte/short/long/uint) —
    // MacToolbox.cs already has the int overload. All return Mac OSErr
    // (short, stubbed to 0) for transcribed callers that capture the return.
    public static short Gestalt(int selector, out uint dataOut) { dataOut = 0; return 0; }
    public static short Gestalt(int selector, out short dataOut) { dataOut = 0; return 0; }
    public static short Gestalt(int selector, out byte dataOut) { dataOut = 0; return 0; }
    public static short Gestalt(int selector, out long dataOut) { dataOut = 0; return 0; }
    // Some transcribed callers declared the response slot as un-collapsed
    // uint[] / int[] and passed it as `out`. Accept both shapes.
    public static short Gestalt(int selector, out uint[] dataOut) { dataOut = new uint[1]; return 0; }
    public static short Gestalt(int selector, out int[] dataOut)  { dataOut = new int[1];  return 0; }

    // GetDateTime called with un-collapsed seconds buffers / out-params — all return
    // the real current Mac epoch seconds (see GetDateTimeSeconds in MacToolbox.cs).
    public static void GetDateTime(int[] secondsBuf) { if (secondsBuf?.Length > 0) secondsBuf[0] = GetDateTimeSeconds(); }
    public static void GetDateTime(uint[] secondsBuf) { if (secondsBuf?.Length > 0) secondsBuf[0] = (uint)GetDateTimeSeconds(); }
    public static void GetDateTime(out int seconds) { seconds = GetDateTimeSeconds(); }
    public static void GetDateTime(out uint seconds) { seconds = (uint)GetDateTimeSeconds(); }

    // CopyBits with varied param types — the transcription widens shorts to ints
    // or passes un-collapsed Rect arrays.
    public static void CopyBits(params object?[] _) { }

    // HGetVol — the transcription passes un-collapsed array buffers for outs.
    public static short HGetVol(int[] volNameBuf, out short vRefNum, out int dirID) { vRefNum = 0; dirID = 0; return 0; }
    public static short HGetVol(byte[] volNameBuf, out short vRefNum, out int dirID) { vRefNum = 0; dirID = 0; return 0; }

    // GetPort — the transcription passes un-collapsed byte[] for portOut.
    public static void GetPort(byte[] portOut) { }
    public static void GetPort(short[] portOut) { }

    // FSpOpenResFile / FSpGetFInfo / FSpCreateResFile — the transcription passes
    // un-collapsed FSSpec byte[] buffer for the spec arg.
    public static short FSpOpenResFile(int[] specBuf, int permission) => -1;
    public static short FSpOpenResFile(byte[] specBuf, int permission) => -1;
    // No file system in the game → the spec names a non-existent file → fnfErr(-43), matching the
    // File-Manager no-FS convention (PBGetCatInfoSync/FSpDelete/FSMakeFSSpec all return -43). A
    // false noErr would let a catalog scan (FUN_1006189c `while(err==0)`) run past a missing entry.
    public static short FSpGetFInfo(int[] specBuf, out int fileType) { fileType = 0; return -43; }
    public static short FSpGetFInfo(byte[] specBuf, out int fileType) { fileType = 0; return -43; }
    public static void FSpCreateResFile(int[] specBuf, int creator, int fileType, int scriptTag) { }
    public static void FSpCreateResFile(byte[] specBuf, int creator, int fileType, int scriptTag) { }

    // SndNewChannel — the transcription passes un-collapsed array buffer.
    public static int SndNewChannel(int[] chBuf, short synthType, int initFlags, int sndUPP, byte param_5) => 0;
    public static short SndNewChannel(int[] chBuf, short synthType, byte initFlags, int sndUPP) => 0;
    // Session 38: 3-arg int[] form for the probe-channel pattern
    // (caller passes only the array slot + synthType + initFlags).
    public static int SndNewChannel(int[] chBuf, int synthType, int initFlags) => -1;

    // FSMakeFSSpec — the transcription passes un-collapsed int[3] for the FSSpec out
    // slot. Real impl writes vRefNum/parID/name into the buffer; stub
    // accepts and ignores.
    public static short FSMakeFSSpec(int vRefNum, int dirID, int fileName, int[] specBuf) => 0;
    public static short FSMakeFSSpec(int vRefNum, int dirID, int fileName, byte[] specBuf) => 0;

    // These 0-arg / wrong-arity overloads bind toolbox calls whose args the
    // mechanical transcription dropped; semantics deferred to later wiring.

    // No-arg NewHandle: the decompile dropped the size arg. Allocates a fixed 512
    // via the register's managed heap when HeapAllocImpl is wired (RegMem.Alloc),
    // else 0. The game has no no-arg caller (leaves it null); the register's
    // PrintRegistration (FUN_1000279c) uses it. Guard for any future wiring:
    // recover the true size and call NewHandle(int size) — never let this return 0
    // (0 is the Mac NULL-handle allocation-failure sentinel callers test + deref).
    public static int  NewHandle() => HeapAllocImpl?.Invoke(512) ?? 0;
    public static int  GetHandleSize() => 0;
    public static void DisposeRoutineDescriptor() { }
    public static void DisposeRoutineDescriptor(int desc, int extra) { }
    public static void DisposeHandle() { }
    public static void DisposePtr() { }
    public static void TEDeactivate() { }

    // CallUniversalProc — the transcription emits arbitrary positional shapes.
    public static void CallUniversalProc(int arg1, int arg2) { }
    public static void CallUniversalProc(int arg1, int arg2, int arg3, int arg4) { }
    public static void CallUniversalProc(params object?[] _) { }

    // GetCWMgrPort — the transcription sometimes passes an out int or an int address.
    public static void GetCWMgrPort(out int port) { port = 0; }
    public static int  GetCWMgrPort(int portAddr) => 0;

    // ResolveAliasFile — Mac 4-arg form: OSErr ResolveAliasFile(FSSpec*, resolveAliasChains,
    // out targetIsFolder, out wasAliased). The first arg is the FSSpec ptr (the FsSpec token's
    // Addr), NOT a vRefNum — every live caller passes a spec. Port-native: redirect a "Last
    // Pilot" pointer spec to its real target so the boot auto-load opens the real pilot under
    // its real name; all other specs pass through untouched (the old inert behaviour).
    // targetIsFolder MUST stay 0 for files — the pilot loader's guard aborts the load on
    // non-zero (LoadPluginPilotData line 42); wasAliased reports whether a redirect happened.
    public static short ResolveAliasFile(int specPtr, int chains, out byte targetIsFolder, out byte wasAliased)
    {
        targetIsFolder = 0;
        wasAliased = TryResolveLastPilotSpec(specPtr) ? (byte)1 : (byte)0;
        return 0;
    }

    // FindWindow — Mac Window Manager part-code dispatch.
    // The catch-all in UnwiredStubs returns 0 (= inDesk), which
    // sends DispatchTitleEvent's `else if (sVar4 < 2) { if (sVar4 == 0)
    // SysBeep }` branch and the click never reaches HitTestTitleButton.
    // The game only has the single virtual-target window; treat every click
    // as inContent (3) so DispatchTitleEvent falls into the
    // `else if (sVar4 < 4)` HitTest branch.
    public static int FindWindow(int packedPoint, int outWindowOrPtr) => 3;

    // BlockMoveData — the transcription occasionally drops the count arg.
    public static void BlockMoveData(int src, int dst) { }

    // SndChannelStatus — the transcribed 3-arg form. POISON (was `=> 0`): returning noErr signals a
    // VALID SCStatus while leaving statusBuf unwritten, but the Mac caller (FUN_10076528 /
    // IsChannelBusy) reads scChannelBusy from that buffer on noErr → garbage busy/idle. DEAD today
    // (the live IsChannelBusy uses the 1-arg tuple overload SndChannelStatus(chPtr)=>(ok,isBusy),
    // MacToolbox.cs); fail-loud if a port ever wires this verbatim form. Same class as NewHandle().
    public static short SndChannelStatus(int chPtr, int len, int statusBuf) => throw new System.NotImplementedException(
        "SndChannelStatus 3-arg absorber: noErr would signal a valid SCStatus with statusBuf unwritten (caller reads scChannelBusy on noErr). Use the 1-arg overload SndChannelStatus(chPtr) => (ok, isBusy).");
    public static short SndChannelStatus(int chPtr, short len, int statusBuf) => throw new System.NotImplementedException(
        "SndChannelStatus 3-arg absorber (short len): same as the int-len form — use the 1-arg (ok, isBusy) overload.");

    // PBHGetVolParmsSync — the transcribed 1-arg dropped-arg form.
    public static short PBHGetVolParmsSync(int paramBlock) => 0;

    // GetCWMgrPort — un-collapsed int[] out form.
    public static void GetCWMgrPort(int[] portBuf) { if (portBuf is not null && portBuf.Length > 0) portBuf[0] = 0; }
    public static void GetCWMgrPort(out int[] portBuf) { portBuf = new int[1]; }
}
