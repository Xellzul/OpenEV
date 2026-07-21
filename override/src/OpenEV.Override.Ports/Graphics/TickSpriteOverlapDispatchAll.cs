using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c684 (EV Override-11.c lines 53409-53451): collision pass over the forward
// chain (+0x2e) with the +2 scan-coordinate sweep cull, ASYMMETRIC dispatch — fire once,
// preferring the outer node's +0x1e collision UPP, else the inner's. Unlike the sibling passes
// this one has NO update-UPP (+0x1a) gates: it overlap-tests every node. MacRectsOverlap =
// FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class TickSpriteOverlapDispatchAll
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int outerSprite = GlobalState.SpriteListHead;
             outerSprite != 0;
             outerSprite = SpriteNodes.At(outerSprite).Next)
        {
            var outer = SpriteNodes.At(outerSprite);
            short scanKey = outer.PosY;
            short cullExtent = GlobalState.SpriteLoopValue;
            int innerSprite = outer.Next;
            while (innerSprite != 0)
            {
                var inner = SpriteNodes.At(innerSprite);
                if (EvMath.MacRectsOverlap(outer, inner))
                {
                    if (outer.CollisionUpp == 0)
                    {
                        if (inner.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(inner, outer, inner.CollisionUpp);
                    }
                    else
                    {
                        InvokeNodeCollisionUpp.Run(outer, inner, outer.CollisionUpp);
                    }
                }
                innerSprite = inner.Next;
                if (innerSprite != 0 && (short)(scanKey + cullExtent) < SpriteNodes.At(innerSprite).PosY)
                    innerSprite = 0;
            }
        }
    }
}
