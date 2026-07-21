using System;

namespace OpenEV.Override.Ports.Systems.Model;

// Typed HANDLE over one star-system record. The record data lives entirely in a
// managed SystRecord object (SystTable.Store[index]) — there is no raw byte-memory
// backing anymore (EvoMemory itself was retired once every port site migrated off
// raw offset access). Same contract as SpobRec/MissionRec:
//   * `Ptr` / implicit `operator int` return the record's synthetic address
//     (SystTable.Base + index*Stride) for callers that just thread it through.
//   * `Index` maps the Ptr to Store[index]; named properties read/write the
//     typed fields; the generic byte accessors THROW (no byte backing exists).
//
// Use as `Systems.Model.SystTable.Store[i].Visibility` / `.FromPtr(ptr)`.
public readonly struct SystRec
{
    public readonly int Ptr;
    public SystRec(int ptr) { Ptr = ptr; }

    public bool IsNull => Ptr == 0;

    // Slot index relative to record[0].
    public int Index => (Ptr - SystTable.Base) / SystTable.Stride;

    public static implicit operator int(SystRec s) => s.Ptr;

    private SystRecord Rec
    {
        get
        {
            int i = Index;
            if ((uint)i >= (uint)SystTable.Count)
                throw new NotSupportedException(
                    $"SystRec.Ptr 0x{Ptr:x8} maps to syst index {i} (out of [0,{SystTable.Count})) — "
                    + "likely a sub-address or stale pointer. The syst record is now a typed object; "
                    + "use a record-aligned handle and a named field.");
            return SystTable.Store[i];
        }
    }

    // ---- named fields → typed SystRecord ----------------------------------------
    public short XPos { get => Rec.XPos; set => Rec.XPos = value; }
    public short YPos { get => Rec.YPos; set => Rec.YPos = value; }
    public short Govt { get => Rec.Govt; set => Rec.Govt = value; }
    public short[] HyperLink => Rec.HyperLink;
    public short[] StellarLink => Rec.StellarLink;
    public short[] FleetSpawn => Rec.FleetSpawn;
    public short Visited { get => Rec.Visited; set => Rec.Visited = value; }
    public short Message { get => Rec.Message; set => Rec.Message = value; }
    public short AsteroidCount { get => Rec.AsteroidCount; set => Rec.AsteroidCount = value; }
    public short Interference { get => Rec.Interference; set => Rec.Interference = value; }
    public short Visibility { get => Rec.Visibility; set => Rec.Visibility = value; }
    public short[] ForcedPers => Rec.ForcedPers;
    public byte[] Name => Rec.Name;
    public byte ShownFlag { get => Rec.ShownFlag; set => Rec.ShownFlag = value; }

    // ---- generic byte access — REMOVED (no byte backing) ------------------------
    private static NotSupportedException NoBytes(int off) =>
        new NotSupportedException(
            $"SystRec generic byte access at +0x{off:x} is gone — the syst record is a typed "
            + "object now. Add a named field to SystRecord/SystRec for this offset and use it.");

    public byte ByteAt(int off) => throw NoBytes(off);
    public short ShortAt(int off) => throw NoBytes(off);
    public ushort UShortAt(int off) => throw NoBytes(off);
    public int IntAt(int off) => throw NoBytes(off);
    public uint UIntAt(int off) => throw NoBytes(off);
    public float FloatAt(int off) => throw NoBytes(off);

    public void SetByteAt(int off, byte v) => throw NoBytes(off);
    public void SetShortAt(int off, short v) => throw NoBytes(off);
    public void SetIntAt(int off, int v) => throw NoBytes(off);
    public void SetFloatAt(int off, float v) => throw NoBytes(off);
}
