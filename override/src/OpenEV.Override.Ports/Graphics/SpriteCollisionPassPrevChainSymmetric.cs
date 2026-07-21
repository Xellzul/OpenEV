using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d054 (EV Override-11.c lines 53804-53835). Like FullSquareSymmetric
// (FUN_1007cea8) but scans the BACKWARD chain (+0x32 = prev) instead of forward, with NO
// early cull and SYMMETRIC dispatch (both nodes' collision UPPs at +0x1e fire).
// MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class SpriteCollisionPassPrevChainSymmetric
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int spriteNode = GlobalState.SpriteListHead;
             spriteNode != 0;
             spriteNode = SpriteNodes.At(spriteNode).Next)
        {
            var sn = SpriteNodes.At(spriteNode);
            if (sn.UpdateUpp == 0)
                continue;

            for (int otherNode = sn.Prev;
                 otherNode != 0;
                 otherNode = SpriteNodes.At(otherNode).Prev)
            {
                var on = SpriteNodes.At(otherNode);
                if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                {
                    if (sn.CollisionUpp != 0)
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    if (on.CollisionUpp != 0)
                        InvokeNodeCollisionUpp.Run(on, sn, on.CollisionUpp);
                }
            }
        }
    }
}
