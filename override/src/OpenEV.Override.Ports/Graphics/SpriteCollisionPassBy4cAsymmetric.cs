using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007cb78 (EV Override-11.c lines 53610-53654). Forward (+0x2e) scan with the
// sweep-and-prune early cull on the +0x4c depth key, ASYMMETRIC dispatch — fire the collision
// handler (InvokeNodeCollisionUpp = FUN_1007e30c) once, preferring the sprite's own +0x1e collision
// UPP, else the other node's. MacRectsOverlap = FUN_1007c324.
public static class SpriteCollisionPassBy4cAsymmetric
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

            int otherNode = sn.Next;
            while (otherNode != 0)
            {
                var on = SpriteNodes.At(otherNode);
                if (on.UpdateUpp != 0 && EvMath.MacRectsOverlap(sn, on))
                {
                    if (sn.CollisionUpp == 0)
                    {
                        if (on.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(on, sn, on.CollisionUpp);
                    }
                    else
                    {
                        InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                    }
                }
                otherNode = on.Next;
                if (otherNode != 0 && (short)(depthKey + cullExtent) < SpriteNodes.At(otherNode).SortKey)
                    otherNode = 0;
            }
        }
    }
}
