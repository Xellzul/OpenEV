using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Text;

// FUN_10006910 (EV Override-11.c 3886-3970) — draws the abbreviated SECONDARY AI sub-state
// label (ship field 0xa76) in the debug target panel; same migration as DrawAiStateLabel
// (do NOT re-derive offsets from the decompile's ×4 `ppuVar1 + -0x17XX` ptr arith).
// Original quirk KEPT: state 6 = "T+F" — its (int)-cast offset GameToc-0x6b97 lands in the
// float-constant pool (bytes 0x54 0x2b 0x46), drawn as a Pascal string. Likely never occurs.
public static class DrawAiManeuverStateLabel
{
    private static readonly string[] Labels =
    {
        "LoJack", "KillSpd", "FlyStel", "FlyHypD", "HypJmp", "RunAwy", "T+F",
        "Missle", "Dock", "Chase", "Zoom", "ChasSlw", "FormFly", "JmpPar",
        "WaitTarg", "Board", "VeerOff", "AfterBurn"
    };

    public static void Run(short stateIndex)
    {
        if ((uint)stateIndex < (uint)Labels.Length)
            MacToolbox.DrawString(Labels[stateIndex]);
    }
}
