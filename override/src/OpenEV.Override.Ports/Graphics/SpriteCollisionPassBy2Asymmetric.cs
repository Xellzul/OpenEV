using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d128 (EV Override-11.c lines 53836-53884). Per-frame sprite-collision
// pass — same shape as SpriteCollisionPassBothChainsAsymmetric (FUN_1007d3c8) but with an
// EARLY CULL: the render list is sorted by the node's +2 coordinate, so each chain scan
// stops once a node's +2 passes the sprite's key ± the window's cull extent (window+0x64).
// MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class SpriteCollisionPassBy2Asymmetric
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

            short nodeKey = sn.PosY;
            short cullExtent = GlobalState.SpriteLoopValue;

            // Forward chain (next links), stop once past nodeKey + cullExtent.
            int otherNode = sn.Next;
            if (sn.CollisionUpp != 0)
            {
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    otherNode = on.Next;
                    if (otherNode != 0 && (short)(nodeKey + cullExtent) < SpriteNodes.At(otherNode).PosY)
                        otherNode = 0;
                }
            }

            // Backward chain (prev links), stop once past nodeKey - cullExtent.
            otherNode = sn.Prev;
            if (sn.CollisionUpp != 0)
            {
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    otherNode = on.Prev;
                    if (otherNode != 0 && SpriteNodes.At(otherNode).PosY < (short)(nodeKey - cullExtent))
                        otherNode = 0;
                }
            }
        }
    }
}
