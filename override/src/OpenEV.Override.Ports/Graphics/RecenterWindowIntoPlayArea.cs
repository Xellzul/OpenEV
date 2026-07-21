using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100583c8 (EV Override-11.c lines 36224-36313): nudge a freshly opened
// dialog/window into the in-game play area (the play field minus the 144px HUD panel).
// Three passes over the window's portRect, re-read after each potential MoveWindow:
//   1. right edge into the panel strip (PortRight-149) -> push left (clamped at PortLeft);
//   2. fully inside with a >=5px left margin -> centre horizontally over the panel-less width;
//   3. bottom past the play-area bottom (PortTop+370) while still overlapping the strip -> pull up.
//
// Play-area bounds are the render-context port rect = GlobalState.Port{Top,Left,Right}.
// The original round-trips each corner Point LocalToGlobal (window port)
// -> GlobalToLocal (game port) around every compare; both are no-op host shims, so the coords
// are used as read. SetGamePortAndDevice (FUN_1007ab1c) between the transforms is kept.
public static class RecenterWindowIntoPlayArea
{
    // Right edge of the play area = port right minus the 149px HUD-panel strip.
    private const int PanelStrip = 149;   // 0x95
                                          // Centring offset: the 144px panel width excluded from the available span.
    private const int PanelWidth = 144;   // 0x90
                                          // Play-area bottom = port top + 370px.
    private const int PlayAreaBottom = 370; // 0x172

    public static void Run(int windowPtr)
    {
        if (windowPtr == 0)
            return;

        int savedPort = MacToolbox.GetPort();
        MacToolbox.SetPort(windowPtr);
        // Window portRect {top,left,bottom,right} (window+0x10..0x16). The original also
        // pre-computes width/height (+0x16-+0x12 / +0x14-+0x10) — dead.
        short[] win = MacToolbox.GetPortRectShorts(windowPtr);
        SetGamePortAndDevice.Run();
        // Pass 1: window right edge past the panel boundary -> push left by the overhang,
        // or clamp to the play-area left edge if the overhang exceeds the left margin.
        if (GlobalState.PortRight - PanelStrip < win[3])
        {
            if (win[3] - (GlobalState.PortRight - PanelStrip) < win[1] - GlobalState.PortLeft)
                MacToolbox.MoveWindow(windowPtr, win[1] - (win[3] - (GlobalState.PortRight - PanelStrip)), win[0], 0);
            else
                MacToolbox.MoveWindow(windowPtr, GlobalState.PortLeft, win[0], 0);
        }

        MacToolbox.SetPort(windowPtr);
        win = MacToolbox.GetPortRectShorts(windowPtr);
        SetGamePortAndDevice.Run();
        // Pass 2: fits inside the play area with a >=5px left margin -> centre horizontally
        // (the -144 centres over the width with the 144px panel excluded).
        if (GlobalState.PortLeft + 5 <= win[1] && win[3] <= GlobalState.PortRight - PanelStrip)
        {
            uint centerDelta = (uint)((win[1] - GlobalState.PortLeft) + (GlobalState.PortRight - win[3]) - PanelWidth);
            MacToolbox.MoveWindow(windowPtr,
                GlobalState.PortLeft + ((int)centerDelta >> 1) + (uint)(((int)centerDelta < 0 && (centerDelta & 1) != 0) ? 1 : 0),
                win[0], 0);
        }

        MacToolbox.SetPort(windowPtr);
        win = MacToolbox.GetPortRectShorts(windowPtr);
        SetGamePortAndDevice.Run();
        // Pass 3: bottom past the play-area bottom while the right edge overlaps the panel
        // strip -> pull the window up by the vertical overhang.
        if (GlobalState.PortTop + PlayAreaBottom < win[2] && GlobalState.PortRight - PanelStrip < win[3])
            MacToolbox.MoveWindow(windowPtr, win[1], win[0] - (win[2] - (GlobalState.PortTop + PlayAreaBottom)), 0);

        MacToolbox.SetPort(savedPort);
    }
}
