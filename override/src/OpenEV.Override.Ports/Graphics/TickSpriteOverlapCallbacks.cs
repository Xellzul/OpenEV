using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c57c (EV Override-11.c lines 53366-53408): collision pass over the forward
// chain (+0x2e) with the +2 scan-coordinate sweep cull, SYMMETRIC dispatch (both nodes' +0x1e
// collision UPPs fire), no collision marking. MacRectsOverlap = FUN_1007c324; InvokeNodeCollisionUpp =
// FUN_1007e30c.
public static class TickSpriteOverlapCallbacks
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

            short scanKey = outer.PosY;
            short cullExtent = GlobalState.SpriteLoopValue;
            int innerSprite = outer.Next;
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
                innerSprite = inner.Next;
                if (innerSprite != 0 && (short)(scanKey + cullExtent) < SpriteNodes.At(innerSprite).PosY)
                    innerSprite = 0;
            }
        }
    }
}
