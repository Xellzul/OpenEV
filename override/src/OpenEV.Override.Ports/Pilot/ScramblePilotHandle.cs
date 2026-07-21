using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_100617c4 (EV Override-11.c lines 40827-40873) — the pilot-file XOR
// scrambler/descrambler (involution: the same pass both scrambles and descrambles).
// XORs the handle's data in place word-by-word with a rolling key seeded 0xabcd1234
// (advanced `+0xdeadbeef ^ 0xdeadbeef` per word), then streams the final key over the
// trailing 1-3 bytes.
public static class ScramblePilotHandle
{
    public static int Run(int handle, uint xorKey)
    {
        if (handle == 0)
            return -109;   // nilHandleErr (Mac OSErr 0xffffff93), as the original

        // Every resource handle in the port is a registry byte[] — run the pass in place
        // (XorBytes is bit-identical to the Mac raw path: big-endian word XOR with the
        // rolling key, tail bytes streamed from the key's top byte).
        var managed = MacToolbox.ResourceBytes(handle);
        if (managed == null)
            // Master pointer nil (a purged handle on the original Mac) — the 6 real callers
            // only ever HPurge/HNoPurge around this call (both no-op shims) and never
            // DisposeHandle the handle first, so this is unreachable in practice, but matched
            // here since real callers branch on this return value.
            return -109;   // nilHandleErr (Mac OSErr 0xffffff93), as the original

        XorBytes(managed, xorKey);
        return 0;
    }

    // Same pass over a managed byte[] (big-endian words, rolling key, tail bytes streamed
    // from the key's top byte — bit-identical to the Mac raw path above).
    private static void XorBytes(byte[] data, uint key)
    {
        int wordCount = data.Length >> 2;
        int rem = data.Length - wordCount * 4;
        int p = 0;
        for (int i = 0; i < wordCount; i++, p += 4)
        {
            data[p] ^= (byte)(key >> 24);
            data[p + 1] ^= (byte)(key >> 16);
            data[p + 2] ^= (byte)(key >> 8);
            data[p + 3] ^= (byte)key;
            key = (key + 0xdeadbeef) ^ 0xdeadbeef;
        }

        // Only `rem` bytes are ever written below, so zero-padding tail's missing byte
        // lanes here matches the ASM's raw 4-byte word read exactly — its over-read bytes
        // (past the end of `data`) never land in a write either.
        uint tail = 0;
        for (int i = 0; i < rem; i++) tail |= (uint)data[p + i] << (24 - i * 8);
        key ^= tail;
        for (int i = 0; i < rem; i++)
        {
            data[p + i] = (byte)(key >> 24);
            key <<= 8;
        }
    }
}
