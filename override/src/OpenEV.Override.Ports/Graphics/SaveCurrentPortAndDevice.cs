using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007aa08 (decompile 52424-52440) — return the saved {port, GDevice}. Restore with
// SetPortAndDevice.Run(port, gdevice). The original wrote a 12-byte stack record.
public static class SaveCurrentPortAndDevice
{
    public static void Run(out int port, out int gdevice)
    {
        if (ResourceGlobals.ToolboxShimInitFlag == 0)
            InitToolboxShimGlobals.Run();
        port = MacToolbox.GetPort();
        gdevice = MacToolbox.GetGDevice();
    }
}
