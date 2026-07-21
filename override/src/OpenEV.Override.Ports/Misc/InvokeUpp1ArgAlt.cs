namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 54600-54611. Byte-identical duplicate compile of
// FUN_1007e348 (InvokeUpp1Arg) — same body, a second entry point for a different caller.
public static class InvokeUpp1ArgAlt
{
    public static byte Run(int dispatchUpp)
    {
        // dispatchUpp reloads into r12, the CFM glue's dispatch register (see InvokeMacUpp);
        // it carries no payload args of its own.
        InvokeMacUpp.Run();

        // NO-OP: the real return value is whatever the invoked UPP left in r3 — this
        // function never writes r3 itself, so it's a genuine pass-through (the sole
        // caller captures it as `cVar3 = FUN_1007e374(...); if (cVar3 != '\0')`,
        // EV Override-11.c:54281). Since InvokeMacUpp never actually calls anything,
        // there's no real value to propagate; hardcoded 0 is the conservative no-op
        // default (keeps that caller's "callback said continue" branch untaken).
        return 0;
    }
}
