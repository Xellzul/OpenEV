namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 54671-54686.
public static class InvokeUppIntShort
{
    public static void Run(int payload1, short payload2, int dispatchUpp)
    {
        // payload1/payload2 reload into r3/r4 (the invoked UPP's own args, `lha` sign-
        // extending the short); dispatchUpp reloads into r12, the CFM glue's dispatch
        // register (see InvokeMacUpp), intentionally dropped here — same "forward the
        // payload, drop the token" no-op convention as InvokeNodeUpdateUpp/InvokeNodeCollisionUpp's
        // unresolved-token fallback branch.
        InvokeMacUpp.Run(payload1, (int)payload2);
    }
}
