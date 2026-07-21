using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d278 (EV Override-11.c lines 53893-53943). Identical to
// SpriteCollisionPassBy2Asymmetric (FUN_1007d128) except the sweep-and-prune early cull
// keys on the node's +0x4c depth field (hence "By4c…Sweep") instead of the +2 coordinate:
// the chain scan stops once a node's +0x4c passes the sprite's depth ± the window cull
// extent (window+0x64). MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class SpriteCollisionPassBy4cAsymmetricSweep
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

            short depthKey = sn.SortKey;
            short cullExtent = GlobalState.SpriteLoopValue;

            // Forward chain (next links), stop once past depthKey + cullExtent.
            int otherNode = sn.Next;
            if (sn.CollisionUpp != 0)
            {
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    otherNode = on.Next;
                    if (otherNode != 0 && (short)(depthKey + cullExtent) < SpriteNodes.At(otherNode).SortKey)
                        otherNode = 0;
                }
            }

            // Backward chain (prev links), stop once past depthKey - cullExtent.
            otherNode = sn.Prev;
            if (sn.CollisionUpp != 0)
            {
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    otherNode = on.Prev;
                    if (otherNode != 0 && SpriteNodes.At(otherNode).SortKey < (short)(depthKey - cullExtent))
                        otherNode = 0;
                }
            }
        }
    }
}
