using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10077e90 (decompile 50843-50871) — unlink one render node from the draw list
// (next +0x2e / prev +0x32, list head at window+0x78). If the node has no teardown UPP
// (+0x22) push it onto the free list (window+0x110); otherwise dispatch its teardown UPP.
public static class UnlinkGWorldNode
{
    public static void Run(int node)
    {
        var n = SpriteNodes.At(node);

        if (n.Next != 0)
            SpriteNodes.At(n.Next).Prev = n.Prev;   // next.prev = this.prev
        if (n.Prev == 0)
            GlobalState.SpriteListHead = n.Next;     // no prev -> this was the head
        else
            SpriteNodes.At(n.Prev).Next = n.Next;    // prev.next = this.next

        if (n.TeardownUpp == 0)
        {
            // No teardown UPP: push onto the free list (chained through +0x2e; all other
            // fields keep their stale values — Mac parity).
            n.Next = GlobalState.SpriteFreeListHead;
            GlobalState.SpriteFreeListHead = node;
        }
        else
        {
            InvokeNodeUpdateUpp.Run(node, n.TeardownUpp);
        }
    }
}
