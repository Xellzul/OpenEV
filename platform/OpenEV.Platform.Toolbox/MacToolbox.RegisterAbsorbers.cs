// Mechanical-transcription wrong-arity/missing-trap ABSORBERS for the Register port. The
// mechanical transcriptions call many Mac Toolbox traps with decompile-lost args (no/extra args)
// or traps the shim never needed (Apple Events, Component Mgr, MacTCP/serial
// driver, Printing, Control Mgr, full TextEdit/Menu Mgr). Each is a no-op 'params
// object?[]' overload returning an Absorb that implicitly converts to any scalar/
// string, so overload resolution succeeds in every return context. Additive: the
// typed overloads still win for exact-match calls, so the game is unaffected. These
// graduate to real wiring as the Register UI is built out.
namespace OpenEV.Platform.Toolbox;

/// Universal absorber return — implicitly converts to whatever the call site assigns.
public readonly struct Absorb
{
    public static implicit operator int(Absorb _) => 0;
    public static implicit operator uint(Absorb _) => 0;
    public static implicit operator short(Absorb _) => 0;
    public static implicit operator ushort(Absorb _) => 0;
    public static implicit operator byte(Absorb _) => 0;
    public static implicit operator sbyte(Absorb _) => 0;
    public static implicit operator long(Absorb _) => 0;
    public static implicit operator ulong(Absorb _) => 0;
    public static implicit operator char(Absorb _) => '\0';
    public static implicit operator bool(Absorb _) => false;
    public static implicit operator double(Absorb _) => 0;
    public static implicit operator float(Absorb _) => 0;
    public static implicit operator string?(Absorb _) => null;
}

public static partial class MacToolbox
{
    public static Absorb AECountItems(params object?[] _) => default;
    public static Absorb AECreateAppleEvent(params object?[] _) => default;
    public static Absorb AECreateDesc(params object?[] _) => default;
    public static Absorb AEDisposeDesc(params object?[] _) => default;
    public static Absorb AEGetAttributePtr(params object?[] _) => default;
    public static Absorb AEGetNthPtr(params object?[] _) => default;
    public static Absorb AEGetParamDesc(params object?[] _) => default;
    public static Absorb AEInstallEventHandler(params object?[] _) => default;   // AE handler install (FUN_1000001c)
    public static Absorb AEProcessAppleEvent(params object?[] _) => default;
    public static Absorb AEPutParamPtr(params object?[] _) => default;
    public static Absorb AESend(params object?[] _) => default;
    public static Absorb AppendMenu(params object?[] _) => default;
    public static Absorb CalcMenuSize(params object?[] _) => default;
    public static Absorb CloseComponent(params object?[] _) => default;
    public static Absorb DeleteMenu(params object?[] _) => default;
    public static Absorb DeleteMenuItem(params object?[] _) => default;
    public static Absorb DetachResource(params object?[] _) => default;
    public static Absorb DIBadMount(params object?[] _) => default;     // Disk Init Mgr (event loop diskEvt)
    public static Absorb DILoad(params object?[] _) => default;
    public static Absorb DIUnload(params object?[] _) => default;
    public static Absorb DisableItem(params object?[] _) => default;
    public static Absorb DisposeControl(params object?[] _) => default;
    public static Absorb DisposeMenu(params object?[] _) => default;
    public static Absorb DragWindow(params object?[] _) => default;
    public static Absorb DrawChar(params object?[] _) => default;
    public static Absorb DrawControls(params object?[] _) => default;
    public static Absorb Delay(params object?[] _) => default;                  // no-arg form (zoom anim FUN_1000088c)
    public static Absorb EnableItem(params object?[] _) => default;
    public static Absorb MenuKey(params object?[] _) => default;
    public static Absorb FSpCreate(params object?[] _) => default;
    public static Absorb FSpOpenDF(params object?[] _) => default;
    public static Absorb FindControl(params object?[] _) => default;
    public static Absorb GetFNum(params object?[] _) => default;
    public static Absorb GetFontInfo(params object?[] _) => default;
    public static Absorb GetItemIcon(params object?[] _) => default;
    public static Absorb GetMenu(params object?[] _) => default;
    public static Absorb GetScrap(params object?[] _) => default;
    public static Absorb GetWDInfo(params object?[] _) => default;
    public static Absorb HOpenResFile(params object?[] _) => default;
    public static Absorb IUDateString(params object?[] _) => default;
    public static Absorb InsertMenu(params object?[] _) => default;
    public static Absorb LMGetSysFontFam(params object?[] _) => default;
    public static Absorb LMGetSysFontSize(params object?[] _) => default;
    public static Absorb Move(params object?[] _) => default;
    public static Absorb NewControl(params object?[] _) => default;
    public static Absorb NewMenu(params object?[] _) => default;
    public static Absorb NewRoutineDescriptor(params object?[] _) => default;   // AE handler UPP (FUN_1000001c)
    public static Absorb OpenDefaultComponent(params object?[] _) => default;
    public static Absorb OpenDriver(params object?[] _) => default;
    public static Absorb PBControlSync(params object?[] _) => default;
    public static Absorb PBHGetFInfoSync(params object?[] _) => default;
    public static Absorb PopUpMenuSelect(params object?[] _) => default;
    public static Absorb PrClose(params object?[] _) => default;
    public static Absorb PrCloseDoc(params object?[] _) => default;
    public static Absorb PrClosePage(params object?[] _) => default;
    public static Absorb PrError(params object?[] _) => default;
    public static Absorb PrJobDialog(params object?[] _) => default;
    public static Absorb PrOpen(params object?[] _) => default;
    public static Absorb PrOpenDoc(params object?[] _) => default;
    public static Absorb PrOpenPage(params object?[] _) => default;
    public static Absorb PrPicFile(params object?[] _) => default;
    public static Absorb PrintDefault(params object?[] _) => default;
    public static Absorb RealFont(params object?[] _) => default;
    public static Absorb StandardPutFile(params object?[] _) => default;
    public static Absorb SystemEdit(params object?[] _) => default;
    public static Absorb SystemTask(params object?[] _) => default;
    public static Absorb TEClick(params object?[] _) => default;
    public static Absorb TECopy(params object?[] _) => default;
    public static Absorb TECut(params object?[] _) => default;
    public static Absorb TEIdle(params object?[] _) => default;
    public static Absorb TEKey(params object?[] _) => default;
    public static Absorb TEPaste(params object?[] _) => default;
    public static Absorb TESetText(params object?[] _) => default;
    public static Absorb TrackControl(params object?[] _) => default;
    public static Absorb ValidRect(params object?[] _) => default;
    public static Absorb Alert(params object?[] _) => default;
    public static Absorb BlockMoveData(params object?[] _) => default;
    public static Absorb CloseResFile(params object?[] _) => default;
    public static Absorb DisposeCIcon(params object?[] _) => default;
    public static Absorb FSpDelete(params object?[] _) => default;
    public static Absorb FlushVol(params object?[] _) => default;
    public static Absorb GetCIcon(params object?[] _) => default;
    public static Absorb GetControlValue(params object?[] _) => default;
    public static Absorb GetCursor(params object?[] _) => default;
    public static Absorb GetPicture(params object?[] _) => default;
    public static Absorb HLock(params object?[] _) => default;
    public static Absorb HLockHi(params object?[] _) => default;
    public static Absorb HNoPurge(params object?[] _) => default;
    public static Absorb HPurge(params object?[] _) => default;
    public static Absorb HUnlock(params object?[] _) => default;
    public static Absorb InsetRect(params object?[] _) => default;
    public static Absorb LineTo(params object?[] _) => default;
    public static Absorb MoveHHi(params object?[] _) => default;
    public static Absorb MoveTo(params object?[] _) => default;
    // NumToString zero/var-arg form for the Register port's decompile-lost-arg call (the alert in
    // FUN_10000b5c). The typed NumToString(int, byte[]) still wins for exact-match calls, so the game
    // is unaffected. Added to complete the re-sync's absorber list (NumToString was the one
    // omitted trap among the sibling entries this function already uses, e.g. GetIndString/Alert).
    public static Absorb NumToString(params object?[] _) => default;
    public static Absorb NewPtr(params object?[] _) => default;
    public static Absorb NewPtrClear(params object?[] _) => default;

    // Register-gated mechanical-transcription heap. The register pane builders call NO-ARG NewPtr()/NewPtrClear()/
    // NewHandle() (the size is decompile-lost); these bind to the no-arg forms below. When the host
    // wires HeapAllocImpl (RegMem.Alloc) they return a real block, so the builders (FUN_1000ada0
    // text nodes, FUN_10009680 cicn buttons) stop bailing on a 0 pointer. The game leaves
    // HeapAllocImpl null → they return 0 (identical to the old no-op absorber), so the game is
    // unaffected. Block size is a generous fixed value (the real size was lost; RegMem.Alloc is a
    // bump allocator, so over-allocating is free).
    public static System.Func<int, int>? HeapAllocImpl;
    private const int RegHeapBlock = 512;
    public static int NewPtr()      => HeapAllocImpl?.Invoke(RegHeapBlock) ?? 0;
    public static int NewPtrClear() => HeapAllocImpl?.Invoke(RegHeapBlock) ?? 0;

    // Window Manager bring-up for the Register port (FUN_1000158c → GetNewCWindow, FUN_1000088c →
    // ShowWindow). The host wires these to create/reveal its real SDL window; the game leaves them
    // null (no GetNewCWindow caller in the game), so the game is unaffected. The Register app wires GetNewCWindow
    // (via RegisterBootAdapter) but not the SDL factory, so it still returns the window handle with no
    // window — identical to the old direct assignment.
    //
    // The register app only ever creates the one window (WIND 128, PreflightAndOpenMainWindow's sole
    // GetNewCWindow call), so tracking "frontmost" as "the last window GetNewCWindow returned" is exact
    // for its whole lifetime — FrontWindow() (MacToolbox.cs) reads this back for NullEventIdleHandler's
    // `RegisterGlobals.MainWindow == FrontWindow()` gate.
    private static int _frontWindowHandle;
    public static System.Func<int, int>? GetNewCWindowImpl;
    public static int GetNewCWindow(int windResId)
    {
        int w = GetNewCWindowImpl?.Invoke(windResId) ?? 0;
        if (w != 0) _frontWindowHandle = w;
        return w;
    }
    public static System.Action? ShowWindowHostImpl;

    // System scrap (clipboard) bridge for the Register port. The Mac delegated Cut/Copy/Paste to
    // TextEdit, which syncs with the system scrap — the desktop-wide clipboard (the original gates
    // its Paste menu item on GetScrap, .c:4329). The host wires these to the OS clipboard so the
    // register fields copy/paste across the app boundary. The game leaves them null (no scrap
    // caller in the game), so RegisterTextFields falls back to
    // its in-app scrap, behaviour-identical to before.
    public static System.Func<string?>? HostClipboardGet;
    public static System.Action<string>? HostClipboardSet;
    public static Absorb OffsetRect(params object?[] _) => default;
    public static Absorb ReleaseResource(params object?[] _) => default;
    public static Absorb SetCCursor(params object?[] _) => default;     // color cursor (NullEventIdleHandler)
    public static Absorb ShowWindow(params object?[] _) { ShowWindowHostImpl?.Invoke(); return default; }   // reveals the host window (zoom anim FUN_1000088c)
    public static Absorb SetCursor(params object?[] _) => default;
    public static Absorb SetHandleSize(params object?[] _) => default;
    public static Absorb SetRect(params object?[] _) => default;
    public static Absorb StringWidth(params object?[] _) => default;
    public static Absorb TEActivate(params object?[] _) => default;
    public static Absorb TECalText(params object?[] _) => default;
    public static Absorb TEDelete(params object?[] _) => default;
    public static Absorb TEDispose(params object?[] _) => default;
    public static Absorb TEInsert(params object?[] _) => default;
    public static Absorb TESetSelect(params object?[] _) => default;
    public static Absorb TEStyleInsert(params object?[] _) => default;
    public static Absorb TEStyleNew(params object?[] _) => default;
    public static Absorb TextFace(params object?[] _) => default;
    public static Absorb TextFont(params object?[] _) => default;
    public static Absorb TextSize(params object?[] _) => default;
    public static Absorb UpperString(params object?[] _) => default;
    public static Absorb p2cstr(params object?[] _) => default;
    public static Absorb ClipRect(params object?[] _) => default;
    public static Absorb DrawPicture(params object?[] _) => default;
    public static Absorb DrawString(params object?[] _) => default;
    public static Absorb EqualString(params object?[] _) => default;
    public static Absorb EraseRect(params object?[] _) => default;
    public static Absorb FrameRect(params object?[] _) => default;
    public static Absorb Gestalt(params object?[] _) => default;
    public static Absorb GetDateTime(params object?[] _) => default;
    public static Absorb GetIndResource(params object?[] _) => default;
    public static Absorb GetIndString(params object?[] _) => default;
    public static Absorb GetResource(params object?[] _) => default;
    public static Absorb InvalRect(params object?[] _) => default;
    public static Absorb PlotCIcon(params object?[] _) => default;
    public static Absorb PtInRect(params object?[] _) => default;
    public static Absorb SetPort(params object?[] _) => default;
    public static Absorb SndPlay(params object?[] _) => default;
    public static Absorb TETextBox(params object?[] _) => default;
    public static Absorb TEUpdate(params object?[] _) => default;
    // GetEOF — File Manager logical-EOF query. Register port calls it with decompile-lost
    // args; no shim exists for it, so absorb (returns 0 = no error / empty file).
    public static Absorb GetEOF(params object?[] _) => default;
    // NewHandleClear zero/var-arg form (the typed NewHandleClear(int) still wins for sized
    // calls). Register port allocates with decompile-lost size args; absorb returns 0 (null
    // handle) — callers branch on != 0, matching the decompile's failure handling.
    public static Absorb NewHandleClear(params object?[] _) => default;
}
