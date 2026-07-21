using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 49542-49567.
// Reachability: `.start`'s trailing FUN_10075f94 call (ProgramEntry / TitleAdapter) is now
// dead code — the boot orchestrator's step-48 GracefulExit ends the process first (decompile
// 49850, dead after the non-returning FUN_10061bb0). Kept faithfully.
// Exit path whose body is a sound-completion drain: unless the shutdown guard
// is set, run the retry-counter drain loop, drain the deferred completion
// list, fire-and-clear the panic-exit proc, then fall into FullShutdown.
// All the state it touches is the SoundProcs exit-path tier now (see its
// header: every proc cell / counter is verified write-free in the binary, so
// the drain loops and the FUN_1008062c proc dispatches are dead branches —
// transcribed faithfully).
public static class PanicExit
{
    public static void Run(int exitCode)
    {
        // piVar1 = *0x10081270 (-> SoundProcs.PanicExitProc's BSS cell), read
        // before the guard test in the original — moot with managed fields.
        if (SoundProcs.ShutdownGuard == 0)   // *(int *)PTR_DAT_10081274 == 0 (always true)
        {
            junkcode.FUN_10076238();    // empty in the binary (callers passed an unused 0)
            while (0 < SoundProcs.PanicExitRetryCounter)   // never >0
            {
                SoundProcs.PanicExitRetryCounter--;
                // FUN_1008062c() glue, dispatching through unk_109D50[counter-1] — an
                // indexed proc TABLE, not a single fixed cell (see DDC-10). Moot either
                // way: the loop body is unreachable (counter never >0), so no table
                // entry is ever consulted at runtime.
                InvokeMacUpp.Run();
            }
            junkcode.FUN_1007623c();    // empty in the binary
            DrainCompletionCallbacks.Run();
            if (SoundProcs.PanicExitProc != 0)   // never set in the binary — dead branch, kept
            {
                InvokeMacUpp.Run();         // FUN_1008062c() glue, see above
                SoundProcs.PanicExitProc = 0;
            }
        }
        // FUN_10076058(param_1): the callee is void(void) — the original just
        // leaves exitCode in r3 and FullShutdown ignores it.
        FullShutdown.Run();
    }
}
