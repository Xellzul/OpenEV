using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1006fa70 (EV Override-11.c lines 45609-45626): when ColorQuickDraw is available,
// validate/restore the screen depth, restore the saved palette, and repaint behind the front
// window. Returns the depth-validation result (0 when ColorQuickDraw is unavailable).
public static class ValidateAndResyncDisplay
{
    public static int Run()
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
            return 0;

        int result = ValidateAndRestoreDepth.Run();
        Palette.RestorePaletteFromSaved(1);
        PaintBehindFrontWindow.Run();
        return result;
    }
}
