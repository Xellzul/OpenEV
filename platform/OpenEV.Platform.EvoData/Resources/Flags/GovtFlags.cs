namespace OpenEV.Platform.EvoData.Resources.Flags;

// Member names are the authoritative 'gövt' schema (Override Bible / RawGovtRecord).
// Where the decompile's OBSERVED AI behavior differs from the Bible name, the
// decompile effect is glossed inline — the decompile is ground truth, the name is not,
// so do NOT "fix" code to match a name (rename is gated on a full per-bit use-site audit).
// Backs GovtRecord.Flags (GovtTable.cs); the naming-vs-decompile list lives in memory game/INDEX.md.
[System.Flags]
public enum GovtFlags : ushort
{
    None = 0,
    Xenophobic = 0x0001,               // decompile: gates "hostile-scan" AI mode (scans/targets the player)
    LawEnforcementEverywhere = 0x0002, // decompile: legal-status double-penalty gate
    AlwaysAttacksPlayer = 0x0004,      // decompile: dockable pers — calls for defenders & engages the player
    RetreatAt25PctShield = 0x0010,     // decompile: surrender/flee (DefendRetreat) allowed
    IgnoreInGoodSamaritan = 0x0020,
    NeverAttacksPlayer = 0x0040,       // decompile: scannable — drops its target & idles
    PersNoEscapePod = 0x0100,          // decompile: persons leave no escape pod (ships also flee on low shield once provoked)
    WarshipsTakeBribes = 0x0200,
    NoHail = 0x0400,
    StartDisabledOrDerelict = 0x0800,
    WarshipsPlunder = 0x1000,          // decompile: guards its parent (enters Plunder state)
    FreightersTakeBribes = 0x2000,
    PlanetsTakeBribes = 0x4000,
    HighBribeDemands = 0x8000          // decompile: "proud" govt — tribute/bribe ×1.5
}
