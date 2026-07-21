using System;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Outfit.Model;

// Typed HANDLE over one outfit record. The record data lives in a managed
// OutfitRecord object (OutfitTable.Store[index]) — there is no raw byte backing;
// the old EvoMemory byte-dictionary this used to address was removed once every
// consumer moved to typed fields (see Misc.OriginalGameStateTotalBytes).
//
// Same shape as ShipRec: Ptr is the synthetic record address (OutfitTable.Base +
// Index*Stride) — address arithmetic only. Named properties read/write the typed
// fields on the shared OutfitRecord.
//
// Use as `Outfit.Model.OutfitTable.Outfits[i].ModType[bank]`.
public readonly struct OutfitRec
{
    public readonly int Ptr;
    public OutfitRec(int ptr) { Ptr = ptr; }

    public bool IsNull => Ptr == 0;

    public int Index => (Ptr - OutfitTable.Base) / OutfitTable.Stride;

    public static implicit operator int(OutfitRec o) => o.Ptr;

    private OutfitRecord Rec
    {
        get
        {
            int i = Index;
            if ((uint)i >= (uint)OutfitTable.Count)
                throw new NotSupportedException(
                    $"OutfitRec.Ptr 0x{Ptr:x8} maps to outfit index {i} (out of [0,{OutfitTable.Count})) — "
                    + "likely a sub-address or stale pointer. The outfit record is now a typed object; "
                    + "use a record-aligned handle and a named field.");
            return OutfitTable.Store[i];
        }
    }

    // ---- named fields → typed OutfitRecord ------------------------------------------
    public short TechLevel { get => Rec.TechLevel; set => Rec.TechLevel = value; }
    public short Mass { get => Rec.Mass; set => Rec.Mass = value; }

    public OutfitModType[] ModType => Rec.ModType;

    public short[] ModValue => Rec.ModValue;

    public short MaximumCount { get => Rec.MaximumCount; set => Rec.MaximumCount = value; }
    public OutfFlags Flags { get => Rec.Flags; set => Rec.Flags = value; }
    public short AvailabilityBit { get => Rec.AvailabilityBit; set => Rec.AvailabilityBit = value; }
    public int Cost { get => Rec.Cost; set => Rec.Cost = value; }
    public string Name { get => Rec.Name; set => Rec.Name = value; }
    public byte PersistentFlagSet { get => Rec.PersistentFlagSet; set => Rec.PersistentFlagSet = value; }
}
