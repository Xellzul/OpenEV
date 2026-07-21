using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Outfit.Model;

// Typed managed C# object for ONE outfit record (0x58 bytes in the original
// EvoMemory byte-dictionary, before that representation was retired). One
// instance per slot, held in OutfitTable.Store[128]; the OutfitRec handle
// reads/writes these fields directly now that every consumer has moved off
// raw EvoMemory access (see Misc/OriginalGameStateTotalBytes).
//
// Fields are 1:1 with the OutfitRec properties; every offset in the 0x58-byte
// record is mapped (no unpromoted Field0xNN placeholders remain).
public sealed class OutfitRecord
{
    public const int ModBankCount = 2;
    // +0x00 / +0x02
    public short TechLevel;
    public short Mass;

    // +0x04 / +0x06 — modifier type per bank (0..1). ModType 13 = density scanner, 4 = shield, 9 = maneuver, etc.
    public OutfitModType[] ModType = new OutfitModType[ModBankCount];

    // +0x08 / +0x0a — modifier value per bank
    public short[] ModValue = new short[ModBankCount];

    // +0x0c max count
    public short MaximumCount;
    // +0x0e flags (see OutfFlags for the bit meanings).
    public OutfFlags Flags;
    // +0x10 AvailabilityBit: gates ControlBits (see BuildAvailableOutfitList).
    public short AvailabilityBit;

    // +0x12 int (cost — 4 bytes)
    public int Cost;

    // +0x16 resource name (was a 64-byte Pascal buffer; now a managed C# string).
    public string Name = "";

    // +0x56 persistent-outfit flag (survives ship loss). (+0x57 is unused tail padding —
    // never read or written by the original game — so it is not modelled here.)
    public byte PersistentFlagSet;
}
