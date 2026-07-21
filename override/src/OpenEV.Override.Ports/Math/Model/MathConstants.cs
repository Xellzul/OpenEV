namespace OpenEV.Override.Ports.EvoMath.Model;

// Managed (real C# literal) values for the PEF data-segment math constants —
// kept separate from the scalar registry in Ports/Math/MathConstants.cs (also
// fully migrated to managed literals). Both used to be read through the
// EvoMemory raw byte-heap, which was retired once every reader moved to these
// typed fields (see OriginalGameStateTotalBytes).
//
// Each value is the exact IEEE-754 double the data segment held.
public static class MathConstants
{
    // 2^52 + 2^31 (bit pattern 0x4330000080000000) — the PPC int->double magic
    // bias. PpcMagic.IntToDouble(x) builds the biased pattern; subtract this to
    // recover (double)x. (Source address 0x100822d8.)
    public const double IntToDoubleBias = 4503601774854144.0;

    public const double NegativeOne = -1.0;   // was _DAT_100822e0
    public const double Zero = 0.0;    // was _DAT_100822e8

    // FUN_10058218 (integer atan2): the quadrant-split boundary (compared against
    // x/y as the literal 0.0) and the ratio->atan-table-index scale. Both are
    // single-precision FLOATS in the data segment — the decompile reads them
    // as `(double)_DAT_...` (float widened to double), and the slots are only 4
    // bytes apart, which a `ReadDouble` would have mis-spanned. Verified by
    // decompressing the PEF data section: 0x100821c8 = 0.0f, 0x100821cc = 100.0f.
    public const float Atan2QuadrantBoundary = 0.0f;     // was _DAT_100821c8
    public const float Atan2RatioScale = 100.0f;   // was _DAT_100821cc

    // FUN_10058064 InitTrigTables: degree->radian scale (sin/cos/tan loop) and the
    // atan input/output scales (atan-table loop). These are ROUNDED literals in the
    // data segment, NOT exact pi/180 and 180/pi — reproduced bit-for-bit here.
    // Verified by decompressing the PEF data section. Source addresses 0x100821e8 /
    // 0x100821e0 / 0x100821d8 (EvMath.InitTrigTables is their only reader now).
    public const double DegToRad = 0.01745329;   // was *(GameToc-0x191e*4) @ 0x100821e8 (~ pi/180, rounded)
    public const double AtanInput = 0.01;         // was *(GameToc-0x1920*4) @ 0x100821e0
    public const double AtanOutput = 57.2957795;   // was *(GameToc-0x1922*4) @ 0x100821d8 (~ 180/pi, rounded)
}
