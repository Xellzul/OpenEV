using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007d640 (EV Override-11.c lines 54076-54181): one bubble-sort pass over the
// render-node doubly-linked list (next +0x2e / prev +0x32), keeping it ordered by the active
// sort key. The decompile open-codes the same forward+backward pass twice (once per key);
// here it is one helper parameterised by the key offset. window+0x78 is the list head.
public static class SpriteListBubbleSortPass
{
    public static void Run()
    {
        if (GlobalState.SpriteListHead == 0)
            return;
        // Selector is +0x60 (SpriteLoopStart, NOT +0x62): 0 = sort by scan-coord (+2),
        // 1 = by depth (+0x4c), 2 (or any other) = no sort. In-game config is Start=1 → depth.
        short sortMode = GlobalState.SpriteLoopStart;
        if (sortMode == 0)
            BubbleSort(2);        // sort by +2 (scan coordinate)
        else if (sortMode == 1)
            BubbleSort(0x4c);     // sort by +0x4c (depth)
    }

    // One forward pass (next links) then one backward pass (prev links), swapping adjacent
    // out-of-order nodes by the short key at curNode+keyOff (2 = PosY, 0x4c = SortKey). Keeps
    // GlobalState.SpriteListHead (window+0x78) updated.
    private static void BubbleSort(int keyOff)
    {
        int curNode, lastNode = 0;

        int nextNode = SpriteNodes.At(GlobalState.SpriteListHead).Next;
        while ((curNode = nextNode) != 0)
        {
            var cur = SpriteNodes.At(curNode);
            nextNode = cur.Next;
            lastNode = curNode;
            if (cur.ShortAt(keyOff) < SpriteNodes.At(cur.Prev).ShortAt(keyOff))
            {
                int swapNode = cur.Prev;
                var swap = SpriteNodes.At(swapNode);
                if (swap.Prev != 0)
                    SpriteNodes.At(swap.Prev).Next = curNode;
                if (cur.Next != 0)
                    SpriteNodes.At(cur.Next).Prev = swapNode;
                cur.Prev = swap.Prev;
                swap.Next = cur.Next;
                cur.Next = swapNode;
                swap.Prev = curNode;
                if (swapNode == GlobalState.SpriteListHead)
                    GlobalState.SpriteListHead = curNode;
            }
        }

        nextNode = lastNode != 0 ? SpriteNodes.At(lastNode).Prev : 0;
        while ((curNode = nextNode) != 0)
        {
            var cur = SpriteNodes.At(curNode);
            nextNode = cur.Prev;
            if (SpriteNodes.At(cur.Next).ShortAt(keyOff) < cur.ShortAt(keyOff))
            {
                int swapNode = cur.Next;
                var swap = SpriteNodes.At(swapNode);
                if (swap.Next != 0)
                    SpriteNodes.At(swap.Next).Prev = curNode;
                if (cur.Prev != 0)
                    SpriteNodes.At(cur.Prev).Next = swapNode;
                cur.Next = swap.Next;
                swap.Prev = cur.Prev;
                cur.Prev = swapNode;
                swap.Next = curNode;
                if (curNode == GlobalState.SpriteListHead)
                    GlobalState.SpriteListHead = swapNode;
            }
        }
    }
}
