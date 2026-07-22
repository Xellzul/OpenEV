using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Systems.Model;

// The 'gövt' government-definition table — 128 records, formerly 0x36 bytes
// each in the heap behind PTR slot 0x1008a520 (toc+0x1ec0, alloc 0x1b00).
// LoadGovtResources fills it from 'gövt' 0x80.. and it is indexed by government
// id; the galaxy map reads the name through the same slot. Records are typed
// managed now; the slot's heap range is retired (OriginalGameStateTotalBytes).
public static class GovtTable
{
    public const int Count = 128;   // resource IDs 128..255 ('gövt' 0x80..)

    public static readonly GovtRecord[] Store = CreateStore();
    private static GovtRecord[] CreateStore()
    {
        var s = new GovtRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new GovtRecord();
        return s;
    }

    // Plug-in data can aim the LinkSyst / syst-sentinel govt bands past the table (E3
    // "The Frozen Heart" flët 130/132/140 carry LinkSyst 19968 → index 4968). The original
    // does the `base + index*0x36` read unchecked and gets adjacent heap garbage — no crash,
    // effectively a near-never relation match. That garbage is unpreservable in a managed
    // array, so out-of-range indexes resolve to this never-matching stand-in instead
    // (Ally/Enemy = -1 can equal no real govt index at the guarded call sites).
    public static GovtRecord AtOrPastTable(int index) =>
        (uint)index < (uint)Store.Length ? Store[index] : PastTableGarbage;
    private static readonly GovtRecord PastTableGarbage = new() { Ally = -1, Enemy = -1 };
}

// One government definition (offsets = the old record layout; 'gövt' resource
// offsets noted).
public sealed class GovtRecord
{
    // +0x00  the resource name (was a Pascal buffer, 0x1f max).
    public string Name = "";

    // +0x20  flags word (res+0x2), gating the AI ticks — now the GovtFlags enum
    // (backing type ushort, identical bit values). The member NAMES are the 'gövt'
    // Bible schema; several diverge from the decompile-observed behavior noted here
    // (the decompile is ground truth, so the behavior — not the name — is authoritative):
    // 0x01 Xenophobic = hostile-scan mode, 0x02 LawEnforcementEverywhere = legal-status
    // double-penalty gate, 0x04 AlwaysAttacksPlayer = dockable (call for defenders +
    // engage player), 0x10 RetreatAt25PctShield = surrender/flee allowed, 0x40
    // NeverAttacksPlayer = scannable (drop target & idle), 0x100 PersNoEscapePod =
    // persons leave no escape pod (ships also flee on low shield once provoked),
    // 0x1000 WarshipsPlunder = guards its parent, 0x8000 HighBribeDemands = "proud"
    // (tribute ×1.5).
    public GovtFlags Flags;

    // +0x22  short (res+0x4, − 0x80 normalized; −1 = none) — allied government index
    // (ShipAi.ShouldTurnOnPlayer's mutual-ally legal-status check; also the LinkSyst
    // 15000.. band).
    public short Ally;

    // +0x24  short (res+0x6, − 0x80 normalized; −1 = none) — enemy government index
    // (ShipAi.ShouldTurnOnPlayer's mutual-enemy hostile check; also the LinkSyst
    // 25000.. band).
    public short Enemy;

    // +0x26  short (res+0x8) — legal-status threshold the player's per-system
    // record is compared against before this govt's ships turn hostile.
    public short CrimeTolerance;

    // +0x28..+0x30 (res 0xa..0x12): the five legal-record values, indexed by event "column".
    // [1/2/3] are the standing the player loses for disabling/boarding/destroying one of this
    // govt's ships — PropagateSystemKillImpact floods these outward (see PenaltyForColumn).
    // [0] gates contraband scanning and [4] is read directly by the retaliation gate; those two
    // are never flooded.
    // (Authoritative gövt schema — Override Bible / RawGovtRecord — names the five columns
    // SmugPenalty / DisabPenalty / BoardPenalty / KillPenalty / ShootPenalty.)
    public short ScanPenalty;       // +0x28 (col 0) — nonzero ⇒ this govt scans for contraband (CheckContrabandScan)
    public short DisablePenalty;    // +0x2a (col 1) — record loss for DISABLING one of its ships
    public short BoardPenalty;      // +0x2c (col 2) — record loss for BOARDING one of its ships
    public short DestroyPenalty;    // +0x2e (col 3) — record loss for DESTROYING one of its ships
    // +0x30 (col 4) ShootPenalty. Bible: "evilness from shooting one of this govt's ships
    // (currently ignored)" — and indeed it is NEVER flooded as a legal penalty, so the documented
    // effect does nothing. Its ONLY consumer is the ApplyShipDamage retaliation gate: <1 ⇒ this
    // govt's ships usually don't fight back when the player shoots them.
    public short ShootPenalty;

    // +0x32  short (res+0x14) — initial per-system legal-status seed
    // (InitGameWorldState copies it into the kill table for the govt's systems).
    public short InitialRecord;

    // +0x34  short (res+0) — the raw first resource short.
    public short InherentJamming;

    // The kill-impact flood (PropagateSystemKillImpact) reads the SAME event column off many
    // govts, so it still needs index access. Maps the decompile's `govt + column*2 + 0x28`:
    // 1=disable, 2=board, 3=destroy (the only columns flooded; 0/4 handled directly).
    public short PenaltyForColumn(int column) => column switch
    {
        0 => ScanPenalty,
        1 => DisablePenalty,
        2 => BoardPenalty,
        3 => DestroyPenalty,
        4 => ShootPenalty,
        _ => 0,
    };
}
