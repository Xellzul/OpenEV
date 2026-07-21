namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 47748-47759 (+ helper FUN_1007360c, 47730-47745).
//
// Original: FUN_1007360c lazily caches NGetTrapAddress(0xA88F, toolbox) in a
// library cell (guard _DAT_100823f8 / toc[-0x189a]) and FUN_10073650 invokes
// that 68k trap through CallUniversalProc(upp, procInfo 0x2e0, param_1, 0x5b)
// — styled-text/notification-library window-teardown glue, NOT a Sound Manager
// call (the "Snd" name predates the B3 audit). Sole caller: FUN_100734f4
// (CloseSlideShowWindow) passes the just-hidden window. The port has no 68k trap
// table or Mixed Mode Manager, so the trap can never resolve: honest
// documented no-op (the caller already ShowHide'd the window and zeroes the
// record field itself).
//
// Coordinated relocation (follow-up to the Rule 19 make-nice note on its twin
// InstallCallbackPtr.cs): moved Sound/ -> Graphics/ together with that twin —
// both are window-teardown glue misfiled under Sound/, both called only from
// Graphics/CloseSlideShowWindow.cs, neither actually a Sound Manager call.
public static class CallSndUPP
{
    public static void Run(int window) { }
}
