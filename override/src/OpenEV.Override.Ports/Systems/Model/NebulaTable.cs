namespace OpenEV.Override.Ports.Systems.Model;

// One background-nebula / scenery sprite row as a typed managed object.
//
// The Mac global at 0x1008a50c (`_DAT_1008a50c`) holds a POINTER to a
// heap-allocated array of 26 records, 0x14 bytes each, allocated at boot
// (OriginalGameStateTotalBytes, toc+0x1eac). The records live in Store[] now;
// row ADDRESSES (Base + i*Stride) are still used as identities (each render
// node's ObjectPtr points at its row) and resolve back through At().
//
// ReseedBackgroundNebulae seeds all 26 rows; TickBackgroundNebulaSprite (the
// node update UPP) wraps them around the play area per frame.
public sealed class NebulaRecord
{
    public float X;       // +0x0 world X (seeded then wrap-clamped vs the camera)
    public float Y;       // +0x4 world Y
    public short Kind;    // +0x8 graphic-set (rand 0..1 → DockingDebrisFrameTables.DebrisPair)
    public short Angle;   // +0xa frame/rotation (rand 0..359)
    public double Depth;  // +0xc parallax-z (scaled by DepthScale)
}

public static class NebulaTable
{
    public const int PtrSlot = 0x1008a50c;   // _DAT_1008a50c: ptr to record[0]
    public const int Stride = 0x14;
    public const int Count = 26;

    public static readonly NebulaRecord[] Store = NewStore();
    private static NebulaRecord[] NewStore()
    {
        var s = new NebulaRecord[Count];
        for (int i = 0; i < Count; i++) s[i] = new NebulaRecord();
        return s;
    }

    // Heap base of record[0] — the DEREFERENCED pointer (ReadInt), matching
    // _DAT_1008a50c in the decompile. Row addresses are identities only.
    public const int Base = 0x3070_0000;   // synthetic record-base in the 0x30 FREE band — NOT 0x60, that is MacPixMap.HandleBase

    /// Resolve a row ADDRESS (node.ObjectPtr) back to the managed record.
    /// Throws on a foreign/stale address — the migration tripwire.
    public static NebulaRecord At(int rowAddr) => Store[(rowAddr - Base) / Stride];

    // PEF data-seg DOUBLE constant: the parallax/depth seeding scale used in
    // ReseedBackgroundNebulae (`DepthScale * (i2d(playWidth) - bias)`).
    public const int DepthScaleSlot = 0x10082158;
    public const double DepthScale = 0.5;   // dumped value at DepthScaleSlot
}
