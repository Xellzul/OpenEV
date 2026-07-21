using System;

namespace OpenEV.Platform.Toolbox;

// Empty shims for Mac Toolbox routines the mechanically transcribed ports
// call. Each method accepts `params object?[]` and returns `int default` (0).
// Real implementations will replace these one-by-one as FUN_xxx callers
// get wired up.
public static partial class MacToolbox
{
    public static int AppendResMenu(params object?[] _) => default;
    public static int BackPat(params object?[] _) => default;
    public static int BitMapToRegion(params object?[] _) => default;
    // NO-OP: deliberate. Its one live consumer (CopyPixMapData) is a broken Pass-1
    // transcription whose bytes the texture-based host never reads; wiring it would
    // turn benign no-ops into bogus low-address writes. BlockMoveData(byte[],…) is real.
    public static int BlockMove(params object?[] _) => default;
    public static int ClosePort(params object?[] _) => default;
    public static int CloseRgn(params object?[] _) => default;
    public static int CountResources(params object?[] _) => default;
    public static int CountVoices(params object?[] _) => default;
    public static int DebugStr(params object?[] _) => default;
    public static int DiffRgn(params object?[] _) => default;
    public static int DisposeGDevice(params object?[] _) => default;
    public static int DisposeSpeechChannel(params object?[] _) => default;
    // Classic InitRenderWindow (FUN_100738e8) disposes a degenerate old game window
    // before creating a fresh one; the host window record is unbacked — nothing to free.
    public static int DisposeWindow(params object?[] _) => default;
    public static int FSClose(params object?[] _) => default;
    // The game has no Mac file system. FSRead must report end-of-file (eofErr,
    // -39) rather than success, or read loops (e.g. the pilot-prefs scan in
    // LoadOrInitPilotPrefsRecord) spin forever on a "successful" empty read.
    public static int FSRead(params object?[] _) => -39;  // eofErr
    public static int FSWrite(params object?[] _) => default;
    public static int FSpSetFInfo(params object?[] _) => default;
    public static int FillCRect(params object?[] _) => default;
    // The game has no Mac folder hierarchy (no Preferences folder), so FindFolder
    // must report not-found (fnfErr, -43). Returning success (0) sends the
    // prefs paths (LoadOrInitPilotPrefsRecord etc.) into file I/O on a
    // phantom folder. With -43 they take their clean "no prefs" early-out.
    public static int FindFolder(params object?[] _) => -43;  // fnfErr
    // Typed managed overload: no Folder Manager in the game, so returns fnfErr and leaves the
    // out FSSpec components zeroed (callers take their clean "no prefs folder" early-out).
    public static int FindFolder(int refType, int folderType, int create, out short vRefNum, out int dirID)
    {
        vRefNum = 0;
        dirID = 0;
        return -43;  // fnfErr
    }
    public static int FindWindow(params object?[] _) => default;
    public static int FrameOval(params object?[] _) => default;
    public static int GetCTSeed(params object?[] _) => default;
    public static int GetCTable(params object?[] _) => default;
    public static int GetDialogItemText(params object?[] _) => default;
    public static int GetKeys(params object?[] _) => default;
    /// Mac GetKeys — fill a caller short array (PollFirstHeldUserKey passes
    /// a ushort[14] KeyMap buffer). word w bit b ⇒ Mac keycode w*16+b held.
    public static int GetKeys(ushort[] dest)
    {
        if (dest is not null)
        {
            var km = HostKeymapSnapshot();   // one consistent frame
            for (int i = 0; i < dest.Length; i++)
                dest[i] = i < 8 ? km[i] : (ushort)0;
        }
        return 0;
    }
    public static int GetMemFragment(params object?[] _) => default;
    public static int GetMenuHandle(params object?[] _) => default;
    public static int GetMenuItemText(params object?[] _) => default;
    public static int GetNewMBar(params object?[] _) => default;
    public static int GetPenState(params object?[] _) => default;
    public static int GetResInfo(params object?[] _) => default;
    public static int GetResourceSizeOnDisk(params object?[] _) => default;
    public static int GetSysBeepVolume(params object?[] _) => default;
    // Typed overload: the Mac OS alert (SysBeep) volume, 0..7, written to level[0].
    // There is no host equivalent for the OS-level alert-volume setting, but a stock
    // Mac defaulted NON-muted, so report a nonzero level. DisposeSoundFileChannel —
    // the sole caller — gates its music-teardown chirp on `level[0] > 0`, so a nonzero
    // reading restores the chirp a normal Mac played (About box, game entry). The game
    // never reads the magnitude, only `> 0`. (User-approved 2026-07-16: faithful default
    // = a non-muted Mac; the params-absorber above returned 0 = the atypical muted case.)
    public static int GetSysBeepVolume(int[] level)
    {
        if (level is not null && level.Length > 0)
            level[0] = 7;   // Mac default alert volume (nonzero); only `> 0` is tested
        return 0;   // noErr
    }
    public static int GetToolboxTrapAddress(params object?[] _) => default;
    public static int GetVol(params object?[] _) => default;
    public static int GetWMgrPort(params object?[] _) => default;
    public static int HCreate(params object?[] _) => default;
    public static int HGetFInfo(params object?[] _) => default;
    // The game has no Mac file system, so a prefs/plugin file can't be opened.
    // Return fnfErr (-43) so callers take their "no file" path (e.g.
    // LoadOrInitPilotPrefsRecord → init defaults) instead of entering a
    // read loop on a phantom "successfully opened" file.
    public static int HOpen(params object?[] _) => -43;  // fnfErr
    public static int HSetFInfo(params object?[] _) => default;
    public static int HiliteMenu(params object?[] _) => default;
    public static int InitDialogs(params object?[] _) => default;
    public static int InitFonts(params object?[] _) => default;
    public static int InitGraf(params object?[] _) => default;
    public static int InitMenus(params object?[] _) => default;
    public static int InitWindows(params object?[] _) => default;
    public static int InsTime(params object?[] _) => default;
    public static int LDispose(params object?[] _) => default;
    public static int LMGetMBarHeight(params object?[] _) => default;
    public static int LMGetTicks(params object?[] _) => default;
    public static int LMGetTime(params object?[] _) => default;
    public static int LMSetMBarHeight(params object?[] _) => default;
    public static int Line(params object?[] _) => default;
    public static int LoadResource(params object?[] _) => default;
    public static int LocalToGlobal(params object?[] _) => default;
    public static int MaxApplZone(params object?[] _) => default;
    public static int MenuSelect(params object?[] _) => default;
    public static int MoreMasters(params object?[] _) => default;
    public static int MoveWindow(params object?[] _) => default;
    public static int Munger(params object?[] _) => default;
    public static int NGetTrapAddress(params object?[] _) => default;
    public static int NMRemove(params object?[] _) => default;
    public static int NewAlias(params object?[] _) => default;
    public static int NewDialog(params object?[] _) => default;
    public static int NewWindow(params object?[] _) => default;
    public static int OpenDeskAcc(params object?[] _) => default;
    public static int OpenPort(params object?[] _) => default;
    public static int OpenResFile(params object?[] _) => default;
    public static int OpenRgn(params object?[] _) => default;
    public static int PaintOval(params object?[] _) => default;
    public static int PrimeTime(params object?[] _) => default;
    public static int RGB2HSL(params object?[] _) => default;
    public static int ReadPartialResource(params object?[] _) => default;
    public static int ResError(params object?[] _) => default;
    public static int RmvTime(params object?[] _) => default;
    public static int SelectDialogItemText(params object?[] _) => default;
    public static int SetControlTitle(params object?[] _) => default;
    public static int SetControlValue(params object?[] _) => default;
    public static int SetDepth(params object?[] _) => default;
    public static int SetDialogItemText(params object?[] _) => default;
    public static int SetEntries(params object?[] _) => default;
    public static int SetFPos(params object?[] _) => default;
    public static int SetMenuBar(params object?[] _) => default;
    public static int SetOrigin(params object?[] _) => default;
    public static int SetPenState(params object?[] _) => default;
    public static int SetStdCProcs(params object?[] _) => default;
    public static int SetStdProcs(params object?[] _) => default;
    public static int ShowHide(params object?[] _) => default;
    public static int SizeWindow(params object?[] _) => default;
    public static int SndPauseFilePlay(params object?[] _) => default;
    public static int SndSoundManagerVersion(params object?[] _) => default;
    public static int SndStopFilePlay(params object?[] _) => default;
    public static int SpeakString(params object?[] _) => default;
    public static int SpeechBusy(params object?[] _) => default;
    public static int StackSpace(params object?[] _) => default;
    public static int StandardGetFile(params object?[] _) => default;
    // Absorbs the register's un-ported arg-less StringToNum() placeholder calls
    // (decompile-lost args). The game binds the real StringToNum(string) in UtilTraps.
    public static int StringToNum(params object?[] _) => default;
    public static int SysEnvirons(params object?[] _) => default;
    public static int SystemClick(params object?[] _) => default;
    public static int TEInit(params object?[] _) => default;
    public static int UnionRgn(params object?[] _) => default;
    public static int WriteResource(params object?[] _) => default;
    public static double atan(double x) => Math.Atan(x);
    public static double cos(double x) => Math.Cos(x);
    public static double sin(double x) => Math.Sin(x);
    public static double sqrt(double x) => Math.Sqrt(x);
    public static double tan(double x) => Math.Tan(x);
}
