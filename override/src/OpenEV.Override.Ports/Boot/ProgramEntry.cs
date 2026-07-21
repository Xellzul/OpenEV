namespace OpenEV.Override.Ports.Boot;

// entry() / ASM `.start` — the PEF/CFM program entry point (EV Override-11.c 49836-49852;
// verified against ASM `.start`). The CodeWarrior C-runtime startup wrapper: init the
// runtime, register the fragment's SoundManager-version routine descriptor, run main
// (FUN_10061bb0 = GameBootSequence), then FUN_10075f94 = PanicExit. GameBootSequence ends
// the process itself — its title-loop tail runs GracefulExit -> ExitToShell (step 48) — so
// it never returns and that trailing PanicExit call is dead code, faithfully kept.
//
// On .NET the CFM runtime IS the host substrate: the real process entry is
// OpenEV.Override.Game.Program.Main -> OverrideGameHost -> TitleAdapter, which performs the
// equivalent startup and — because it can't block in the Mac title loop — calls
// GameBootSequence.RunPreTitle() then RunTitleLoop() on a background thread instead of one
// call to the blocking Run(). So this Run() IS `.start`'s call graph, just composed
// piece-by-piece at that call site instead of as one Run() call — the same reason the host
// already split RunPreTitle out of GameBootSequence.Run(). The four CFM-runtime steps that
// precede the game (steps 2-5 below) are documented but skipped, not called (they're the
// loader's own bookkeeping, not game logic).
public static class ProgramEntry
{
    public static void Run()
    {
        // 1. local_18[0] = 0 — zeroes a stack temp later passed (and ignored) by FUN_10061bb0.
        // 2. FUN_100762e8() — CFM runtime init; empty stub in the decompile (ported as
        //    Misc.junkcode.FUN_100762e8). No-op.
        // 3. FUN_100762f4() — returns &_toc; its result feeds only the step-4 registration.
        //    .NET owns the TOC.
        // 4. _DAT_1008243c = FUN_10076240(sndMgr globals, &SndSoundManagerVersion TVector, _toc)
        //    — registers the fragment's SoundManager-version routine descriptor into the CFM
        //    connection table _DAT_10081a34 (32 slots x 7 words) and saves the slot index. The
        //    slot-insert logic is ported generically as Sound.InsertIntoSlotTable, but this call
        //    site is skipped: the index is never read by game logic, and its sndMgr TVector params
        //    (off_8128x glue) are unwired CFM data, not real game state.
        // 5. FUN_10000000() — `bl sub_0`; empty stub (ported as EmptyStub). No-op.
        // 6. FUN_10061bb0(0, local_18) — run the game: boot -> title loop -> exit. The two args are
        //    ignored (FUN_10061bb0 is void).
        GameBootSequence.Run();
        // 7. FUN_10075f94(0) — NOT CFM runtime machinery: it's the game's own PanicExit
        //    (Misc.PanicExit), `.start`'s FINAL statement after FUN_10061bb0. It is DEAD CODE:
        //    FUN_10061bb0's title-loop tail runs GracefulExit (FUN_1005296c) -> ExitToShell, so
        //    it never returns and this line is unreachable — kept faithfully, exactly as the
        //    decompile keeps it after the non-returning FUN_10061bb0 (line 49850). The literal
        //    arg is 0 (ASM: `li r31,0` then `addi r3,r31,0`); PanicExit.Run ignores exitCode.
        Misc.PanicExit.Run(0);
    }
}
