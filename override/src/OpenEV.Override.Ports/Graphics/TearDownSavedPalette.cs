using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1006f99c (EV Override-11.c 45574-45596) — tear down the saved palette / GDevice-depth
// state: restore the GWorld palette, optionally restore the original screen depth, repaint,
// then free the record's two CTabHandles. The record is now the managed PaletteState object
// (was the heap struct *PaletteStateRecordSlot); the trailing DisposePtr(rec) drops (GC-owned).
public static class TearDownSavedPalette
{
    public static void Run()
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return;

        Palette.RestoreGWorldPalette(1);
        if (PaletteState.Flag0 != 0 && PaletteState.SavedDepth != 0)
            MacToolbox.SetDepth(PaletteState.GDevice, PaletteState.SavedDepth, 0, 0);
        Palette.RepaintBehindFrontWindow();
        MacToolbox.DisposeHandle(PaletteState.SnapshotCTab);
        MacToolbox.DisposeHandle(PaletteState.SavedCTab);
    }
}
