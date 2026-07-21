using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10071f3c (EV Override-11.c lines 46884-46914): the registered
// owner name for display (the credits "<REG>" line). When the registration
// record fails its self-check, falls back to STR# 900 item 2 (the "unregistered"
// placeholder). Returns -1001 when no session is open.
// Managed string out (was a fill-a-Str255-by-address out-param — the
// Credits_RegBuf boundary buffer is gone).
public static class GetRegisteredOwnerName
{
    public static int Run(out string ownerName)
    {
        // DEVIATION (faithful): on this early-return path the ASM (sub_71F3C) leaves
        // the caller's Str255 buffer UNTOUCHED — CreditsScroller discards this return
        // code and uses the buffer unconditionally, so an unregistered real Mac shows
        // raw stack garbage as the credits owner name. C#'s out-param definite
        // assignment forces a value here; "" substitutes for that unpreservable UB.
        ownerName = "";
        if (ShareWareGlobals.Registered == 0)
        {
            return -1001; // 0xfffffc17 — no registration session open
        }
        CheckShareWareRegistrationMatch.Run(out byte regCodeMatches);
        if (regCodeMatches == 0)
        {
            ownerName = MacToolbox.GetIndString(900, 2);
        }
        else
        {
            // The registration record's first 0x100 bytes are a Str255 owner name.
            int nameHandle = LoadRegistrationRecord.Run(900);
            WriteHandleToFile.Run(nameHandle, 900);
            ownerName = MacToolbox.PascalToString(MacToolbox.HandleToBytes(nameHandle));
            MacToolbox.HUnlock(nameHandle);
            MacToolbox.DisposeHandle(nameHandle);
        }
        return 0;
    }
}
