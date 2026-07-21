using System;

namespace OpenEV.Override.Ports.Systems.Model;

// Typed HANDLE over one spob record. The record data lives in a managed
// SpobRecord object (SpobTable.Store[index]) — there is no raw byte backing;
// the old EvoMemory byte-dictionary this used to address was removed once
// every consumer moved to typed fields (see Misc.OriginalGameStateTotalBytes).
//
// The handle keeps its old shape so call sites didn't need to change:
//   * `Ptr` / implicit `operator int` return the synthetic record address
//     (SpobTable.Base + Index*Stride) — address arithmetic only, for
//     consumers that still identify a spob by address (e.g. SpriteNode.ObjectPtr).
//   * `Index` maps the Ptr to Store[index]; the NAMED properties read/write the
//     typed fields on that shared object. An out-of-range Ptr throws from `Rec`.
//
// Use as `Core.Model.GameData.Spobs[i].XPos` / `.FromPtr(ptr)`.
public readonly struct SpobRec
{
    public readonly int Ptr;
    public SpobRec(int ptr) { Ptr = ptr; }

    public bool IsNull => Ptr == 0;

    // Slot index relative to record[0]. (Ptr - Base) / Stride.
    public int Index => (Ptr - SpobTable.Base) / SpobTable.Stride;

    // Pass a SpobRec into any port that still takes a raw `int spobPtr` for
    // address-identity purposes (e.g. SpriteNode.ObjectPtr).
    public static implicit operator int(SpobRec s) => s.Ptr;

    // The backing typed object for this handle's record.
    private SpobRecord Rec
    {
        get
        {
            int i = Index;
            if ((uint)i >= (uint)SpobTable.Count)
                throw new NotSupportedException(
                    $"SpobRec.Ptr 0x{Ptr:x8} maps to spob index {i} (out of [0,{SpobTable.Count})) — "
                    + "likely a sub-address or stale pointer. The spob record is now a typed object; "
                    + "use a record-aligned handle and a named field.");
            return SpobTable.Store[i];
        }
    }

    // ---- named fields → typed SpobRecord ----------------------------------------
    public short XPos { get => Rec.XPos; set => Rec.XPos = value; }
    public short YPos { get => Rec.YPos; set => Rec.YPos = value; }
    public short System { get => Rec.System; set => Rec.System = value; }
    public short Govt { get => Rec.Govt; set => Rec.Govt = value; }
    public short MinCoolness { get => Rec.MinCoolness; set => Rec.MinCoolness = value; }
    public short SpriteId { get => Rec.SpriteId; set => Rec.SpriteId = value; }
    public short TechLevel { get => Rec.TechLevel; set => Rec.TechLevel = value; }
    public short[] SpecialTech => Rec.SpecialTech;
    public short CustomPicId { get => Rec.CustomPicId; set => Rec.CustomPicId = value; }
    public short CustomSoundId { get => Rec.CustomSoundId; set => Rec.CustomSoundId = value; }
    public short TributeAccrualTicks { get => Rec.TributeAccrualTicks; set => Rec.TributeAccrualTicks = value; }
    public int Flags { get => Rec.Flags; set => Rec.Flags = value; }
    public byte Spawned { get => Rec.Spawned; set => Rec.Spawned = value; }
    public byte Visible { get => Rec.Visible; set => Rec.Visible = value; }
    public byte TradingEnabled { get => Rec.TradingEnabled; set => Rec.TradingEnabled = value; }
    public string Name { get => Rec.Name; set => Rec.Name = value; }
    public short DefenseDude { get => Rec.DefenseDude; set => Rec.DefenseDude = value; }
    public short Tribute { get => Rec.Tribute; set => Rec.Tribute = value; }
    public short TributeMax { get => Rec.TributeMax; set => Rec.TributeMax = value; }
}
