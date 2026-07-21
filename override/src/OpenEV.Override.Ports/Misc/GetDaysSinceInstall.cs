using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_10072008 — EV Override-11.c lines 46915-46940. Days since install (registered builds only).
public static class GetDaysSinceInstall
{
    public static int Run(out int daysOut)
    {
        // DEVIATION (faithful): the not-registered path never writes *param_1 (the ASM leaves it
        // as the caller's indeterminate stack value); C#'s `out` rules force a value, so we zero it.
        daysOut = 0;
        int resultCode = 0;
        if (ShareWareGlobals.Registered == 0)
        {
            resultCode = -1001; // "not registered" error
        }
        else
        {
            uint[] dateTime = new uint[3];
            MacToolbox.GetDateTime(dateTime);
            // Must stay UNSIGNED 32-bit (ASM divwu/cmplw): Mac epoch seconds exceed 2^31, so a
            // signed subtract/compare would go negative and misfire both the day count and the clamp.
            uint installDate = (uint)ShareWareGlobals.Record.InstallDateSeconds;
            daysOut = (int)((dateTime[0] - installDate) / 86400 + 1); // seconds per day
            if (dateTime[0] < installDate)
            {
                daysOut = 90; // clock set back (now < install) → clamp to 90 days
            }
        }
        return resultCode;
    }
}
