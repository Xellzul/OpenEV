using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1004198c (EV Override-11.c lines 27014-27023).
//
// Advance the credits-screen cumulative scroll progress by `scrollDelta`
// (a double delta), then repaint the scroll bar (RedrawCreditsProgressBar /
// FUN_100419cc). `*_DAT_100810a0` is the progress double, managed now as
// Graphics.Model.BootProgress.Current.
public static class AdvanceCreditsScrollProgress
{
    public static void Run(double scrollDelta)
    {
        BootProgress.Current += scrollDelta;

        // NO-OP: the decompile's `dStack00000018 = scrollDelta;` is a compiler
        // register-spill artifact (PPC's unoptimised prologue always spills an
        // incoming arg to its 0x18-byte linkage-area home slot), not game logic —
        // FUN_100419cc takes void and never reads it back. Not ported.
        RedrawCreditsProgressBar.Run();
    }
}
