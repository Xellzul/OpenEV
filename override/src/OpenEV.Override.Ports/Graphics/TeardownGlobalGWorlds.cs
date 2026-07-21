using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10077fa0 (decompile 50891-50916) — tear down the global in-game GWorlds/ports.
// Clears the toolbox-shim init flag, disposes the two main pixmaps (+0x8a/+0xa4), closes+frees
// the off-window sprite port (+0xbe), disposes the two GWorld records (+0x1e/+0x38) and the
// offscreen GWorld (+0x82/+0x86), then unlinks every remaining render node (+0x78 list).
public static class TeardownGlobalGWorlds
{
    public static void Run()
    {
        ResourceGlobals.ToolboxShimInitFlag = 0;   // clears the node-system "initialized" flag
        ZeroPixMapBaseAndDispose.Run(ref GlobalState.ComposeScratchPort, ref GlobalState.ComposeScratchGDevice, ref GlobalState.ComposeScratchRowTable);
        ZeroPixMapBaseAndDispose.Run(ref GlobalState.SecondaryGWorldPort, ref GlobalState.SecondaryGWorldGDevice, ref GlobalState.SecondaryGWorldRowTable);
        MacToolbox.ClosePort(GlobalState.SpriteGWorldPort);     // ctx+0xbe
        MacToolbox.DisposePtr(GlobalState.SpriteGWorldPort);
        GlobalState.SpriteGWorldPort = 0;
        DisposeGWorldRecord.Run(ref GlobalState.OffscreenGameGWorld, ref GlobalState.OffscreenGameGDevice, ref GlobalState.OffscreenGameRowTable);
        DisposeGWorldRecord.Run(ref GlobalState.AnimScratchPort, ref GlobalState.AnimScratchGDevice, ref GlobalState.AnimScratchRowTable);
        if (GlobalState.OffscreenGWorldA != 0)
            DisposeOffscreenGWorld.Run(GlobalState.OffscreenGWorldA, GlobalState.OffscreenGWorldADevice);
        GlobalState.OffscreenGWorldA = 0;
        while (GlobalState.SpriteListHead != 0)
            UnlinkGWorldNode.Run(GlobalState.SpriteListHead);
    }
}
