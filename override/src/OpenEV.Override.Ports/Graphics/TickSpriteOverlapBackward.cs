using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c778 (EV Override-11.c lines 53452-53494): like TickSpriteOverlapCallbacks but
// scans the BACKWARD chain (+0x32 = prev) with the +2 scan-coordinate sweep cull, SYMMETRIC
// dispatch, no collision marking. MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp = FUN_1007e30c.
public static class TickSpriteOverlapBackward
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int outerSprite = GlobalState.SpriteListHead;
             outerSprite != 0;
             outerSprite = SpriteNodes.At(outerSprite).Next)
        {
            var outer = SpriteNodes.At(outerSprite);
            if (outer.UpdateUpp == 0)
                continue;

            short cullExtent = GlobalState.SpriteLoopValue;
            short scanKey = outer.PosY;
            int innerSprite = outer.Prev;
            while (innerSprite != 0)
            {
                var inner = SpriteNodes.At(innerSprite);
                if (inner.UpdateUpp != 0 && EvMath.MacRectsOverlap(outer, inner))
                {
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
    }
}
