using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007ab40 (decompile 52491-52512) — cache the current screen device's pixmap bounds
// + depth into the render context: WindowBounds {top,left}/{bottom,right} (ctx+0x6a/+0x6e)
// and RenderMode (ctx+0x72). GlobalState.GDevice is the screen GDHandle; the toolbox
// accessor walks GDHandle -> gdPMap -> PixMap.
public static class CacheCurrentDeviceFields
{
    public static void Run()
    {
        MacToolbox.GetDevicePixMapFields(
            GlobalState.GDevice,
            out int boundsTopLeftPacked,
            out int boundsBotRightPacked,
            out short pixelSize);

        GlobalState.WindowBoundsTopLeftPacked = boundsTopLeftPacked;
        GlobalState.WindowBoundsBotRightPacked = boundsBotRightPacked;
        GlobalState.RenderMode = pixelSize;
    }
}
