using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.EvoMath;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10072dd0 (EV Override-11.c:47426; disassembly sub_72DD0) — the game's offline registration KEY
// GENERATOR. Computes the expected 8-letter ('A'..'P') code for a registration record: fetch the hash
// seed GetIndString(seedStrListId, 1) (STR# 900/1 = "EV Override"), uppercase the owner name (the
// offset-0 Str255 of the record block), then fold the name and the seed into a 32-bit accumulator
// keyed by the copy count, emitting 4 bits per output letter. The mix tier is the shareware user mode
// (EvoGlobals.ShareWareUserMode, clamped 1..2 by InitShareWareRegistrationSession): 2 = the
// 0xdeadbeef-keyed variant, 1 = the lighter variant, 0/other = none. Returns a handle holding the
// 8-byte Pascal code (0 on failure).
//
// DEVIATION (faithful): the original returns a NewPtr(0x100) Ptr (its caller frees it with
// DisposePtr); the port returns a managed handle over the 9 written bytes instead — MacToolbox has
// no separate Ptr/Handle indirection (both are opaque int tokens over a managed byte[]), so this is
// observably identical to the EqualString consumer.
//
// Key generator only: the game's self-check (CheckShareWareRegistrationMatch) loads the record via
// LoadRegistrationRecord (FUN_100727bc) and compares this computed code against the stored one.
public static class GenerateRegistrationCode
{
    public static int Run(int nameRecordHandle, int copyCount, int seedStrListId)
    {
        byte[] seed = PascalBytes(MacToolbox.GetIndString((short)seedStrListId, 1));
        byte[] name = MacToolbox.HandleToBytes(nameRecordHandle);   // record block; owner name Str255 at offset 0
        // DEVIATION (faithful): managed guard — the ASM dereferences the block unconditionally.
        if (name.Length == 0) return 0;

        int tier = EvoGlobals.ShareWareUserMode;
        UpperPascalName(name);
        uint acc = 0;
        acc = MixRegistrationHash(acc, name, copyCount, tier);
        acc = MixRegistrationHash(acc, seed, copyCount, tier);

        const int codeLetters = 8;                  // 8-letter 'A'..'P' code
        byte[] code = new byte[codeLetters + 1];
        code[0] = codeLetters;                      // Pascal length byte
        for (short i = 1; i <= codeLetters; i++)
        {
            int nibble = (int)(acc & 0xf);
            acc = EvMath.RotateRight(acc, 4);
            code[i] = (byte)(nibble + 'A');         // low nibble 0..15 → 'A'..'P'
        }
        return MacToolbox.NewHandleFromBytes(code);
    }

    // sub_72CA8 — fold a Pascal string into the accumulator byte by byte, keyed by the copy count.
    // Tier 2 (the register's tier): XOR-fold the byte in, rotate left 6, add the key, XOR 0xdeadbeef,
    // rotate right 1. Tier 1: OR-fold, rotate left 5, add the key (no deadbeef). Tier 0/other: no-op.
    private static uint MixRegistrationHash(uint acc, byte[] pstr, int key, int tier)
    {
        int len = pstr[0];
        for (short i = 1; i <= len; i++)
        {
            if (tier == 2)
            {
                acc = (acc & 0xffffff00) ^ (((acc & 0xff) + pstr[i]) & 0xff);
                acc = EvMath.RotateLeft(acc, 6);
                acc = (acc & 0xffff0000) | (((acc & 0xffff) + (uint)key) & 0xffff);
                acc ^= 0xdeadbeef;
                acc = EvMath.RotateRight(acc, 1);
            }
            else if (tier == 1)
            {
                acc = (acc & 0xffffff00) | (((acc & 0xff) + pstr[i]) & 0xff);
                acc = EvMath.RotateLeft(acc, 5);
                acc = (acc & 0xffff0000) | (((acc & 0xffff) + (uint)key) & 0xffff);
            }
        }
        return acc;
    }

    // sub_72C6C — ASCII-uppercase the Pascal string in place. Faithful off-by-one: the loop starts at
    // length+1, touching one byte past the string body — harmless, since that byte belongs to the same
    // 0x202-byte record block and is never read by the hash. The `i >= pstr.Length` guard is a managed
    // safety net for a tightly-sized array; it never fires for the real call site (the full record).
    private static void UpperPascalName(byte[] pstr)
    {
        for (int i = pstr[0] + 1; i > 0; i--)
        {
            if (i >= pstr.Length) continue;
            byte c = pstr[i];
            if (c >= 'a' && c <= 'z') pstr[i] = (byte)(c - ('a' - 'A'));
        }
    }

    private static byte[] PascalBytes(string s)
    {
        int len = s.Length > 255 ? 255 : s.Length;
        var b = new byte[len + 1];
        b[0] = (byte)len;
        for (int i = 0; i < len; i++) b[i + 1] = (byte)s[i];
        return b;
    }
}
