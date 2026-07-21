namespace OpenEV.Platform.Toolbox;

// Real implementations of the Mac fixed-point math traps. The ported
// transcriptions call these and CONSUME the result (HSL palette remap,
// sound-channel pitch ratios, scroll ratios), so the no-op `=> default`
// stubs in MacToolbox.UnwiredStubs.cs silently returned 0 and produced wrong
// numbers. These strongly-typed (int,int)->int overloads bind ahead of the
// params-object catch-all and give faithful Mac `Fixed` (16.16) behaviour.
public static partial class MacToolbox
{
    /// Mac FixMul(Fixed a, Fixed b): 16.16 multiply with round-to-nearest,
    /// matching the Toolbox/PPC implementation ((a*b + 0x8000) >> 16).
    public static int FixMul(int a, int b)
        => (int)(((long)a * b + 0x8000) >> 16);

    /// Mac FixDiv(Fixed a, Fixed b): (a << 16) / b. On divide-by-zero the
    /// Toolbox returns the saturated value 0x7FFFFFFF (sign of numerator).
    public static int FixDiv(int a, int b)
    {
        if (b == 0) return a >= 0 ? 0x7FFFFFFF : unchecked((int)0x80000000);
        return (int)(((long)a << 16) / b);
    }

    /// Mac FixRatio(short numer, short denom): (numer << 16) / denom. The
    /// transcriptions widen the args to int; same saturation rule as FixDiv.
    public static int FixRatio(int numer, int denom)
    {
        if (denom == 0) return numer >= 0 ? 0x7FFFFFFF : unchecked((int)0x80000000);
        return (int)(((long)numer << 16) / denom);
    }

    /// Mac Long2Fix(long x): widen an integer to Fixed by shifting into the
    /// integer half (x << 16), truncated to 32 bits.
    public static int Long2Fix(int x) => x << 16;

    /// Mac Fix2Long(Fixed x): round a Fixed to the nearest integer.
    public static int Fix2Long(int x) => (x + 0x8000) >> 16;

    /// Mac BitTst(bytePtr, bitNum): the Toolbox numbers bits from the HIGH bit
    /// of byte 0, so for a 32-bit big-endian value V, BitTst(V, n) tests bit
    /// (31 - n) of V. The ported transcriptions pass the Gestalt result VALUE
    /// directly (DetectSpeechSupport: "scalar, not an address"), e.g.
    /// BitTst(response, 31) -> bit 0 = gestaltSpeechMgrPresent. Returns 0/1.
    public static int BitTst(int value, int bitNum)
    {
        if ((uint)bitNum > 31) return 0;
        return (value >> (31 - bitNum)) & 1;
    }
}
