using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1006fac8 — checks whether the screen GDevice's current pixel depth / colour
// table still matches the saved palette-state record, and restores the saved
// palette if it drifted. Returns 1 when a deferred depth restore is still needed
// (the caller retries), 0 otherwise.
// Decompile: EV Override-11.c lines 45627-45663.
public static class ValidateAndRestoreDepth
{
    public static int Run()
    {
        if (RenderGlobals.ColorQuickDrawAvailable == 0)
        {
            return 0;
        }

        int gdHandle = PaletteState.GDevice;
        short savedDepth = PaletteState.DepthCheck;

        MacToolbox.GetDevicePixMapFields(gdHandle, out _, out _, out short currentDepth);
        int currentSeed = MacToolbox.ColorTableSeed(MacToolbox.DeviceColorTable(gdHandle));

        if (currentDepth == savedDepth && currentSeed == PaletteState.SavedSeed)
        {
            return 0;
        }

        int needsRestore = 0;
        if (currentDepth != savedDepth)
        {
            if (PaletteState.Flag0 == 0)
            {
                // The record doesn't own the device — defer the depth restore.
                needsRestore = 1;
            }
            // NO-OP: SetDepth is an unwired Toolbox stub, always returning 0, so this
            // condition is always false and needsRestore is never set via this path.
            else if ((short)MacToolbox.SetDepth(gdHandle, (int)savedDepth, 0, 0) != 0)
            {
                needsRestore = 1;
            }
        }

        if (needsRestore == 0)
        {
            Palette.RestorePaletteFromSaved(1);
        }

        return needsRestore;
    }
}
