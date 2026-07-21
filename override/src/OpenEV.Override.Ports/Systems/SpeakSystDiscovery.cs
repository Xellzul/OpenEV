using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_1005d0d0 (EV Override-11.c lines 38510-38529): speaks the
// system-discovery chatter line for systIndex — 'STR ' resource id systIndex+999,
// falling back to STR# resource 1000 entry systIndex when that resource is absent.
public static class SpeakSystDiscovery
{
    public static void Run(int systIndex)
    {
        string? discovery = TryLoadStr.RunString((short)(systIndex + 999));
        if (discovery == null)
        {
            discovery = MacToolbox.GetIndString(1000, (short)systIndex);
        }
        EnqueueChatterEvent.Run(discovery, 480, 0, 12, UiColors.ChatterText, 0, 0);
    }
}
