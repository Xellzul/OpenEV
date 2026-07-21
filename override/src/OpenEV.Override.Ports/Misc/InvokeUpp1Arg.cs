using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 54588-54596.
public static class InvokeUpp1Arg
{
    public static void Run(int arg1)
    {
        // DEVIATION (faithful): FUN_1008062c (InvokeMacUpp) is the generic PowerPC CFM
        // "call through a TVector" glue (lwz r0,0(r12); mtctr r0; lwz r2,4(r12); bctr)
        // shared by many call sites across the binary, so it's correctly a no-op absorber
        // in general — the decompile can't resolve an arbitrary caller's live r12. But
        // FUN_1007e348 (this function) has exactly ONE call site in the whole binary:
        // FatalOOM (FUN_10078aec), which only reaches here with the value it read from
        // the global scratch cell (unk_82466) — and that cell has exactly ONE writer,
        // FUN_10078ae0 / GWorldPort.SetCurrentGameWindow, always with the constant
        // GameWindowGlobals.CurrentWindowSource (0x10082588). The disassembly records
        // loc_54EFC (FUN_10054efc / TeardownAudioForExit) with "DATA XREF:
        // seg001:off_82588o" — i.e. word 0 at address 0x10082588 IS the code pointer
        // loc_54EFC, making 0x10082588 the 2-word TVector {code = loc_54EFC, toc} that
        // the lwz/mtctr/bctr sequence resolves and jumps to. So for the one arg1 value
        // this call site can ever pass, the real dispatch target is known and singular —
        // port it directly instead of leaving this reachable, resolvable case as a no-op.
        if (arg1 == GameWindowGlobals.CurrentWindowSource)
        {
            TeardownAudioForExit.Run();
            return;
        }

        InvokeMacUpp.Run();
    }
}
