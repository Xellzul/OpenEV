using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1007ab1c (decompile 52480-52485) — set the active QuickDraw port + GDevice to the
// in-game render context's pair (ctx+0 / ctx+4). The pair lives in GlobalState.
public static class SetGamePortAndDevice
{
    public static void Run()
    {
        SetPortAndDevice.Run(GlobalState.ActivePortPixmap, GlobalState.GDevice);
    }
}
