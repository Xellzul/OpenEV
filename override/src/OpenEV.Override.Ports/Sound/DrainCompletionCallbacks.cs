using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49802-49814.
// Pops every node off the deferred completion list and invokes the FUN_1008062c
// cross-TOC glue (see InvokeMacUpp) with (node.Arg, -1) — the same dead-branch
// idiom PanicExit/FullShutdown use for the identical FUN_1008062c dispatch. See
// SoundProcs for why the list is always empty at runtime (no pusher exists in
// the binary), so this call never actually fires.
public static class DrainCompletionCallbacks
{
    public static void Run()
    {
        SoundProcs.SndCompletionNode? node;
        while ((node = SoundProcs.CompletionListHead) != null)
        {
            SoundProcs.CompletionListHead = node.Next;
            InvokeMacUpp.Run(node.Arg, -1);
        }
    }
}
