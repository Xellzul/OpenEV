using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007aa68 (decompile 52441-52457) — make a {port, GDevice} pair the active QuickDraw
// target; lazily inits the render window first while the toolbox-shim flag is 0 (dormant in the
// port; see InitToolboxShimGlobals). The original took a pointer to the {port, gdevice} record;
// the pair now lives in GlobalState, so the pair is passed by value.
public static class SetPortAndDevice
{
    public static void Run(int port, int gdevice)
    {
        if (ResourceGlobals.ToolboxShimInitFlag == 0)
            InitToolboxShimGlobals.Run();
        MacToolbox.SetPort(port);
        if (gdevice != 0)
            MacToolbox.SetGDevice(gdevice);
    }
}
