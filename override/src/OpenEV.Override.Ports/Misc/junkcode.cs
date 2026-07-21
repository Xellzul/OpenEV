namespace OpenEV.Override.Ports.Misc;

// Home for "junk code": trivial decompiled functions that have no real
// behaviour (constant returns / no-ops) and aren't worth a semantic name.
// Methods keep their raw FUN_xxxxxxxx names, so call sites read as
// `junkcode.FUN_xxxxxxxx()` — a deliberate signal that the callee is a stub.
public static class junkcode
{
    // FUN_100600f4 (EV Override-11.c:40121-40125): the whole body is `return 1;`.
    // An OS-capability probe the old-OS-warning gate (ShowOldOsWarningIfNeeded)
    // calls; it always reports OK.
    public static int FUN_100600f4() => 1;

    // Empty toolbox-callback stubs — each decompile body is exactly `void FUN_xxx(void)
    // { return; }` (verified). The decompile drops unused params from a few of these
    // signatures because the ASM callers pass a now-ignored arg (a stale Mac handle, or a
    // literal 0); the real signatures are no-arg, so nothing is dropped here.
    public static void FUN_10023060() { }   // EV Override-11.c:15295-15304
    public static void FUN_100314cc() { }   // 20288-20297
    public static void FUN_10060094() { }   // 40080-40084 (callers passed an unused node ptr)
    public static void FUN_100600ec() { }   // 40105-40109 (one caller: FUN_10061bb0 boot step 15, GameBootSequence.cs)
    public static void FUN_100600f0() { }   // 40113-40117
    public static void FUN_10076234() { }   // 49743-49750
    public static void FUN_10076238() { }   // 49751-49758 (callers passed an unused 0)
    public static void FUN_1007623c() { }   // 49759-49768 (callers passed an unused 0)
    public static void FUN_100762e8() { }   // 49818-49822 — decompile's only caller is the unported PEF
                                             // entry() (line 49843); not called anywhere in the C# port.
}
