using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007974c (decompile 51748-51779) — rebuild the offscreen colour GWorld
// (OffscreenGWorldA +0x82 / +0x86): dispose the old one, clone an 8-bit colour table, and
// remap a new GWorld sized from the compose-scratch port's portRect (ctx+0x8a, +0x10/+0x14).
public static class RefreshOffscreenPixMap
{
    public static void Run()
    {
        if (GlobalState.OffscreenGWorldA != 0)
            DisposeOffscreenGWorld.Run(GlobalState.OffscreenGWorldA, GlobalState.OffscreenGWorldADevice);

        int colorTable = MacToolbox.GetCTable(8);
        if (colorTable == 0)
        {
            GlobalState.OffscreenGWorldA = 0;
            return;
        }
        MacToolbox.GetPortRect(GlobalState.ComposeScratchPort, out int boundsTopLeft, out int boundsBotRight);
        short remapResult = (short)NewOffscreenColorPort.Run(boundsTopLeft, boundsBotRight, 8, colorTable, out int outPort, out int outDevice);
        GlobalState.OffscreenGWorldA = outPort;
        GlobalState.OffscreenGWorldADevice = outDevice;
        if (remapResult != 0)
            GlobalState.OffscreenGWorldA = 0;
    }
}
