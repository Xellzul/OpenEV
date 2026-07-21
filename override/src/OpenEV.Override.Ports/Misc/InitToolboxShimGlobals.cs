using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 53131-53139.
//
// Lazy first-time render-window init: builds a default render window (an empty
// bounds Rect, centred, no existing window/device). Several guarded call sites
// across Graphics/Resource invoke this only while the toolbox-shim flag
// (*0x100812a4) is 0.
//
// DORMANT in practice: InitRenderWindow (called below) sets that flag to 1 on
// entry, and the port's explicit boot-time InitGalaxyMapWindow -> InitRenderWindow
// call runs first, so the guards never fire again. Original-game behavior, not a
// port shortcut; kept as the faithful lazy-init fallback the guards reference.
public static class InitToolboxShimGlobals
{
    public static void Run()
    {
        short[] emptyRect = new short[4];
        MacToolbox.SetRect(emptyRect, 0, 0, 0, 0);

        // FUN_1007b2f4's last two args (refreshPixMap, clampToBounds) are stack-passed;
        // this caller's ASM writes stw 0 -> 0x38(r1) and stw 1 -> 0x3C(r1), giving
        // refreshPixMap=false, clampToBounds=true.
        InitRenderWindow.Run(0, 0, emptyRect, 0, 0, false, true, false, refreshPixMap: false, clampToBounds: true);
    }
}
