using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenEV.Platform.EvoData.Resources;
using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.EvoData;

// Loads the seven canonical EVO resource files from a game directory. Reads the
// resource fork via the unpacked '.rsrc/' sidecar (the Mac fork) first, falling
// back to the file's data fork. Plug-in merging (EVO Plug-Ins) is M7 work.
public static class OverrideDataLoader
{
    /// Where ExtractSfnts writes fonts pulled out of the game data. The host sets
    /// this to EvoPaths.Fonts at boot; this project cannot reference Platform.Toolbox
    /// directly. Null → a "Fonts" folder beside the executable.
    public static string? SfntExtractDir;

    // The application's own fork — open from launch, so it sits at the BOTTOM of the
    // Mac resource search chain (searched last = lowest precedence).
    private const string ApplicationFork = "EV Override";

    // Fallback data-file open order, used only if the app fork's STR# 130 can't be read.
    // Matches the canonical EVO 1.0.2 STR# 130 (idx 1..6).
    private static readonly string[] FallbackOpenOrder =
    {
        "Override Graphics", "Override Sounds", "Override Data 1",
        "Override Data 2",   "Override Titles", "Override Music",
    };

    // Loads the EVO resource forks the way the original boot does, so resource
    // precedence is correct without a hand-maintained order. The Mac Resource Manager
    // searches the most-recently-OPENED fork first, so a (type, id) in several forks
    // resolves to the LAST-opened one; MergeResources is last-write-wins, so loading in
    // OPEN order reproduces that. The open order is NOT hardcoded — it follows the boot:
    //   1. the application fork is open from launch ⇒ load FIRST (base of the chain);
    //   2. OpenPluginResourceFiles (FUN_10015b4c) then opens the six data files by name,
    //      reading the names from STR# 130 via GetIndString(idx 1..6) — we read the SAME
    //      STR# 130 here, so the precedence is driven by the game's data, not a constant;
    //   3. plug-ins are opened last ⇒ highest precedence (merged last, below).
    // Load-bearing: 'ppat' 128 exists in BOTH the app fork (a 64×64 white/gray pattern)
    // and "Override Graphics" (the green 32×32 radar-jam pattern matching 129..137).
    // Opened later, Graphics wins, so the jammed radar renders GREEN as in the original;
    // the old "app fork last" order let its white/gray pattern shadow it → white static.
    public static OverrideGameData Load(string sourceDir, Action<string>? log = null)
        => LoadCore(sourceDir, log ?? NoLog, null);

    /// <summary>
    /// Same load as <see cref="Load"/>, but also records per-(type,id) PROVENANCE — which files
    /// defined each resource, in load order, and which one won. Reads each fork exactly once. Used
    /// by the editor to show the override chain; the winner payload is reference-equal to what
    /// reaches the game, so the editor can't drift from the loader's precedence.
    /// </summary>
    public static OverrideProvenanceData LoadWithProvenance(string sourceDir, Action<string>? log = null)
    {
        var prov = new ProvenanceRecorder();
        OverrideGameData data = LoadCore(sourceDir, log ?? NoLog, prov);
        return new OverrideProvenanceData(data, prov.Build(), prov.FileOrder);
    }

    private static void NoLog(string _) { }

    // The shared body. `prov == null` ⇒ Load()'s original behaviour, byte-identical and overhead-free.
    private static OverrideGameData LoadCore(string sourceDir, Action<string> log, ProvenanceRecorder? prov)
    {
        var data = new OverrideGameData();

        // 1. Application fork (chain base / lowest precedence).
        byte[]? appFork = LoadAndMerge(data, sourceDir, ApplicationFork, log, OverrideLayerKind.Application, prov);

        // 2. The six data files, in the boot's open order (read from STR# 130, as
        //    OpenPluginResourceFiles does), each merged on top ⇒ correct precedence. A non-null
        //    return means the fork opened — record the name so the boot's OpenResFile guard can
        //    tell present forks from absent ones (missing → the game's "couldn't locate…" alert).
        foreach (string fileName in ReadResourceFileOpenOrder(appFork) ?? FallbackOpenOrder)
            if (LoadAndMerge(data, sourceDir, fileName, log, OverrideLayerKind.DataFile, prov) is not null)
                data.OpenedDataFiles.Add(fileName);

        // Plug-in support — the original game scans EV Plug-Ins/ for resource forks
        // and merges them on top of the canonical files. Resources sharing the same
        // (type, id) get OVERWRITTEN by the plug-in's version. New IDs are added.
        // The 'Sample Plugin' bundled with EVO 1.0.2 is loaded here.
        string pluginsDir = Path.Combine(sourceDir, "EV Plug-Ins");
        string pluginsRsrcDir = Path.Combine(pluginsDir, ".rsrc");
        if (Directory.Exists(pluginsRsrcDir))
        {
            // Alphabetical order for deterministic plug-in priority (later loads
            // win when (type, id) collides). The original game uses the Mac
            // Finder's sort which is also alphabetical for our cases.
            var pluginFiles = new List<string>(Directory.GetFiles(pluginsRsrcDir));
            pluginFiles.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string file in pluginFiles)
            {
                if (file.EndsWith("Icon\r") || file.EndsWith("Icon%0D")) continue;
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    if (bytes.Length < 16) continue;

                    // Probe a 'vers' resource for the plug-in's compatibility
                    // string. The original game checks the major version against
                    // a hard-coded "1.0.2"; we just log and accept any vers.
                    string versInfo = TryReadVersResource(bytes);
                    string label = Path.GetFileName(file);
                    if (versInfo.Length > 0)
                        log($"Plug-in '{label}' ({bytes.Length:N0} bytes) — {versInfo}");
                    else
                        log($"Plug-in '{label}' ({bytes.Length:N0} bytes)...");
                    prov?.BeginFile(label, OverrideLayerKind.Plugin);
                    MergeResources(data, bytes, log, label, prov);
                }
                catch (Exception ex) { log($"  ! plug-in {file}: {ex.Message}"); }
            }
        }

        log($"Loaded {data.Picts.Count} PICTs, {data.Spins.Count} spins, {data.Dlogs.Count} DLOGs, {data.Snds.Count} snds; raw resources={data.RawByOsType.Count}.");

        // Extract the embedded Geneva TrueType (sfnt) once so renderer libs can
        // load it directly. The original game uses Geneva 9pt for in-game text.
        ExtractSfnts(data, log);

        return data;
    }

    // Records, as the loader merges forks in OPEN order, which files defined each (type,id) and
    // in what order. Threaded as an optional param so the null path stays byte-identical.
    private sealed class ProvenanceRecorder
    {
        private readonly Dictionary<(uint, int), List<OverrideLayer>> _chains = new();
        private readonly List<string> _fileOrder = new();
        private int _loadOrder = -1;
        private string _fileName = "";
        private OverrideLayerKind _kind;

        public IReadOnlyList<string> FileOrder => _fileOrder;

        public void BeginFile(string fileName, OverrideLayerKind kind)
        {
            _loadOrder++;
            _fileName = fileName;
            _kind = kind;
            _fileOrder.Add(fileName);
        }

        public void Record(uint rawType, int id, byte[] payload, string? name)
        {
            var key = (rawType, id);
            if (!_chains.TryGetValue(key, out var list))
                _chains[key] = list = new List<OverrideLayer>(1);
            list.Add(new OverrideLayer(_loadOrder, _fileName, _kind, rawType, id, payload, name));
        }

        public IReadOnlyDictionary<(uint, int), OverrideChain> Build()
        {
            var result = new Dictionary<(uint, int), OverrideChain>(_chains.Count);
            foreach (var kv in _chains)
                result[kv.Key] = new OverrideChain(kv.Value);
            return result;
        }
    }

    private static void ExtractSfnts(OverrideGameData data, Action<string> log)
    {
        if (data.Sfnts.Count == 0) return;
        try
        {
            // This project cannot see Platform.Toolbox (where EvoPaths lives), so the
            // host pushes the resolved portable path in at boot — same seam idiom as
            // MacToolbox.HfsDataForkBaseDir. Null only in unit tests that never extract.
            string fontDir = SfntExtractDir
                ?? Path.Combine(AppContext.BaseDirectory, "Fonts");
            Directory.CreateDirectory(fontDir);
            foreach (var (id, bytes) in data.Sfnts)
            {
                // sfnt entries don't carry resource names in EVO; the only sfnt
                // shipped is Geneva (the system font Inside Macintosh references
                // for in-game text). Use "Geneva" by default + id disambiguator.
                string name = data.Names.GetValueOrDefault(("sfnt", id), "Geneva");
                string safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
                string outPath = Path.Combine(fontDir, $"{safe}_{id}.ttf");
                if (!File.Exists(outPath)) File.WriteAllBytes(outPath, bytes);
                log($"Extracted sfnt {id} '{name}' → {outPath}");
            }
        }
        catch (Exception ex) { log($"  ! sfnt extract: {ex.Message}"); }
    }

    // Provenance-free entry for sibling loaders (ClassicDataLoader) — same merge, no recorder.
    internal static void MergeResources(OverrideGameData data, byte[] fork, Action<string> log, string sourceLabel)
        => MergeResources(data, fork, log, sourceLabel, null);

    private static void MergeResources(OverrideGameData data, byte[] fork, Action<string> log, string sourceLabel,
        ProvenanceRecorder? prov)
    {
        foreach (var entry in MacResourceFork.Read(fork))
        {
            try
            {
                if (entry.Name is not null)
                    data.Names[(entry.EvoType(), entry.Id)] = entry.Name;
                if (entry.RawType != 0)
                {
                    data.RawByOsType[(entry.RawType, entry.Id)] = entry.Data;
                    if (entry.Name is not null)
                        data.NameByOsType[(entry.RawType, entry.Id)] = entry.Name;
                    // Same array we just stored as the winner ⇒ chain.Winner.Payload is what reaches the game.
                    prov?.Record(entry.RawType, entry.Id, entry.Data, entry.Name);
                }
                Dispatch(data, entry);
            }
            catch (Exception ex)
            {
                log($"  ! {sourceLabel} {entry.EvoType()} {entry.Id}: {ex.Message}");
            }
        }
    }

    // Load one resource fork by name and merge it on top of `data` (last-write-wins).
    // Returns the raw fork bytes (or null if absent) so the caller can re-read it — the
    // app fork's STR# 130 drives the data-file open order.
    private static byte[]? LoadAndMerge(OverrideGameData data, string sourceDir, string fileName, Action<string> log,
        OverrideLayerKind kind, ProvenanceRecorder? prov)
    {
        byte[]? fork = LoadResourceFork(sourceDir, fileName);
        if (fork is null) { log($"Skipping '{fileName}' (no resource fork found)."); return null; }
        log($"Reading '{fileName}' ({fork.Length:N0} bytes)...");
        prov?.BeginFile(fileName, kind);
        MergeResources(data, fork, log, fileName, prov);
        return fork;
    }

    // The boot (OpenPluginResourceFiles / FUN_10015b4c) opens its six data files by name,
    // reading the names from STR# 130 via GetIndString(idx 1..6). We read the SAME STR#
    // 130 so the load/precedence order is the game's, not a constant. STR# layout: int16
    // count, then `count` Pascal strings. Entry 7 ("Override Prefs") is the writable prefs
    // file the boot opens separately — only 1..6 are the resource files. Returns null
    // (→ FallbackOpenOrder) if STR# 130 is absent or malformed.
    private static string[]? ReadResourceFileOpenOrder(byte[]? appFork)
    {
        if (appFork is null) return null;
        try
        {
            foreach (var e in MacResourceFork.Read(appFork))
            {
                if (e.EvoType() != "STR#" || e.Id != 130) continue;
                byte[] d = e.Data;
                if (d.Length < 2) return null;
                int count = (d[0] << 8) | d[1], o = 2;
                var names = new List<string>(count);
                for (int i = 0; i < count && o < d.Length; i++)
                {
                    int len = d[o++];
                    if (o + len > d.Length) break;
                    names.Add(System.Text.Encoding.Latin1.GetString(d, o, len));
                    o += len;
                }
                return names.Count >= 6 ? names.GetRange(0, 6).ToArray() : null;
            }
        }
        catch { /* malformed STR# 130 → fall back to the canonical order */ }
        return null;
    }

    public static byte[]? LoadResourceFork(string sourceDir, string fileName)
    {
        string sidecar = Path.Combine(sourceDir, ".rsrc", fileName);
        if (File.Exists(sidecar))
        {
            byte[] bytes = File.ReadAllBytes(sidecar);
            if (bytes.Length > 0) return bytes;
        }
        string dataFork = Path.Combine(sourceDir, fileName);
        if (File.Exists(dataFork))
        {
            byte[] bytes = File.ReadAllBytes(dataFork);
            if (bytes.Length > 0) return bytes;
        }
        return null;
    }

    // Try to extract the human-readable substring from a 'vers' resource in the
    // fork. Mac vers layout: 2 BCD bytes + 1 stage byte + 1 prerelease byte +
    // region code (2 bytes) + Pascal short version string + Pascal long string.
    // We don't fully decode — just locate the type==vers entry and dig out the
    // long version string for the log line.
    private static string TryReadVersResource(byte[] fork)
    {
        try
        {
            foreach (var entry in MacResourceFork.Read(fork))
            {
                if (entry.EvoType() != "vers" || entry.Id != 1) continue;
                var d = entry.Data;
                if (d.Length < 8) continue;
                // Skip: 2 BCD bytes + stage + prerelease + region (2 bytes) = 6 bytes
                int off = 6;
                if (off >= d.Length) continue;
                int shortLen = d[off++]; off += shortLen;
                if (off >= d.Length) continue;
                int longLen = d[off++];
                if (off + longLen > d.Length) continue;
                return System.Text.Encoding.GetEncoding("Windows-1252").GetString(d, off, longLen);
            }
        }
        catch { }
        return "";
    }

    private static void Dispatch(OverrideGameData data, ForkResource entry)
    {
        switch (entry.EvoType())
        {
            // The gameplay records (ship/syst/spob/weap/outf/govt/pers/…) are
            // NOT parsed host-side: the ported Mac loaders read the raw payload
            // bytes from RawByOsType (populated above for every resource), the
            // same way the original Resource Manager served them. Only the
            // host-bridge types keep typed views.
            case "spin": data.Spins[entry.Id] = RawSpinRecord.Load(entry.Data); break;
            case "PICT": data.Picts[entry.Id] = entry.Data; break;
            case "snd ": data.Snds[entry.Id] = entry.Data; break;
            case "DITL": data.Ditls[entry.Id] = RawDitlRecord.Load(entry.Data); break;
            case "DLOG": data.Dlogs[entry.Id] = RawDlogRecord.Load(entry.Data); break;
            case "sfnt": data.Sfnts[entry.Id] = entry.Data; break;
        }
    }
}
