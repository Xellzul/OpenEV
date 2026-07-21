using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.GalaxyMap;

// Port of FUN_10033fac (EV Override-11.c lines 21270-21304).
//
// Scroll the galaxy-map area (map dialog item 3) by the drag delta and redraw —
// the sole caller is RunGalaxyMapDialog's drag-pan.
public static class ScrollGalaxyMapArea
{
    public static void Run(short scrollDh, short scrollDv)
    {
        // The map-area Rect (GetDialogItem rectOut for item 3), inset by
        // (1,1) field-by-field as the decompile does, then handed to ScrollRect.
        short[] areaRect = new short[4];

        MacToolbox.SetPort(GalaxyMapState.MapDialog);
        MacToolbox.GetDialogItem(GalaxyMapState.MapDialog, 3, 0, 0, areaRect);
        areaRect[1] += 1;   // left
        areaRect[0] += 1;   // top
        areaRect[3] -= 1;   // right
        areaRect[2] -= 1;   // bottom

        // BackPat is an unseeded no-op stub, so the Pattern-field arguments the
        // decompile passes here — QD-globals +0xba = qd.black, +0xc2 = qd.white
        // (arrow @+0x5e / thePort @+0xca fix the struct base) — collapse to a bare 0.
        // ScrollRect below can't read the black bkPat, so it fills its vacated strip
        // with black directly (see MacToolbox.ScrollRect).
        MacToolbox.BackPat(0);
        MacToolbox.ScrollRect(areaRect, scrollDh, scrollDv, GalaxyMapState.UpdateRgn);
        MacToolbox.BackPat(0);
        GalaxyMapState.ScrollInProgress = 1;
        DrawGalaxyMap.Run();
        GalaxyMapState.ScrollInProgress = 0;
    }
}
