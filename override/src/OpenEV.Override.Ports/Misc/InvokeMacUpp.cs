namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 59286-59299.
public static class InvokeMacUpp
{
    // Variadic absorber. FUN_1008062c is a CFM glue thunk in the original
    // binary — callers reach it with whatever register set the caller
    // computed; this overload absorbs 1..N args so call sites compile.
    public static void Run(params object?[] _) { }

    public static void Run()
    {
        // NO-OP: (*(code *)*in_r12)() — indirect call through a UPP pointer the
        // caller sets in r12 at each call site; no static target here. A call
        // site whose UPP value IS resolvable inlines its own dispatch instead
        // of routing through here — see InvokeUpp1Arg.
        return;
    }
}
