using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Decompile: EV Override-11.c lines 46504-46527.
//
// Repaint everything behind the FRONT window after a palette install: build a
// region from the main GDevice's bounds rect and CalcVisBehind/PaintBehind.
// Same shape as Graphics.Model.Palette.RepaintBehindFrontWindow (FUN_100713c0), which
// starts from the BACKMOST window instead.
public static class PaintBehindFrontWindow
{
    public static void Run()
    {
        // SPLIT-TOC FIX (was _toc): base = unassigned RTOC artifact. The read at
        // -0x6270 resolves under GameToc to 0x100823f0 = the palette/GDevice-state
        // record → PaletteState. The decompile's `**(*(rec)+2) + 0x22` is *GDHandle +
        // 0x22 = the GDevice gdRect — MacToolbox.DeviceBoundsRect.
        if (RenderGlobals.ColorQuickDrawAvailable != 0)
        {
            int updateRgn = MacToolbox.NewRgn();
            MacToolbox.RectRgn(updateRgn, MacToolbox.DeviceBoundsRect(PaletteState.GDevice));
            int frontWindow = MacToolbox.FrontWindow();
            int[] savedPort = new int[4];
            MacToolbox.GetPort(savedPort);
            MacToolbox.CalcVisBehind(frontWindow, updateRgn);
            MacToolbox.PaintBehind(frontWindow, updateRgn);
            MacToolbox.SetPort(savedPort[0]);
            MacToolbox.DisposeRgn(updateRgn);
            MacToolbox.DrawMenuBar();
        }
        return;
    }
}
