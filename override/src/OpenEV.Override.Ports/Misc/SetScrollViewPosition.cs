using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007ace8 (EV Override-11.c lines 52559-52622).
//
// Stores the scroll offset into the game window, (re)allocates the two offscreen GWorlds
// for the new content size, stamps the content width into their pixmaps, clears the
// scrolled regions, and stages the background PICT.
//
// MANAGED: the render context is GlobalState. The two offscreen GWorld sub-records
// (primary = OffscreenGameGWorld +0x1e, secondary = AnimScratchPort +0x38) are
// disposed/recreated via the ref helper overloads; the GWorld-port / pixmap structs
// they point at are walked through semantic MacToolbox accessors.
public static class SetScrollViewPosition
{
    public static void Run(int horizPos, int vertPos)
    {
        GlobalState.ScrollHoriz = (short)horizPos;
        GlobalState.ScrollVert = (short)vertPos;

        // contentRect = {0, 0, InnerBottom, InnerRight+8}.
        short[] contentRect = new short[4];
        MacToolbox.SetRect(contentRect, 0, 0, (short)(GlobalState.InnerRight + 8), GlobalState.InnerBottom);

        if (GlobalState.OffscreenGameGWorld != 0)
            DisposeGWorldRecord.Run(ref GlobalState.OffscreenGameGWorld, ref GlobalState.OffscreenGameGDevice, ref GlobalState.OffscreenGameRowTable);
        if (GlobalState.AnimScratchPort != 0)
            DisposeGWorldRecord.Run(ref GlobalState.AnimScratchPort, ref GlobalState.AnimScratchGDevice, ref GlobalState.AnimScratchRowTable);
        CallGWorldOpOrFatal.Run(ref GlobalState.OffscreenGameGWorld, ref GlobalState.OffscreenGameGDevice, ref GlobalState.OffscreenGameRowTable, contentRect, out int primaryStageTopLeft, out int primaryStageBotRight);
        CallGWorldOpOrFatal.Run(ref GlobalState.AnimScratchPort, ref GlobalState.AnimScratchGDevice, ref GlobalState.AnimScratchRowTable, contentRect, out int scratchStageTopLeft, out int scratchStageBotRight);

        // Seed each record's stage Rect from the created GWorld's portRect (FUN_10079468
        // param_1[3]/[4] — ctx+0x2a/+0x2e for the primary, ctx+0x44/+0x48 for the scratch).
        // The black-out PaintRect(PrimaryStageRect/ScratchStageRect) at landing / galaxy-map
        // close / game entry needs a non-degenerate Rect — without the bottom (= InnerBottom)
        // it painted nothing and ships stayed visible behind the landing dialog. The right
        // edge is restamped to contentWidth below (ctx+0x30 / +0x4a).
        GlobalState.PrimaryStageTop = (short)(primaryStageTopLeft >> 16);
        GlobalState.PrimaryStageLeft = (short)primaryStageTopLeft;
        GlobalState.PrimaryStageBottom = (short)(primaryStageBotRight >> 16);
        GlobalState.ScratchStageTop = (short)(scratchStageTopLeft >> 16);
        GlobalState.ScratchStageLeft = (short)scratchStageTopLeft;
        GlobalState.ScratchStageBottom = (short)(scratchStageBotRight >> 16);

        // Stamp the content width into each port's portRect.right and (under Color QD)
        // its pixmap's bounds.right.
        short contentWidth = (short)(contentRect[3] - 8);   // SetRect 'right' - 8 == InnerRight
        MacToolbox.SetPortRectRight(GlobalState.OffscreenGameGWorld, contentWidth);
        GlobalState.PrimaryContentWidth = contentWidth;
        if (GlobalState.ColorQuickDrawFlag != 0)
            MacToolbox.SetPixMapBoundsRight(MacToolbox.GetPortPixMap(GlobalState.OffscreenGameGWorld), contentWidth);
        MacToolbox.SetPortRectRight(GlobalState.AnimScratchPort, contentWidth);
        GlobalState.SecondaryContentWidth = contentWidth;
        if (GlobalState.ColorQuickDrawFlag != 0)
            MacToolbox.SetPixMapBoundsRight(MacToolbox.GetPortPixMap(GlobalState.AnimScratchPort), contentWidth);

        if (GlobalState.ActivePortPixmap == 0)
            // Message from data-seg cell 0x10085c4c (StaticData.UiErrorStrings[NoWindowErrorIndex]).
            FatalOutOfMemoryExit.Run(StaticData.UiErrorStrings[StaticData.NoWindowErrorIndex]);

        // (The decompile builds a scroll Rect from WindowBounds offset to origin here but
        // never uses it — dropped as dead.)

        // Clear the primary then secondary GWorld's scrolled region. Faithful quirk: the
        // decompile passes the SCRATCH port's visRgn to BOTH RectRgn calls (piVar1[0xe]+0x18).
        GWorldPort.SetActivePortSecondaryGame();
        short[] gameRect = MacToolbox.GetPortRectShorts(GlobalState.OffscreenGameGWorld);
        MacToolbox.ClipRect(gameRect);
        MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(GlobalState.AnimScratchPort), gameRect);
        MacToolbox.PaintRect(gameRect);
        GWorldPort.SetActivePortScratch();
        short[] scratchRect = MacToolbox.GetPortRectShorts(GlobalState.AnimScratchPort);
        MacToolbox.ClipRect(scratchRect);
        MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(GlobalState.AnimScratchPort), scratchRect);
        MacToolbox.PaintRect(scratchRect);

        LoadAndStagePictResource.Run(horizPos, vertPos);
        SetGamePortAndDevice.Run();
    }
}
