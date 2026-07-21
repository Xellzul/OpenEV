using System;
using System.Collections.Generic;

namespace OpenEV.Platform.Toolbox;

// Minimal *real* Mac File Manager + Resource Manager fork I/O for the game.
//
// The ported transcriptions (WritePrefsToDisk = FUN_1001a3b8, the prefs-load
// FUN_10019f88, pilot save, plugin open, …) drive disk persistence through
// the raw Toolbox traps: FSMakeFSSpec → FSpCreateResFile → FSpOpenResFile →
// UseResFile → AddResource → UpdateResFile/CloseResFile. The default stubs
// for those traps are no-ops (FSpOpenResFile → -1) so every one of those
// paths is silently inert.
//
// This bridge makes that trap sequence real — but SCOPED. A file behaves
// "real" only if its name has been registered via RegisterManagedForkFile;
// for every other filename the traps return exactly their old stub values,
// so enabling prefs persistence does NOT start half-driving the still-
// deferred pilot/plugin/alias paths (same scoping discipline as the Dialog
// Manager's dlogId==4001 guard).
//
// Actual byte I/O is delegated to the host (ForkFileReader/Writer) and the
// real Mac resource-fork (de)serializer (ForkParser/Serializer) — same
// "make the traps real, bridge the unportable bits to the host" model as
// the QuickDraw / Dialog / Sound subsystems.
public static partial class MacToolbox
{
    public sealed class ForkResource
    {
        public uint Type;
        public int Id;
        public byte[] Data = Array.Empty<byte>();
        public string? Name;
    }

    // Host-installed delegates.
    public static Func<string, byte[]?>? ForkFileReader;          // managed name → raw fork bytes (null = no file)
    public static Action<string, byte[]>? ForkFileWriter;         // managed name → write raw fork bytes
    public static Func<byte[], List<ForkResource>>? ForkParser;   // raw fork → resources
    public static Func<List<ForkResource>, byte[]>? ForkSerializer;// resources → raw fork
    public static Action<string>? ForkFileDeleter;                // managed name → delete on disk
    // StandardGetFile host picker: returns the chosen file's full path, or null
    // if the user cancelled. The bridge derives the managed name (leaf) and the
    // reply record from it.
    public static Func<string?>? PilotFilePicker;

    // Picked files may live outside the default OpenEV.Override dir; this maps a
    // registered managed name → its absolute path so ForkFileReader/Writer can
    // resolve it. Empty for the prefs file (it uses the default dir mapping).
    public static readonly Dictionary<string, string> ManagedForkPathOverride =
        new(StringComparer.Ordinal);

    /// A managed stand-in for the Mac 70-byte FSSpec stack record. The legacy
    /// spec ADDRESS lives inside the Toolbox (the sanctioned boundary — same
    /// pattern as the Str255 scratch staging); ports hold the object and the
    /// implicit int conversion feeds every existing addr-form FSp* trap.
    public sealed class FsSpec
    {
        internal int Addr { get; } = MacScratch.Alloc();
        public static implicit operator int(FsSpec s) => s.Addr;
        /// The spec's parID field — the alias-rewrite guard the pilot loaders snapshot and
        /// compare. The game never writes it (FSp* traps key off the managed _specName dict, and
        /// ResolveAliasFile never rewrites), so it was always the same unwritten value; a
        /// constant 0 keeps the snapshot==current guard holding (no rewrite), off EvoMemory.
        public int ParID => 0;
    }

    /// StandardGetFile, managed form: pop the host file picker, register the
    /// picked file as a managed fork, and return sfGood with the leaf name
    /// (sfFile.vRefNum/parID are always 0 in the game — see the reply-record form).
    public static bool StandardGetFile(int[] typeList, out string leafName)
    {
        leafName = "";
        string? path = PilotFilePicker?.Invoke();
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return false;                            // sfGood = false → Cancel
        leafName = System.IO.Path.GetFileName(path);
        RegisterManagedForkFile(leafName);
        ManagedForkPathOverride[leafName] = path;    // resolve reads/writes to the picked path
        return true;
    }

    // Registered managed filenames (the only names the traps treat as real).
    private static readonly HashSet<string> _managedFiles = new(StringComparer.Ordinal);
    public static void RegisterManagedForkFile(string name)
    {
        if (!string.IsNullOrEmpty(name)) _managedFiles.Add(name);
    }
    public static bool IsManagedForkFile(string name) => _managedFiles.Contains(name);

    // FSSpec scratch addr → managed name (set by FSMakeFSSpec, read by the
    // FSp* traps that take the spec back).
    private static readonly Dictionary<int, string> _specName = new();

    /// The FSSpec's name field (the Mac spec+6 Str63 leaf name). The game stores the
    /// picked file's name in _specName when FSMakeFSSpec builds the spec, instead
    /// of writing the raw 70-byte record — so the pilot loaders read the loaded
    /// file's name from here (was reading the never-written spec+6 → garbage).
    public static string FsSpecName(int specPtr)
        => _specName.TryGetValue(specPtr, out var nm) ? nm : "";
    // Names whose fork was just FSpCreateResFile'd → open empty (rewrite).
    private static readonly HashSet<string> _freshCreate = new(StringComparer.Ordinal);

    // "Last Pilot" pointer (port-native Finder-alias equivalent).
    // The Mac wrote a real 'alis' file named "Last Pilot" in the Pilots folder that
    // resolved to the most-recently saved/loaded pilot; the boot auto-load (FUN_1001b56c)
    // opens "Last Pilot" and the File Manager transparently redirects to the real file.
    // This host has no Mac aliases, so the pointer instead persists the target pilot's
    // LEAF NAME, and ResolveAliasFile rewrites a "Last Pilot" spec to that target — so the
    // real pilot opens and FsSpecName yields its real name (NOT "Last Pilot"). On disk the
    // pointer is just the UTF-8 target name written to <Pilots>/Last Pilot via ForkFileWriter.
    public const string LastPilotPointerName = "Last Pilot";
    private static string? _lastPilotTarget;   // same-session cache; disk is the cross-launch source

    /// Persist the "Last Pilot" pointer at the just-saved/loaded pilot's leaf name.
    /// Called from WriteAliasResourceFile (FUN_1001d4bc) on every pilot save and load.
    public static void WriteLastPilotPointer(string targetLeafName)
    {
        // Guard the self-reference: WriteAliasResourceFile passes the spec AFTER
        // ResolveAliasFile has redirected it to the real pilot, so targetLeafName is
        // already the real name — but stay defensive (an empty/"Last Pilot" target
        // would make boot resolve to itself forever).
        if (string.IsNullOrEmpty(targetLeafName) || targetLeafName == LastPilotPointerName) return;
        RegisterManagedForkFile(LastPilotPointerName);
        _lastPilotTarget = targetLeafName;
        // Pointer payload = the leaf name, plus (when the pilot lives OUTSIDE the
        // default Pilots folder — an Open-Pilot pick) its absolute path on a second
        // line, so a relaunch resolves the exact file the Mac alias record would have.
        string payload = targetLeafName;
        if (ManagedForkPathOverride.TryGetValue(targetLeafName, out var path) && !string.IsNullOrEmpty(path))
            payload += "\n" + path;
        ForkFileWriter?.Invoke(LastPilotPointerName, System.Text.Encoding.UTF8.GetBytes(payload));
        FMT($"WriteLastPilotPointer → '{targetLeafName}'");
    }

    private static string? ReadLastPilotPointerRaw()
    {
        var bytes = ForkFileReader?.Invoke(LastPilotPointerName);
        if (bytes is null || bytes.Length == 0) return null;
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// The leaf name the "Last Pilot" pointer currently targets (in-memory cache first,
    /// else the on-disk pointer file), or null when no last pilot has been recorded.
    public static string? ReadLastPilotTarget()
    {
        if (!string.IsNullOrEmpty(_lastPilotTarget)) return _lastPilotTarget;
        var raw = ReadLastPilotPointerRaw();
        if (raw is null) return null;
        return _lastPilotTarget = raw.Split('\n')[0];
    }

    /// If `specPtr` currently names the "Last Pilot" pointer, redirect it to the real
    /// target pilot (registering that target — and restoring its out-of-folder path,
    /// if the pointer recorded one — so the following FSpOpenResFile opens it). Returns
    /// true when a redirect happened (the Mac `wasAliased` out). The no-op Mac
    /// ResolveAliasFile shim calls this; non-"Last Pilot" specs are untouched.
    internal static bool TryResolveLastPilotSpec(int specPtr)
    {
        if (!_specName.TryGetValue(specPtr, out var nm) || nm != LastPilotPointerName) return false;
        var raw = ReadLastPilotPointerRaw();
        if (string.IsNullOrEmpty(raw)) return false;
        var parts = raw.Split('\n');
        string target = parts[0];
        if (string.IsNullOrEmpty(target)) return false;
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && !ManagedForkPathOverride.ContainsKey(target))
            ManagedForkPathOverride[target] = parts[1];   // restore an out-of-folder pilot across relaunch
        RegisterManagedForkFile(target);
        _specName[specPtr] = target;   // the spec now names the real pilot file
        FMT($"ResolveAliasFile '{LastPilotPointerName}' → '{target}'");
        return true;
    }

    private sealed class OpenFork
    {
        public string Name = "";
        public int RefNum;
        public List<ForkResource> Res = new();
        public bool Dirty;
    }
    private static readonly Dictionary<int, OpenFork> _openForks = new();
    private static int _nextForkRef = 0x7000;
    private static int _curResFile;   // set by UseResFile; AddResource targets it

    private static OpenFork? OpenForkForResource(uint type, int id)
    {
        foreach (var of in _openForks.Values)
            if (of.Res.Exists(r => r.Type == type && r.Id == id))
                return of;
        return null;
    }

    internal static string? TryGetManagedResourceName(uint type, int id)
    {
        var of = OpenForkForResource(type, id);
        return of?.Res.Find(r => r.Type == type && r.Id == id)?.Name;
    }

    /// True if the bridge has a managed-fork resource for (type,id); if so,
    /// `bytes` is its payload. GetResource consults this before the host.
    internal static bool TryGetManagedResource(uint type, int id, out byte[] bytes)
    {
        var of = OpenForkForResource(type, id);
        if (of is not null)
        {
            bytes = of.Res.Find(r => r.Type == type && r.Id == id)!.Data;
            return true;
        }
        bytes = Array.Empty<byte>();
        return false;
    }

    // Trap routing (called from the trap bodies in MacToolbox.cs). Each returns true
    // (and sets `result`) when it handled a managed file; false means "fall through to
    // the old stub behaviour".

    public static bool FileManagerTrace;
    private static void FMT(string m) { if (FileManagerTrace) Console.WriteLine("[FM] " + m); }

    /// Managed-name FSMakeFSSpec: the string-form callers (PilotIdentity.Name etc.) reach
    /// this DIRECTLY — no MacScratch Str255 round-trip.
    private static bool Mgr_FSMakeFSSpecByName(string raw, int specPtr, out short result)
    {
        result = 0;
        if (string.IsNullOrEmpty(raw) || !_managedFiles.Contains(raw))
        {
            // No tracked file by this name → fnfErr, matching the Mac FSMakeFSSpec contract
            // (a missing file fills the spec and returns fnfErr). The game's registry is the
            // existence source-of-truth — an unregistered name has no path to stat, so
            // "unknown name = absent" is the honest answer. Registered names fall through to the
            // real ForkFileReader check below; the unmanaged spec is deliberately NOT tracked, so
            // a follow-on FSpCreateResFile on it stays inert (no deferred create path is woken).
            result = -43;
            FMT($"FSMakeFSSpec name='{raw}' UNMANAGED/empty → fnfErr(-43)");
            return true;
        }
        if (specPtr != 0) _specName[specPtr] = raw;
        bool exists = (ForkFileReader?.Invoke(raw)) is not null;
        result = exists ? (short)0 : (short)-43;   // noErr : fnfErr
        FMT($"FSMakeFSSpec name='{raw}' spec=0x{specPtr:x} exists={exists} → {result}");
        return true;
    }

    private static bool Mgr_FSpCreateResFile(int specPtr)
    {
        if (!_specName.TryGetValue(specPtr, out var nm)) { FMT($"FSpCreateResFile spec=0x{specPtr:x} UNKNOWN → stub"); return false; }
        _freshCreate.Add(nm);   // next FSpOpenResFile opens an empty fork
        FMT($"FSpCreateResFile name='{nm}' → fresh");
        return true;
    }

    // FSpDelete: delete a managed fork file on disk. Returns the Mac OSErr the caller
    // tests: noErr(0) on a real delete, fnfErr(-43) if the managed file is absent, and
    // noErr(0) for an UNKNOWN/unmanaged spec (the old stub value) so deferred paths stay
    // inert. PilotSave relies on the file actually being gone afterwards (its next
    // FSMakeFSSpec must return fnfErr to reach the writer).
    private static short Mgr_FSpDelete(int specPtr)
    {
        if (!_specName.TryGetValue(specPtr, out var nm)) { FMT($"FSpDelete spec=0x{specPtr:x} UNKNOWN → stub(0)"); return 0; }
        bool existed = (ForkFileReader?.Invoke(nm)) is not null;
        if (!existed) { FMT($"FSpDelete name='{nm}' → fnfErr(-43)"); return -43; }
        ForkFileDeleter?.Invoke(nm);
        _freshCreate.Remove(nm);
        FMT($"FSpDelete name='{nm}' → deleted (0)");
        return 0;
    }

    private static bool Mgr_FSpOpenResFile(int specPtr, out short refNum)
    {
        refNum = -1;
        if (!_specName.TryGetValue(specPtr, out var nm)) return false;
        var of = new OpenFork { Name = nm, RefNum = _nextForkRef++ };
        if (!_freshCreate.Contains(nm))
        {
            var bytes = ForkFileReader?.Invoke(nm);
            if (bytes is not null && ForkParser is not null)
                of.Res = ForkParser(bytes);
        }
        _freshCreate.Remove(nm);
        _openForks[of.RefNum] = of;
        _curResFile = of.RefNum;
        refNum = (short)of.RefNum;
        FMT($"FSpOpenResFile name='{nm}' → refNum={refNum}, {of.Res.Count} existing res");
        return true;
    }

    private static bool Mgr_UseResFile(int refNum)
    {
        if (!_openForks.ContainsKey(refNum)) { return false; }
        _curResFile = refNum;
        return true;
    }

    /// AddResource from a managed byte[] — the migration target for writers
    /// that build the resource data in C# (no NewHandle/EvoMemory staging).
    /// Same replace-or-append semantics as the handle form.
    public static bool AddResource(byte[] data, int resType, int resId, string? name)
    {
        if (!_openForks.TryGetValue(_curResFile, out var of)) return false;
        uint type = unchecked((uint)resType);
        if (name is { Length: 0 }) name = null;
        of.Res.RemoveAll(r => r.Type == type && r.Id == resId);
        of.Res.Add(new ForkResource { Type = type, Id = resId, Data = (byte[])data.Clone(), Name = name });
        of.Dirty = true;
        FMT($"AddResource type=0x{type:x} id=0x{resId:x} size={data.Length} name='{name}' → fork '{of.Name}'");
        return true;
    }

    /// Copy a resource Handle's data block into a managed byte[] (managed
    /// handles resolve directly; raw handles read through the master pointer).
    /// The migration target for readers that parse a whole resource in C#.
    public static byte[] HandleToBytes(int handle)
    {
        var managed = ResolveResource(handle);
        if (managed != null) return (byte[])managed.Clone();
        // Every resource handle resolves managed (GetResource/NewHandleFromBytes register
        // in _resourceData); a raw NewHandle'd block's arena bytes are never read as a
        // resource here. Tripwire if a genuine raw handle ever reaches this.
        throw new System.InvalidOperationException(
            $"HandleToBytes on un-managed handle 0x{handle:x8} — resource handles should be managed");
    }

    /// Managed-name AddResource — the string-form caller (AddResource(handle,…,string))
    /// reaches this DIRECTLY, no MacScratch Str255 round-trip.
    internal static bool Mgr_AddResourceByName(int handle, int resType, int resId, string? name)
    {
        if (!_openForks.TryGetValue(_curResFile, out var of)) return false;
        var managed = ResolveResource(handle);   // managed handle (NewHandleFromBytes / GetResource)
        if (managed == null)
            // AddResource always receives a managed handle (the writers build the resource
            // bytes in C#); a raw NewHandle'd block never carries resource data here.
            throw new System.InvalidOperationException(
                $"AddResource on un-managed handle 0x{handle:x8} — resource writers should build managed bytes");
        byte[] data = (byte[])managed.Clone();
        uint type = unchecked((uint)resType);
        if (name is { Length: 0 }) name = null;
        of.Res.RemoveAll(r => r.Type == type && r.Id == resId);
        of.Res.Add(new ForkResource { Type = type, Id = resId, Data = data, Name = name });
        of.Dirty = true;
        FMT($"AddResource type=0x{type:x} id=0x{resId:x} size={data.Length} name='{name}' → fork '{of.Name}'");
        return true;
    }

    private static bool Mgr_CloseResFile(int refNum)
    {
        if (!_openForks.TryGetValue(refNum, out var of)) { FMT($"CloseResFile refNum={refNum} UNKNOWN → stub"); return false; }
        FMT($"CloseResFile name='{of.Name}' dirty={of.Dirty} res={of.Res.Count} writer={(ForkFileWriter != null)}");
        if (of.Dirty && ForkSerializer is not null && ForkFileWriter is not null)
            ForkFileWriter(of.Name, ForkSerializer(of.Res));
        _openForks.Remove(refNum);
        if (_curResFile == refNum) _curResFile = 0;
        return true;
    }

    private static bool Mgr_UpdateResFile(int refNum)
    {
        if (!_openForks.TryGetValue(refNum, out var of)) return false;
        if (of.Dirty && ForkSerializer is not null && ForkFileWriter is not null)
        {
            ForkFileWriter(of.Name, ForkSerializer(of.Res));
            of.Dirty = false;
        }
        return true;
    }

    /// Read a managed fork's resources WITHOUT driving the open/close traps —
    /// used by the focused prefs-load (PrefsMemory) which mirrors the boot
    /// loader's happy path but skips the FSSpec/error machinery.
    public static bool TryReadManagedFork(string name, out List<ForkResource> resources)
    {
        resources = new List<ForkResource>();
        if (!_managedFiles.Contains(name)) return false;
        var bytes = ForkFileReader?.Invoke(name);
        if (bytes is null || ForkParser is null) return false;
        resources = ForkParser(bytes);
        return true;
    }

    /// Find a resource (type,id) across every registered managed fork on disk,
    /// without knowing which file holds it. Used by the focused prefs-load to
    /// pull the 'Mp¨Ä' id-0x80 blob back at boot.
    public static bool TryLoadManagedResource(uint type, int id, out byte[] data)
    {
        foreach (var name in _managedFiles)
        {
            if (!TryReadManagedFork(name, out var res)) continue;
            var hit = res.Find(r => r.Type == type && r.Id == id);
            if (hit is not null) { data = hit.Data; return true; }
        }
        data = Array.Empty<byte>();
        return false;
    }
}
