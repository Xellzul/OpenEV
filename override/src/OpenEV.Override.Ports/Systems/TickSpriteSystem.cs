using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Systems;

// FUN_1007de58 — EV Override-11.c lines 54366-54393.
//
// Per-frame sprite-list pass: run each live node's update UPP, refresh its
// screen-edge rect from position + half-extents, depth-sort the list, run
// collision, then sweep nodes whose updater was cleared (the death path
// zeroes UpdateUpp) before ticking the sound subsystem.
public static class TickSpriteSystem
{
    public static void Run()
    {
        for (int node = GlobalState.SpriteListHead; node != 0;)
        {
            var n = SpriteNodes.At(node);
            int updateUpp = n.UpdateUpp;
            if (updateUpp != 0 && updateUpp != -1)
            {
                InvokeNodeUpdateUpp.Run(node, updateUpp);
            }
            n.RectTop = (short)(n.ExtentTop + n.PosY);
            n.RectBottom = (short)(n.ExtentBottom + n.PosY);
            n.RectLeft = (short)(n.ExtentLeft + n.PosX);
            n.RectRight = (short)(n.ExtentRight + n.PosX);
            node = n.Next;
        }
        SpriteListBubbleSortPass.Run();
        DispatchCollisionByAxes.Run();

        // NOTE: the decompile types this loop's list pointer
        // `undefined2 *`, so its literal indices (0x17/0xd/0xf) are SHORT-scaled
        // aliases for the byte offsets used below (0x2e Next, 0x1a UpdateUpp,
        // 0x1e CollisionUpp) — don't derive offsets by doubling those raw indices.
        int next = GlobalState.SpriteListHead;
        int current;
        while ((current = next) != 0)
        {
            var n = SpriteNodes.At(current);
            next = n.Next;
            if (n.UpdateUpp == 0)
            {
                n.CollisionUpp = 0;
                n.State = 0;
            }
        }
        TickSoundSubsystem.Run();
    }
}
