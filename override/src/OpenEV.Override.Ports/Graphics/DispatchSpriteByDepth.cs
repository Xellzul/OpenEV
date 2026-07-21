using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007a7e0 (EV Override-11.c lines 52333-52354): install the primary GWorld
// port from a sprite FRAME record, dispatched on the render depth (ctx+0x72 = RenderMode).
public static class DispatchSpriteByDepth
{
    public static void Run(int spriteFrame)
    {
        var f = SpriteFrames.At(spriteFrame);
        short depth = GlobalState.RenderMode;
        // Depths 4 and 1 install *(*ColorRef) — a read THROUGH the colour header — where the
        // live default (RenderMode 0) installs ColorRef itself. Those deref arms are DEAD
        // under the pinned RenderMode 0, and ColorRef is a host texture KEY with no record
        // behind it, so tripwire rather than deref a key.
        if (depth == 4 || depth == 1)
        {
            throw new System.NotSupportedException(
                "DispatchSpriteByDepth: sub-8-bit depth (RenderMode pinning changed?) — re-derive the deref arms.");
        }
        GWorldPort.InstallPrimaryGWorldPort(
            f.ColorRef, f.BoundsTopLeftPacked, f.BoundsBotRightPacked);
    }
}
