using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007a760 (EV Override-11.c lines 52314-52332): walk the rerender FRAME list
// (GlobalState.SpriteListHead2, ctx+0xc2) — the SpriteFrames records chained at +0x16
// (NextInList2) — and reinit/rerender each frame to the screen's current depth. The depth
// is the main GDevice's pixmap pixelSize (GDevice handle → record → gdPMap → pixmap →
// pixelSize +0x20, via GetDevicePixMapFields).
public static class RerenderAllSpritesForCurrentDepth
{
    public static void Run()
    {
        MacToolbox.GetDevicePixMapFields(GlobalState.GDevice, out _, out _, out short depth);
        for (int spriteFrame = GlobalState.SpriteListHead2; spriteFrame != 0;
             spriteFrame = SpriteFrames.At(spriteFrame).NextInList2)
        {
            SpriteReinitToDepth.Run(spriteFrame, GlobalState.RenderMode, depth);
            SpriteRerender.Run(spriteFrame, depth);
        }
    }
}
