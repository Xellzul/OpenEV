using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c880 (EV Override-11.c lines 53495-53566). The full collision-RESOLUTION
// pass: for each render node whose state (+0) is > 0, scan BOTH chains (forward +0x2e and
// backward +0x32) with the +0x4c depth-key sweep cull, and for every overlapping node whose
// state (+0) is < 0, mark the pair as collided (this node +0 = 10, other +0 = -10) and fire
// both nodes' collision UPPs (+0x1e) SYMMETRICALLY. After each node, OR its "dead" state
// (+0 < -1) into the window's collision flag (window+0x80).
// MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
// (The decompile indexes the node as short*, so its [N] fields are byte offset N*2 here.)
public static class SpriteCollisionPassByZ
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int spriteNode = GlobalState.SpriteListHead;
             spriteNode != 0;
             spriteNode = SpriteNodes.At(spriteNode).Next)
        {
            var sn = SpriteNodes.At(spriteNode);
            if (sn.UpdateUpp != 0 && 0 < sn.State)
            {
                short depthKey = sn.SortKey;
                short cullExtent = GlobalState.SpriteLoopValue;

                // Forward chain (next links), stop once past depthKey + cullExtent.
                int otherNode = sn.Next;
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && on.State < 0 && EvMath.MacRectsOverlap(sn, on))
                    {
                        sn.State = 10;
                        on.State = -10;
                        if (sn.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                        if (on.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(on, sn, on.CollisionUpp);
                    }
                    otherNode = on.Next;
                    if (otherNode != 0 && (short)(depthKey + cullExtent) < SpriteNodes.At(otherNode).SortKey)
                        otherNode = 0;
                }

                // Backward chain (prev links), stop once past depthKey - cullExtent.
                otherNode = sn.Prev;
                while (otherNode != 0)
                {
                    var on = SpriteNodes.At(otherNode);
                    if (on.UpdateUpp != 0 && on.State < 0 && EvMath.MacRectsOverlap(sn, on))
                    {
                        sn.State = 10;
                        on.State = -10;
                        if (sn.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(sn, on, sn.CollisionUpp);
                        if (on.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(on, sn, on.CollisionUpp);
                    }
                    otherNode = on.Prev;
                    if (otherNode != 0 && SpriteNodes.At(otherNode).SortKey < (short)(depthKey - cullExtent))
                        otherNode = 0;
                }
            }

            // OR this node's "dead" state (+0 < -1) into the window collision flag.
            GlobalState.SpriteListLock = (byte)(GlobalState.SpriteListLock | (sn.State < -1 ? 1 : 0));
        }
    }
}
