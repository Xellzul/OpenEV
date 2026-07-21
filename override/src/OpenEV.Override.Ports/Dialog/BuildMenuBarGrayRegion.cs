using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1005eff0 (EV Override-11.c lines 39576-39608).
//
// Builds the menu-bar gray region (GrayRgn ∪ menu-bar rect) and resets the saved
// menu-bar-height / hidden-flag globals that HideMacMenuBar/RestoreMacMenuBar use.
// The four menu-bar globals live in the managed Dialog.Model.MenuBarState now.
//
// On Windows there is no Window Manager port (GetWMgrPort is a no-op stub → 0):
// NewRgn/OpenRgn/CloseRgn/UnionRgn are no-op stubs, and FrameRect resolves to a
// degenerate (all-zero) rect from GetPortRectShorts(0), so its own width/height
// guard early-returns without drawing — the framing sequence below is inert in
// practice, but it runs UNCONDITIONALLY, exactly as the ASM does (no null check
// on the WMgrPort result).
public static class BuildMenuBarGrayRegion
{
    public static void Run()
    {
        int[] savedPort = new int[4];
        MacToolbox.GetPort(savedPort);

        // Reset the menu-bar globals Hide/RestoreMacMenuBar consult.
        MenuBarState.SavedRgn = 0;
        MenuBarState.SavedMBarHeight = (short)MacToolbox.LMGetMBarHeight();
        MenuBarState.Hidden = 0;

        int wMgrPort = MacToolbox.GetWMgrPort();
        MacToolbox.SetPort(wMgrPort);
        MenuBarState.GrayRgn = MacToolbox.NewRgn();
        MacToolbox.OpenRgn();
        MacToolbox.FrameRect(MacToolbox.GetPortRectShorts(wMgrPort));   // WMgrPort portRect (was port+0x10)
        MacToolbox.CloseRgn(MenuBarState.GrayRgn);
        MacToolbox.UnionRgn(MacToolbox.GetGrayRgn(), MenuBarState.GrayRgn, MenuBarState.GrayRgn);
        MacToolbox.SetPort(savedPort[0]);
    }
}
