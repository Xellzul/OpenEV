using OpenEV.Override.Ports.Sound;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 49573-49596.
// The terminal exit path: drain the (always-zero) retry counter, fire-and-clear
// the (never-installed) full-shutdown proc, then ExitToShell. Same dead-branch
// family as PanicExit — see the SoundProcs exit-path tier header for the
// verified no-writer findings; cell/offset provenance for every field below
// lives on the SoundProcs accessors themselves, not repeated here.
public static class FullShutdown
{
    public static void Run()
    {
        junkcode.FUN_10076238();
        while (0 < SoundProcs.FullShutdownRetryCounter)   // never >0 — dead branch, kept
        {
            SoundProcs.FullShutdownRetryCounter--;
            InvokeMacUpp.Run();   // FUN_1008062c() — glue for a proc that's never installed
        }
        junkcode.FUN_1007623c();
        junkcode.FUN_10076234();
        if (SoundProcs.FullShutdownProc != 0)   // never set — dead branch, kept
        {
            InvokeMacUpp.Run();
            SoundProcs.FullShutdownProc = 0;
        }
        MacToolbox.ExitToShell();
    }
}
