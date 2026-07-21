using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007cc84 (EV Override-11.c lines 53655-53692): for each render node with an update
// UPP set, walks its prev chain (+0x32) firing both nodes' collision UPPs (+0x1e) on rect overlap,
// early-exiting once a prev node's +0x4c depth key drops more than +0x64 (cullExtent) below the
// outer node's.
// auStack_18 (byte[8]): passed to FUN_1007c324 as param_3 but never dereferenced by that function
// (decompile line 53286 uses only param_1/param_2) — drops out with the managed-node
// MacRectsOverlap overload.
public static class CollisionPass
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int outerNode = GlobalState.SpriteListHead; outerNode != 0; outerNode = SpriteNodes.At(outerNode).Next)
        {
            var outer = SpriteNodes.At(outerNode);
            if (outer.UpdateUpp != 0)
            {
                short depthKey = outer.SortKey;
                short cullExtent = GlobalState.SpriteLoopValue;
                int innerNode = outer.Prev;
                while (innerNode != 0)
                {
                    var inner = SpriteNodes.At(innerNode);
                    if (inner.UpdateUpp != 0 && EvMath.MacRectsOverlap(outer, inner))
                    {
                        if (outer.CollisionUpp != 0)
                        {
                            InvokeNodeCollisionUpp.Run(outer, inner, outer.CollisionUpp);
                        }
                        if (inner.CollisionUpp != 0)
                        {
                            InvokeNodeCollisionUpp.Run(inner, outer, inner.CollisionUpp);
                        }
                    }
                    innerNode = inner.Prev;
                    if (innerNode != 0 && SpriteNodes.At(innerNode).SortKey < (short)(depthKey - cullExtent))
                    {
                        innerNode = 0;
                    }
                }
            }
        }
    }
}
