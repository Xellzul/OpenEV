using System.Collections.Generic;
using OpenEV.Platform.EvoData.Resources;

namespace OpenEV.Platform.EvoData;

// In-memory game data store. The GAMEPLAY resources (ship/syst/spob/weap/outf/
// govt/pers/…) are NOT parsed here any more — the ported Mac loaders
// (FUN_10015e70 and friends) read the raw payload bytes from RawByOsType
// through the Resource Manager bridge, exactly like the original. Only the
// HOST-side bridges keep typed views: PICT/snd blobs for the texture/sound
// caches, spïn records for the sprite cache, DLOG/DITL for the Dialog Manager,
// and the extracted sfnt fonts.
public sealed class OverrideGameData
{
    public Dictionary<int, RawSpinRecord> Spins { get; } = new();
    public Dictionary<int, byte[]> Picts { get; } = new();
    public Dictionary<int, byte[]> Snds { get; } = new();
    public Dictionary<int, RawDitlRecord> Ditls { get; } = new();
    public Dictionary<int, RawDlogRecord> Dlogs { get; } = new();
    public Dictionary<int, byte[]> Sfnts { get; } = new();
    public Dictionary<(string Type, int Id), string> Names { get; } = new();

    // The STR# 130 data-file names (idx 1..6) whose resource fork was actually found and opened
    // during load. The boot's OpenPluginResourceFiles (FUN_10015b4c) checks these via OpenResFile:
    // a name absent here means that fork couldn't be opened, which fires the game's
    // "couldn't locate its <graphics/sounds/titles/data> file" alert + ExitToShell.
    public HashSet<string> OpenedDataFiles { get; } = new();

    // Raw resource payloads keyed by (original Mac OSType big-endian uint, id).
    // The ported game code's Mac Resource Manager (GetResource/Get1Resource)
    // reads these raw bytes at specific offsets — e.g. the universe loader
    // FUN_10015e70 walks 'shïp'/'spöb'/'sÿst'/'wëap'/'oütf' resources. Populated
    // for every enumerated resource so any gameplay type resolves.
    public Dictionary<(uint RawType, int Id), byte[]> RawByOsType { get; } = new();

    // Resource names keyed by (original Mac OSType, id). The Mac GetResInfo trap
    // returns this; the universe loader copies it into the syst/spob/ship/outf
    // name fields (e.g. system names shown in the HUD).
    public Dictionary<(uint RawType, int Id), string> NameByOsType { get; } = new();

    // Resource-Manager index over RawByOsType, built lazily on first use and cached —
    // CountResources/GetIndResourceId back the CountResources/GetIndResource Mac traps,
    // and a full syst/spob/cargo load can ask for hundreds of ids, so this is built ONCE,
    // not re-scanned per call.
    private Dictionary<uint, int>? _countsByType;
    private Dictionary<uint, List<int>>? _idsByType;

    private void EnsureResourceIndex()
    {
        if (_countsByType is not null) return;
        _countsByType = new Dictionary<uint, int>();
        _idsByType = new Dictionary<uint, List<int>>();
        foreach (var k in RawByOsType.Keys)
        {
            _countsByType[k.RawType] = (_countsByType.TryGetValue(k.RawType, out var c) ? c : 0) + 1;
            if (!_idsByType.TryGetValue(k.RawType, out var lst)) _idsByType[k.RawType] = lst = new List<int>();
            lst.Add(k.Id);
        }
        foreach (var lst in _idsByType.Values) lst.Sort();
    }

    // CountResources (Mac trap) — number of resources of a type. The universe loader's
    // syst/spob loops early-exit once they've loaded this many; a stub 0 stopped them
    // after the first.
    public int CountResources(uint type)
    {
        EnsureResourceIndex();
        return _countsByType!.TryGetValue(type, out var c) ? c : 0;
    }

    // GetIndResource (Mac trap) — the index-th (1-based) resource id of a type, in
    // ascending-id order (LoadCargoResources walks 'dëqt' this way; a stub 0 left the
    // cargo/commodity tables EMPTY).
    public int? GetIndResourceId(uint type, int index)
    {
        EnsureResourceIndex();
        return _idsByType!.TryGetValue(type, out var lst) && index >= 1 && index <= lst.Count ? lst[index - 1] : null;
    }

    // Join a DLOG record with its DITL item list — what GetNewDialog needs to build a
    // dialog template. False if either resource is missing (unknown dlogId, or its
    // ItemsId doesn't resolve to a parsed DITL).
    public bool TryGetDialogAndItems(int dlogId, out RawDlogRecord dlog, out IReadOnlyList<RawDitlItem> items)
    {
        if (Dlogs.TryGetValue(dlogId, out var d) && Ditls.TryGetValue(d.ItemsId, out var dt))
        {
            dlog = d;
            items = dt.Items;
            return true;
        }
        dlog = default!;
        items = System.Array.Empty<RawDitlItem>();
        return false;
    }
}
