namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100720a8 (EV Override-11.c lines 46941-46954) — hand back the registration
// code word from the shareware stats record, or -1001 when not registered.
public static class GetRegistrationCode
{
    public static int Run(out int code)
    {
        code = 0;
        if (ShareWareGlobals.Registered == 0)
        {
            return -1001;   // 0xfffffc17 — no registration session open
        }
        code = ShareWareGlobals.Record.RegCodeWord;
        return 0;
    }
}
