using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10071a40 (EV Override-11.c lines 46730-46755) — the shareware registration
// self-check: load the on-disk registration record (LoadRegistrationRecord), write it back
// (WriteHandleToFile, persisting a freshly-created default), recompute the expected code from the
// stored owner name + the copy count at record+0x200 (GenerateRegistrationCode), and compare it
// against the stored code at record+0x100 (EqualString). Sets `matches`; returns -1001 when no
// registration session is open. Same shared logic the register app ports as
// Validation.IsAlreadyRegistered (FUN_1000acd8).
public static class CheckShareWareRegistrationMatch
{
    public static int Run(out byte matches)
    {
        matches = 0;
        if (ShareWareGlobals.Registered == 0)
        {
            return -1001;   // 0xfffffc17 — no registration session open
        }

        // The decompile derefs the record handle unconditionally (no null guard); LoadRegistrationRecord
        // only yields 0 on an open/read failure, the same inputs the original would have crashed on.
        int regHandle = LoadRegistrationRecord.Run(900);
        WriteHandleToFile.Run(regHandle, 900);
        MacToolbox.HLockHi(regHandle);

        byte[] record = MacToolbox.HandleToBytes(regHandle);            // *regHandle — the 0x202 record block
        int copyCount = (short)((record[0x200] << 8) | record[0x201]);  // big-endian copy-count word at +0x200
        int expectedHandle = GenerateRegistrationCode.Run(regHandle, copyCount, 900);
        string storedCode = MacToolbox.PascalToString(record[0x100..]); // Str255
        matches = MacToolbox.EqualString(storedCode, MacToolbox.HandleToBytes(expectedHandle), 1, 1);

        MacToolbox.DisposeHandle(expectedHandle);
        MacToolbox.HUnlock(regHandle);
        MacToolbox.DisposeHandle(regHandle);
        return 0;
    }
}
