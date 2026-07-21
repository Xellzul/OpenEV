using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Text;

// FUN_1000679c (EV Override-11.c 3819-3885) — draws the abbreviated PRIMARY AI-state label
// (ship field 0xa74) in the debug target panel; labels are consecutive data-seg Pascal
// strings from GameToc-0x5f32, baked below. Do NOT re-derive the offsets from the decompile's
// `ppuVar1 + -0x17XX` — that is undefined** pointer arithmetic (×4), not byte offsets.
public static class DrawAiStateLabel
{
    private static readonly string[] Labels =
    {
        "HiJack", "GoStel", "HypOut", "DefRet", "FightSh", "GoHome", "Wait",
        "Inspect", "JumpIn", "Refuel", "FlyPr", "HypPr", "Protect", "Plunder"
    };

    public static void Run(short stateIndex)
    {
        if ((uint)stateIndex < (uint)Labels.Length)
            MacToolbox.DrawString(Labels[stateIndex]);
    }
}
