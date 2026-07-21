using System;

namespace OpenEV.Override.Ports.Mission.Model;

// Spaceport mission-AVAILABILITY table (the 'mïsn' availability subset) — 512 records,
// formerly 0x14 bytes each behind PTR slot 0x10080bfc (`PTR_DAT_10080bfc`, a PEF-relocated
// BSS pointer). Filled by LoadBarPersonResources from the 'mïsn' resources; read by the bar
// eligibility/greeting code (IsBarPersEligible, Dialog spaceport, Mission spawn).
// Records are typed managed now.
public static class MissionAvailTable
{
    public const int Count = 512;
    public const int Stride = 0x14;   // old record stride (ReadShortAtByteOffset)

    public static readonly MissionAvailRecord[] Store = CreateStore();
    private static MissionAvailRecord[] CreateStore()
    {
        var s = new MissionAvailRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new MissionAvailRecord();
        return s;
    }

    // The 'ëbug' bit-0xf "everyone shows up" availability override (was the byte behind
    // ptr cell 0x10081154, `PTR_DAT_10081154`). Set by LoadBarPersonResources from
    // BugBit.MissionAvailOverride; a nonzero value forces IsBarPersEligible's odds-roll
    // criterion to pass unconditionally.
    public static byte AvailOverride;

    // FAITHFUL ODD-STRIDE READ (kept bug): RunSpaceportDialog (FUN_10009eac,
    // decompile L5383) walks this table at stride 0x12 — `*(ushort *)(base +
    // idx*0x12 + 0x10)` with `undefined *` byte arithmetic — so for idx > 0 the
    // read lands INSIDE a different record of the 0x14-stride layout. Emulate the
    // byte-exact read over the typed Store (all offsets are even and in-table for
    // idx 0..511, so every read maps to exactly one short field).
    public static short ReadShortAtByteOffset(int byteOffset)
    {
        var rec = Store[byteOffset / Stride];
        switch (byteOffset % Stride)
        {
            case 0x00: return rec.LocationSelector;
            case 0x02: return rec.RequireBit;
            case 0x04: return rec.ForbidBit;
            case 0x06: return rec.AvailLocation;
            case 0x08: return rec.RecordRequirement;
            case 0x0a: return rec.ScoreRequirement;
            case 0x0c: return rec.AppearOdds;
            case 0x0e: return rec.CargoSpaceRequired;
            case 0x10: return rec.AvailShipType;   // ushort read (the & 0x1000 consumer at FUN_10009eac L5383/5393)
            case 0x12: return rec.Flags;
            default:
                throw new InvalidOperationException(
                    $"MissionAvailTable.ReadShortAtByteOffset: odd offset 0x{byteOffset:x}");
        }
    }
}

// One mission-availability record (offsets = the old 0x14-byte layout; 'mïsn'
// resource offsets noted). All shorts.
public sealed class MissionAvailRecord
{
    public short LocationSelector;   // res+0x00 — location selector (-32000 = empty sentinel; reset 0x8300)
    public short RequireBit;   // res+0x02 — ControlBits gate (< 512 bit index, 1000..1511 alias; -1 = always)
    public short ForbidBit;   // res+0x46 — ControlBits NOT-gate (-1 = always)
    public short AvailLocation;   // res+0x04 — clamped 0..2 by the loader (-1 reset; 2 = gate-only)
    public short RecordRequirement;   // res+0x06 — legal-record gate vs GalaxyMapGlobals.SystemStatus (0 ignored; -32000/-32001 = trading-enabled sentinels)
    public short ScoreRequirement;   // res+0x08 — player-score requirement
    public short AppearOdds;   // res+0x0a — appearance odds vs RandomOddsTable (reset 0)
    public short CargoSpaceRequired;   // res+0x12 — cargo-space requirement (ResolveSignedRollShort'd in place)
    public short AvailShipType;   // res+0x5a when handle >= 112, else -1 — ship-class/govt selector (reset -1)
    public short Flags;   // res+0x50 — flags word (& 0x2000 AI gate; & 0x1000 read by the odd-stride consumer)
}
