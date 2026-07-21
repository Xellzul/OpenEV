namespace OpenEV.Override.Ports.Core.Model;

// MANAGED: the global "control bits" byte[512] (`&DAT_1008f3cc` in the
// decompile — a FIXED BSS array, addressed directly as base + bitIndex). EV
// calls these the mission / visibility control bits: missions set and clear
// them, and they gate the visibility of systems, fleets, outfits, pers and
// spobs. Pilot-SAVED (PilotRec.ControlBit region, record +0x1e7e).
//
// The decompile has FOUR spellings of the same byte (per-band alias bases) —
// all canonicalized to Get/Set(v - band) at the call sites:
//   * `0x1008f3cc + v` : v in [0, 511]      ("bit v")
//   * `0x1008efe4 + v` : v in [1000, 1511]  == Base + (v - 1000)
//   * `0x1008ebfc + v` : v in [2000, 2511]  == Base + (v - 2000)
//   * `0x1008e814 + v` : v in [3000, 3511]  == Base + (v - 3000)
//
// The whole BSS band was 0x1008f3cc..0x1008f5cc in the Mac binary.
public static class ControlBits
{
    public const int Count = 512;

    public static readonly byte[] Store = new byte[Count];

    public static byte Get(int bitIndex) => Store[bitIndex];
    public static bool IsSet(int bitIndex) => Get(bitIndex) != 0;
    public static void Set(int bitIndex, byte value) => Store[bitIndex] = value;
}
