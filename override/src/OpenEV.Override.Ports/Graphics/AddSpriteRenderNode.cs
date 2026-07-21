using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c06c (EV Override-11.c lines 53178-53260): the add-render-node
// primitive. Allocates (or recycles) a sprite-render node, initialises it, links it
// into the window's draw list (GlobalState.SpriteListHead, window+0x78), and returns
// its handle. Nodes are managed SpriteNode objects (byte[]-backed) behind int handles;
// the Mac sized the block from *PTR_DAT_100812c8, replaced here by SpriteNode.Capacity,
// so the size read is dropped.
public static class AddSpriteRenderNode
{
    // reuseNode: existing node to reuse (0 = alloc new).
    public static int Run(int insertAfterNode, int reuseNode, int initState, int initPosX, int initPosY, int spriteHandle)
    {
        // Lazy toolbox-shim init (same guard as BlitSpriteToWindow/SpriteAllocateImpl).
        if (Resource.ResourceGlobals.ToolboxShimInitFlag == 0)
            Misc.InitToolboxShimGlobals.Run();

        SpriteNode n;
        if (reuseNode == 0)
        {
            if (GlobalState.SpriteFreeListHead == 0)
            {
                n = SpriteNodes.Register();            // zeroed byte[] = NewPtrClear parity
                n.UpdateUpp = -1;                      // 0xffffffff sentinel
            }
            else
            {
                n = SpriteNodes.At(GlobalState.SpriteFreeListHead);   // pop free list
                GlobalState.SpriteFreeListHead = n.Next;
                // Recycled nodes are only PARTIALLY re-cleared — every other field keeps
                // its stale previous-owner value (Mac parity).
                n.UpdateUpp = -1;                      // 0xffffffff sentinel
                n.CollisionUpp = 0;
                n.TeardownUpp = 0;
                n.SpritePtr = 0;
                n.Field46 = 0;
                n.SetInt(0x3e, 0);   // SetRect prev-frame rect = 0 (+0x3e..+0x44)
                n.SetInt(0x42, 0);
                n.SetInt(0x36, 0);   // SetRect draw rect = 0 (+0x36..+0x3c)
                n.SetInt(0x3a, 0);
                n.PosPackedSnapshot = 0;
                n.ClipRgn = 0;
            }
        }
        else
        {
            n = SpriteNodes.At(reuseNode);
            n.UpdateUpp = -1;                          // 0xffffffff sentinel
            n.CollisionUpp = 0;
            n.TeardownUpp = 0;
            n.SpritePtr = 0;
            n.Field46 = 0;
            n.SetInt(0x3e, 0);   // SetRect prev-frame rect = 0 (+0x3e..+0x44)
            n.SetInt(0x42, 0);
            n.SetInt(0x36, 0);   // SetRect draw rect = 0 (+0x36..+0x3c)
            n.SetInt(0x3a, 0);
            n.PosPackedSnapshot = 0;
            n.ClipRgn = 0;
        }

        int node = n.Handle;
        n.State = (short)initState;
        n.PosX = (short)initPosX;
        n.PosY = (short)initPosY;
        n.PosPackedSnapshot = n.IntAt(2);   // packed PosY<<16|PosX
        if (insertAfterNode == 0)
        {
            n.Next = GlobalState.SpriteListHead;   // next = old head
            n.Prev = 0;
            GlobalState.SpriteListHead = node;     // head = node
            if (n.Next != 0)
                SpriteNodes.At(n.Next).Prev = node;   // old-head prev = node
        }
        else
        {
            var after = SpriteNodes.At(insertAfterNode);
            n.Next = after.Next;
            n.Prev = insertAfterNode;
            after.Next = node;
            if (n.Next != 0)
                SpriteNodes.At(n.Next).Prev = node;
        }

        if (spriteHandle != 0)
        {
            Misc.InvokeNodeUpdateUpp.Run(node, spriteHandle);
            n.SetInt(0x0e, n.IntAt(0x06));   // rect top/left ← extents (+0x0e ← +0x06)
            n.SetInt(0x12, n.IntAt(0x0a));   // rect bottom/right ← extents (+0x12 ← +0x0a)
            // OffsetRect(rect, dx = PosX, dy = PosY)
            n.RectTop += n.PosY;
            n.RectBottom += n.PosY;
            n.RectLeft += n.PosX;
            n.RectRight += n.PosX;
        }
        return node;
    }
}
