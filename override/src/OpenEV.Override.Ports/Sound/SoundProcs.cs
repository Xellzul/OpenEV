using System;

namespace OpenEV.Override.Ports.Sound;

// Managed home for the sound subsystem's UPP / callback tier — the cells at
// 0x100851a0..0x100851b0 (interrupt-mask UPP pair), 0x1008129c (error handler),
// 0x10081280 (completion list head -> BSS 0x10089c7c) and 0x10081a34 (the
// 32-entry pending-command table -> BSS 0x100898fc).
public static class SoundProcs
{
    // FUN_10075f08 — CallUniversalProc(*0x100851a0, 0x32): raise the interrupt
    // level around mixer-queue mutation and return the previous mask.
    // FUN_10075f50 — CallUniversalProc(*0x100851ac, 0x1802, mask): restore it.
    // The port runs the mixer on the game thread only, so these are HONEST no-ops kept
    // as named calls so every original save/restore pair stays line-mapped.
    public static int SaveInterruptMask() => 0;
    public static void RestoreInterruptMask(int mask) { }

    // Sound-subsystem error callback (was the TVector ptr behind cell 0x1008129c
    // -> 0x10089ff4; InitSoundSubsystem zeroes it, ReportSoundError invokes it
    // with the error code when set).
    public static Action<short>? ErrorHandlerProc;

    // Deferred completion-notify list (was the linked list headed at BSS
    // 0x10089c7c behind cell 0x10081280). DrainCompletionCallbacks
    // (FUN_10076294) pops nodes and invokes the completion glue with
    // (node.Arg, -1). ORIGINAL QUIRK (verified, B1): the binary contains NO
    // pusher for this list — only the drain — so the head stays null forever
    // and the drain's invoke is a documented dead branch.
    public sealed class SndCompletionNode
    {
        public SndCompletionNode? Next;
        public int Arg;
    }

    public static SndCompletionNode? CompletionListHead;

    // The 32-entry x 7-int pending-slot table (was BSS 0x100898fc behind cell
    // 0x10081a34). Writer = InsertIntoSlotTable (FUN_10076240): finds the first
    // entry with Words[2] == 0 and stores its 7 params as
    // {[2],[3],[4],[5],[0],[1],[6]}. ProbeSoundChannelSampleRate queues a
    // deferred SndSoundManagerVersion call through it. TODO(B2): find the
    // consumer and give Words semantic names.
    public sealed class PendingSlotEntry
    {
        public int[] Words = new int[7];
    }

    public static readonly PendingSlotEntry[] PendingSlotTable = NewPendingSlots();

    private static PendingSlotEntry[] NewPendingSlots()
    {
        var t = new PendingSlotEntry[32];
        for (int i = 0; i < t.Length; i++) t[i] = new PendingSlotEntry();
        return t;
    }

    // ── exit-path drain tier (PanicExit FUN_10075f94 / FullShutdown FUN_10076058) ──
    // Proc cells: 0x10081270 -> BSS 0x100898f4 (PanicExitProc), 0x1008126c -> BSS
    // 0x100898f0 (FullShutdownProc), 0x10081274 -> BSS 0x100898f8 (ShutdownGuard).
    // Retry counters: BSS 0x10082438 (toc ppu[-0x188a]) drained by PanicExit, BSS
    // 0x10082434 (ppu[-0x188b]) drained by FullShutdown.
    // ORIGINAL QUIRK (verified, B2b — grep of every cell + toc/ppu alias): the binary
    // contains NO writer for any of the three proc cells (the exit paths only read +
    // zero them) and the retry counters are only ever DECREMENTED, never raised — all
    // five stay 0 forever, so both drain loops and both FUN_1008062c cross-TOC proc
    // dispatches are dead branches (same dead-path family as CompletionListHead above).
    public static int PanicExitProc;             // was *0x100898f4 (cell 0x10081270)
    public static int FullShutdownProc;          // was *0x100898f0 (cell 0x1008126c)
    public static int ShutdownGuard;             // was *0x100898f8 (cell 0x10081274); PanicExit drains only while 0
    public static int PanicExitRetryCounter;      // was BSS 0x10082438 (PanicExit's drain loop)
    public static int FullShutdownRetryCounter;   // was BSS 0x10082434 (FullShutdown's drain loop)
}
