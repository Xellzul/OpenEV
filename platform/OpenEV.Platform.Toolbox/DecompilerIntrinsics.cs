namespace OpenEV.Platform.Toolbox;

// CONCAT22(hi, lo) packs two 16-bit values into a 32-bit one — the decompiler's rendering of a
// byte-level concat PowerPC performs inline, and the shape a Mac Point (v<<16|h) is built in. The
// mechanically transcribed register panes call it under that name, so it stays a real function
// rather than being folded into each call site. Endianness follows the decompiler's convention:
// the first argument supplies the high-order bytes.
public static class DecompilerIntrinsics
{
    public static int CONCAT22(short hi, short lo)
        => ((ushort)hi << 16) | (ushort)lo;

    // Widened call sites (PrintRegistration) pass int expressions; mask to the low half so the
    // result matches the short overload exactly.
    public static int CONCAT22(int hi, int lo)
        => ((hi & 0xffff) << 16) | (lo & 0xffff);
}
