using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_100720d8 — EV Override-11.c lines 46960-46974. Returns the stats record's accumulated
// TotalPlaySeconds / 3600, or error -1001 when the build is not registered.
public static class GetInstallHours
{
    // int-out overload — the original's single uint* result is read through int locals at some
    // call sites; forwards to the uint implementation.
    public static int Run(out int hoursOut)
    {
        int rc = Run(out uint hours);
        hoursOut = (int)hours;
        return rc;
    }

    public static int Run(out uint hoursOut)
    {
        // DEVIATION (faithful): the not-registered path never writes *param_1 (the ASM leaves it
        // as the caller's indeterminate stack value); C#'s `out` rules force a value, so we zero it.
        hoursOut = 0;
        if (ShareWareGlobals.Registered == 0)
        {
            return -1001; // "not registered" error
        }
        // GetDateTime's output is discarded — the ASM fills this buffer but never reads it.
        MacToolbox.GetDateTime(new int[3]);
        hoursOut = (uint)ShareWareGlobals.Record.TotalPlaySeconds / 3600; // (uint) → unsigned divide (ASM divwu)
        return 0;
    }
}
