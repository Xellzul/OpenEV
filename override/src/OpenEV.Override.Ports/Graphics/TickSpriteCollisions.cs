using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c38c (EV Override-11.c lines 53294-53365): the collision-RESOLUTION pass keyed
// on the +2 scan coordinate (cf. SpriteCollisionPassByZ, which is the identical pass keyed on the
// +0x4c depth). For each node whose state (+0) is > 0, scan both chains (forward +0x2e / backward
// +0x32) with the +2 sweep cull, and for each overlapping node whose state (+0) is < 0, mark the
// pair collided (this +0 = 10, other +0 = -10) and fire both collision UPPs (+0x1e) symmetrically.
// After each node, OR its "dead" state (+0 < -1) into window+0x80.
// MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class TickSpriteCollisions
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int outerSprite = GlobalState.SpriteListHead;
             outerSprite != 0;
             outerSprite = SpriteNodes.At(outerSprite).Next)
        {
            var outer = SpriteNodes.At(outerSprite);
            if (outer.UpdateUpp != 0 && 0 < outer.State)
            {
                short scanKey = outer.PosY;
                short cullExtent = GlobalState.SpriteLoopValue;

                // Forward chain (next links), stop once past scanKey + cullExtent.
                int innerSprite = outer.Next;
                while (innerSprite != 0)
                {
                    var inner = SpriteNodes.At(innerSprite);
                    if (inner.UpdateUpp != 0 && inner.State < 0 && EvMath.MacRectsOverlap(outer, inner))
                    {
                        outer.State = 10;
                        inner.State = -10;
                        if (outer.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(outer, inner, outer.CollisionUpp);
                        if (inner.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(inner, outer, inner.CollisionUpp);
                    }
                    innerSprite = inner.Next;
                    if (innerSprite != 0 && (short)(scanKey + cullExtent) < SpriteNodes.At(innerSprite).PosY)
                        innerSprite = 0;
                }

                // Backward chain (prev links), stop once past scanKey - cullExtent.
                innerSprite = outer.Prev;
                while (innerSprite != 0)
                {
                    var inner = SpriteNodes.At(innerSprite);
                    if (inner.UpdateUpp != 0 && inner.State < 0 && EvMath.MacRectsOverlap(outer, inner))
                    {
                        outer.State = 10;
                        inner.State = -10;
                        if (outer.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(outer, inner, outer.CollisionUpp);
                        if (inner.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(inner, outer, inner.CollisionUpp);
                    }
                    innerSprite = inner.Prev;
                    if (innerSprite != 0 && SpriteNodes.At(innerSprite).PosY < (short)(scanKey - cullExtent))
                        innerSprite = 0;
                }
            }

            GlobalState.SpriteListLock = (byte)(GlobalState.SpriteListLock | (outer.State < -1 ? 1 : 0));
        }
    }
}
