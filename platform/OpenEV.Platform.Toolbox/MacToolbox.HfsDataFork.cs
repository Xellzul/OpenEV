using System;
using System.Collections.Generic;
using System.IO;

namespace OpenEV.Platform.Toolbox;

// A minimal REAL Mac HFS *data-fork* File Manager backed by a Windows directory — the
// host-substrate analogue of the Mac Preferences folder + the raw HFS file traps
// (FindFolder array-form, HOpen / HCreate / SetFPos / FSRead / FSWrite / FSClose). This is
// a DIFFERENT trap family from the resource-fork bridge in MacToolbox.FileManager.cs
// (FSMakeFSSpec / FSpOpenResFile / AddResource), which the game-prefs + pilot-save paths use.
//
// SCOPED, by overload signature, to the shareware registration/stats records: the LIVE callers
// are Pilot.LoadOrInitPilotPrefsRecord + Pilot.WritePilotRecordToPrefsFile (the 0x11c stats
// record in "EV Override Pilots", namePtr forms) and Resource.OpenOrCreatePrefsFolderFile (the
// license record in "<STR#900:1> License", string-name forms below). Other prefs callers bind
// elsewhere:
//   * Boot.InitPrefsPathAndBugBits uses the (out short, out int) FindFolder overload in
//     MacToolbox.UnwiredStubs.cs (still fnfErr) — it only stores the result, no file I/O.
//   * HGetFInfo / HSetFInfo stay no-op param stubs — the prefs path's Finder-invisibility flag
//     OR (kIsInvisible into FInfo.fdFlags) is a real, LIVE read-modify-write on one FInfo buffer
//     in the original (NOT dead code there); it's a no-op here only because these two traps
//     aren't wired to real Windows file-attribute semantics yet.
//
// Pre-substrate these traps were all stubs returning errors, so LoadOrInitPilotPrefsRecord
// returned fnfErr (-43), the registration session never opened (ShareWareGlobals.Registered
// stayed 0) and the shareware nag was unreachable. With a real folder the 0x11c record
// loads / inits / persists across launches and the session opens faithfully.
// Mac codes: noErr=0, fnfErr=-43, eofErr=-39. Seek modes: fsFromStart=1, fsFromLEOF=2,
// fsFromMark=3 (fsAtMark=0 = no move).
public static partial class MacToolbox
{
    private const short HfsNoErr  = 0;
    private const short HfsFnfErr = -43;
    private const short HfsEofErr = -39;
    private const short PrefsFolderVRefNum = 1;   // sentinel vRefNum → the prefs folder
    private const int PreferencesFolderType = 0x70726566;   // 'pref' = kPreferencesFolderType

    private static readonly object _hfsLock = new();
    private static readonly Dictionary<short, FileStream> _openDataForks = new();
    private static short _nextDataForkRef = 1000;

    // The one PEF data-seg Pascal-name pointer the prefs path passes by address
    // (Pilot.Model.PilotPrefsFile.NameStr = &DAT_10084f6e). The Toolbox can't reference
    // Ports, so the single constant is mirrored here, resolved to its faithful Mac filename.
    private const int PilotPrefsNamePtr = 0x10084f6e;

    /// Override for the data-fork base folder. Null → EvoPaths.DataRoot, which
    /// matches the host's prefsDir (V2TitleAdapter) so the shareware record lands beside the
    /// game prefs.
    public static string? HfsDataForkBaseDir;

    // The Windows folder standing in for the Mac System Preferences folder.
    private static string PrefsFolderPath()
    {
        string dir = HfsDataForkBaseDir ?? EvoPaths.DataRoot;
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Optional host hook: resolve a Mac name pointer to the ACTUAL Pascal-string filename. The
    // register port builds its prefs filename ("EV Override License") dynamically in addressed
    // memory and passes its address, so it wires this to read the Str255 back; the game leaves it
    // null and the fixed pointer-constant mapping (PilotPrefsNamePtr) applies. Read + write pass the
    // same pointer, so the round-trip is consistent either way.
    public static System.Func<int, string?>? DataForkNameResolver;

    private static string DataForkFileName(int namePtr)
    {
        string? resolved = DataForkNameResolver?.Invoke(namePtr);
        if (!string.IsNullOrEmpty(resolved)) return SanitizeFileName(resolved);
        return namePtr == PilotPrefsNamePtr ? "EV Override Pilots" : $"macfile_{(uint)namePtr:x8}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    // GetEOF: logical end-of-file (byte length) of an open data fork → eofOut[0]. The register's
    // record loader uses it to tell a fresh/empty prefs file (→ build the default record) from one
    // holding a saved 0x202 registration record (→ read it back).
    public static int GetEOF(int refNum, int[] eofOut)
    {
        lock (_hfsLock)
        {
            if (!_openDataForks.TryGetValue((short)refNum, out FileStream? fs))
            { if (eofOut is { Length: > 0 }) eofOut[0] = 0; return HfsFnfErr; }
            if (eofOut is { Length: > 0 }) eofOut[0] = (int)fs.Length;
        }
        return HfsNoErr;
    }

    private static string? ResolveDataForkPath(int vRefNum, int namePtr)
        => (short)vRefNum == PrefsFolderVRefNum
            ? Path.Combine(PrefsFolderPath(), DataForkFileName(namePtr))
            : null;

    // String-name form: the game's OpenOrCreatePrefsFolderFile passes the filename as a C# string
    // (its managed simplification of the decompile's Pascal-name address) rather than a name
    // pointer, so resolve it directly against the prefs folder.
    private static string? ResolveDataForkPath(int vRefNum, string fileName)
        => (short)vRefNum == PrefsFolderVRefNum
            ? Path.Combine(PrefsFolderPath(), SanitizeFileName(fileName))
            : null;

    // Shared open: open the resolved data-fork file read/write, assigning a fresh refNum.
    // rn is -1 (fnfErr) when the path is absent, else the opened refNum.
    private static int TryOpenDataFork(string? path, out short rn)
    {
        rn = -1;
        if (path is null || !File.Exists(path)) return HfsFnfErr;
        lock (_hfsLock)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            rn = _nextDataForkRef++;
            _openDataForks[rn] = fs;
        }
        return HfsNoErr;
    }

    // FindFolder (array form): the prefs folder type resolves to the real Windows folder.
    // refType is a `long` for headroom against any raw hex refType literal a caller might pass
    // (e.g. an unchecked 0xffff8000 constant, which C# types as `uint` and would miss an
    // `int`-first-param overload, falling through to the params stub instead) — today both prefs
    // callers (LoadOrInitPilotPrefsRecord, WritePilotRecordToPrefsFile) pass a named
    // `int OnSystemDisk = -32768` constant, which widens to `long` implicitly. refType itself is
    // ignored (only the folder type selects the folder). `create` is ignored too — the prefs
    // folder is always created; benign because every live caller passes create == 1.
    public static int FindFolder(long refType, int folderType, int create, short[] vRefNum, int dirID)
    {
        if (folderType == PreferencesFolderType)
        {
            PrefsFolderPath();   // ensure it exists (create == 1)
            if (vRefNum is { Length: > 0 }) vRefNum[0] = PrefsFolderVRefNum;
            return HfsNoErr;
        }
        if (vRefNum is { Length: > 0 }) vRefNum[0] = 0;
        return HfsFnfErr;
    }

    // HOpen (namePtr form): open an existing file read/write; fnfErr if absent.
    public static int HOpen(int vRefNum, int dirID, int namePtr, int permission, short[] refNum)
    {
        int err = TryOpenDataFork(ResolveDataForkPath(vRefNum, namePtr), out short rn);
        if (refNum is { Length: > 0 }) refNum[0] = rn;
        return err;
    }

    // HOpen (string-name form): the game's OpenOrCreatePrefsFolderFile ("<STR#900:1> License") variant.
    public static int HOpen(int vRefNum, int dirID, string fileName, int permission, ushort[] refNum)
    {
        int err = TryOpenDataFork(ResolveDataForkPath(vRefNum, fileName), out short rn);
        if (refNum is { Length: > 0 }) refNum[0] = (ushort)rn;
        return err;
    }

    // HCreate (namePtr form): create an empty file (no-op if it already exists).
    public static int HCreate(int vRefNum, int dirID, int namePtr, int creator, int type)
        => HCreateAt(ResolveDataForkPath(vRefNum, namePtr));

    // HCreate (string-name form): the game's OpenOrCreatePrefsFolderFile variant.
    public static int HCreate(int vRefNum, int dirID, string fileName, int creator, int type)
        => HCreateAt(ResolveDataForkPath(vRefNum, fileName));

    private static int HCreateAt(string? path)
    {
        if (path is null) return HfsFnfErr;
        if (!File.Exists(path)) { using FileStream _ = File.Create(path); }
        return HfsNoErr;
    }

    // SetFPos: Mac seek modes fsFromStart=1, fsFromLEOF=2, fsFromMark=3 (fsAtMark=0 = no move).
    public static int SetFPos(int refNum, int mode, int pos)
    {
        lock (_hfsLock)
        {
            if (!_openDataForks.TryGetValue((short)refNum, out FileStream? fs)) return HfsFnfErr;
            try
            {
                switch (mode)
                {
                    case 1: fs.Seek(pos, SeekOrigin.Begin); break;
                    case 2: fs.Seek(pos, SeekOrigin.End); break;
                    case 3: fs.Seek(pos, SeekOrigin.Current); break;
                }
            }
            catch (Exception) { return HfsEofErr; }
        }
        return HfsNoErr;
    }

    // FSRead: read `count` bytes at the mark; a short read returns eofErr — Mac semantics
    // the prefs-path scan loops depend on (they exit on the first non-zero ioErr). The mark
    // advances by the bytes actually read (matching ioActCount), so an exact-multiple file
    // ends positioned at EOF and the append FSWrite lands correctly.
    public static int FSRead(int refNum, int count, byte[] buffer)
    {
        lock (_hfsLock)
        {
            if (!_openDataForks.TryGetValue((short)refNum, out FileStream? fs)) return HfsFnfErr;
            int got = 0;
            while (got < count)
            {
                int n = fs.Read(buffer, got, count - got);
                if (n == 0) break;
                got += n;
            }
            return got < count ? HfsEofErr : HfsNoErr;
        }
    }

    // FSWrite: write `count` bytes at the mark.
    public static int FSWrite(int refNum, int count, byte[] buffer)
    {
        lock (_hfsLock)
        {
            if (!_openDataForks.TryGetValue((short)refNum, out FileStream? fs)) return HfsFnfErr;
            fs.Write(buffer, 0, count);
            fs.Flush();
        }
        return HfsNoErr;
    }

    // FSClose: close the file and release the refNum.
    public static int FSClose(int refNum)
    {
        lock (_hfsLock)
        {
            if (_openDataForks.TryGetValue((short)refNum, out FileStream? fs))
            {
                fs.Dispose();
                _openDataForks.Remove((short)refNum);
            }
        }
        return HfsNoErr;
    }
}
