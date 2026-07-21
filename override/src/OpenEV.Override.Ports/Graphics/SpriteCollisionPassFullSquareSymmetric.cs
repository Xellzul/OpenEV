using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007cea8 (EV Override-11.c lines 53738-53769). Per-frame sprite-collision
// pass: full forward (+0x2e) scan with NO early cull, SYMMETRIC dispatch — on each overlap,
// fire the collision handler (InvokeNodeCollisionUpp = FUN_1007e30c) for both nodes that have a
// collision UPP (+0x1e). MacRectsOverlap = FUN_1007c324.
public static class SpriteCollisionPassFullSquareSymmetric
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

            for (int otherNode = sn.Next;
                 otherNode != 0;
                 otherNode = SpriteNodes.At(otherNode).Next)
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
