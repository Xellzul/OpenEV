using System;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10072700 (EV Override-11.c lines 47172-47187) — build the DEFAULT registration
// record used when the "<STR#900:1> License" prefs file is missing/empty: a cleared 0x202 block
// stamped with the "not registered" placeholders from STR# strListId — owner name (item 2) at +0,
// copy count (0) at +0x200, stored code (item 3) at +0x100.
//
// The Mac allocated a NewHandleClear(0x202) and filled *handle; the managed record is a byte[]
// wrapped as a NewHandleFromBytes handle so its callers (GenerateRegistrationCode /
// GetRegisteredOwnerName) read it via HandleToBytes. Same shared code the register app ported as
// Prefs.BuildDefaultRegistrationRecord (FUN_1000a4a8).
//
// Bug-for-bug: the decompile skips both GetIndString stamps (returning an unstamped, all-zero
// record) when NewHandleClear or MemError fails — unreachable in the managed model, so the alloc
// folds into the byte[] and the stamps always run (same simplification as LoadRegistrationRecord).
public static class BuildDefaultRegistrationRecord
{
    private const int RecordSize = 0x202;   // name[0x100] + code[0x100] + copy-count short[0x200]
    private const int CodeOffset = 0x100;
    // copy count lives at +0x200 and stays 0 (the cleared block).

    public static int Run(int strListId)
    {
        var record = new byte[RecordSize];
        StampPascal(record, 0, MacToolbox.GetIndString((short)strListId, 2));
        StampPascal(record, CodeOffset, MacToolbox.GetIndString((short)strListId, 3));
        return MacToolbox.NewHandleFromBytes(record);
    }

    private static void StampPascal(byte[] record, int offset, string value)
    {
        byte[] pstr = MacToolbox.StringToPascalBytes(value);
        int n = Math.Min(pstr.Length, record.Length - offset);
        Array.Copy(pstr, 0, record, offset, n);
    }
}
