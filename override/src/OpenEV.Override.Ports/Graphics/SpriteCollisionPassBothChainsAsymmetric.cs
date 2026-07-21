using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d3c8 (EV Override-11.c lines 53950-53983). Per-frame sprite-collision
// pass: for every render node that has both an update UPP (+0x1a) and a collision UPP
// (+0x1e), test its bounding box (+0xe) against every other node reachable along the
// forward (+0x2e = next) AND backward (+0x32 = prev) links, and dispatch the collision
// handler (InvokeNodeCollisionUpp = FUN_1007e30c) on each overlap. MacRectsOverlap = FUN_1007c324.
public static class SpriteCollisionPassBothChainsAsymmetric
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

            // Forward chain (next links).
            int otherNode = sn.Next;
            if (sn.CollisionUpp != 0)
            {
                for (; otherNode != 0; otherNode = SpriteNodes.At(otherNode).Next)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                }
            }

            // Backward chain (prev links).
            otherNode = sn.Prev;
            if (sn.CollisionUpp != 0)
            {
                for (; otherNode != 0; otherNode = SpriteNodes.At(otherNode).Prev)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                }
            }
        }
    }
}
