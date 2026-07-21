using System.Collections.Generic;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics.Model;

// ONE sprite-render-list node as a managed C# object — the render list headed by
// GlobalState.SpriteListHead (window+0x78), doubly linked via +0x2e (next) /
// +0x32 (prev), allocated by AddSpriteRenderNode (FUN_1007c06c) and recycled
// through GlobalState.SpriteFreeListHead (window+0x110) by UnlinkGWorldNode.
//
// Representation: a big-endian byte[] mirroring the Mac heap block, with named
// accessors for every known offset. Links and the free-list chain still store
// int HANDLES inside Data, so list traversal/ordering semantics (including the
// stale-field reuse of free-listed nodes) are byte-for-byte faithful.
//
// Capacity is 0x100 (the Mac allocation is 0x80; all real fields stay within
// +0x58..0x5b). The extra headroom is a cheap safety margin until every
// node-writer is audited.
public sealed class SpriteNode
{
    public const int Capacity = 0x100;

    public readonly int Handle;
    public readonly byte[] Data = new byte[Capacity];

    internal SpriteNode(int handle) => Handle = handle;

    // Big-endian accessors over Data (same packing as EvoMemory).
    public short ShortAt(int off) => BigEndian.ReadInt16(Data, off);
    public ushort UShortAt(int off) => BigEndian.ReadUInt16(Data, off);
    public int IntAt(int off) => BigEndian.ReadInt32(Data, off);
    public void SetShort(int off, short v) => BigEndian.WriteInt16(Data, off, v);
    public void SetInt(int off, int v) => BigEndian.WriteInt32(Data, off, v);

    public short State { get => ShortAt(0x00); set => SetShort(0x00, value); }   // +0x00 state/flag (zeroed when the updater is cleared)
    public short PosY { get => ShortAt(0x02); set => SetShort(0x02, value); }    // +0x02 screen Y
    public short PosX { get => ShortAt(0x04); set => SetShort(0x04, value); }    // +0x04 screen X

    // Half-extents the per-frame tick derives the screen rect from (TickSpriteSystem).
    public short ExtentTop { get => ShortAt(0x06); set => SetShort(0x06, value); }    // +0x06
    public short ExtentLeft { get => ShortAt(0x08); set => SetShort(0x08, value); }   // +0x08
    public short ExtentBottom { get => ShortAt(0x0a); set => SetShort(0x0a, value); } // +0x0a
    public short ExtentRight { get => ShortAt(0x0c); set => SetShort(0x0c, value); }  // +0x0c

    // Screen-space bounds Rect (+0xe..+0x14): the rect collision passes overlap-test it,
    // AddSpriteRenderNode offsets it by (PosX, PosY).
    public short RectTop { get => ShortAt(0x0e); set => SetShort(0x0e, value); }     // +0x0e
    public short RectLeft { get => ShortAt(0x10); set => SetShort(0x10, value); }    // +0x10
    public short RectBottom { get => ShortAt(0x12); set => SetShort(0x12, value); }  // +0x12
    public short RectRight { get => ShortAt(0x14); set => SetShort(0x14, value); }   // +0x14

    public int SpritePtr { get => IntAt(0x16); set => SetInt(0x16, value); }     // +0x16 sprite-frame handle
    public int UpdateUpp { get => IntAt(0x1a); set => SetInt(0x1a, value); }     // +0x1a per-frame update UPP (InvokeNodeUpdateUpp sentinel)
    public int CollisionUpp { get => IntAt(0x1e); set => SetInt(0x1e, value); }  // +0x1e collision UPP (InvokeNodeCollisionUpp sentinel)
    public int TeardownUpp { get => IntAt(0x22); set => SetInt(0x22, value); }   // +0x22 teardown UPP (dispatched by UnlinkGWorldNode)
    public int ClipRgn { get => IntAt(0x26); set => SetInt(0x26, value); }       // +0x26 per-node clip region handle (BlitSpriteByDepth SectRgn)
    public int PosPackedSnapshot { get => IntAt(0x2a); set => SetInt(0x2a, value); } // +0x2a packed (PosY<<16|PosX) snapshot
    public int Next { get => IntAt(0x2e); set => SetInt(0x2e, value); }          // +0x2e next node handle (also the free-list chain)
    public int Prev { get => IntAt(0x32); set => SetInt(0x32, value); }          // +0x32 prev node handle

    // Draw Rect (+0x36..+0x3c) and its previous-frame snapshot (+0x3e..+0x44),
    // copied as two ints each by UpdateWindowRegionLayout.
    public short DrawRectTop { get => ShortAt(0x36); set => SetShort(0x36, value); }     // +0x36
    public short DrawRectLeft { get => ShortAt(0x38); set => SetShort(0x38, value); }    // +0x38
    public short DrawRectBottom { get => ShortAt(0x3a); set => SetShort(0x3a, value); }  // +0x3a
    public short DrawRectRight { get => ShortAt(0x3c); set => SetShort(0x3c, value); }   // +0x3c
    public short PrevRectTop { get => ShortAt(0x3e); set => SetShort(0x3e, value); }     // +0x3e
    public short PrevRectLeft { get => ShortAt(0x40); set => SetShort(0x40, value); }    // +0x40
    public short PrevRectBottom { get => ShortAt(0x42); set => SetShort(0x42, value); }  // +0x42
    public short PrevRectRight { get => ShortAt(0x44); set => SetShort(0x44, value); }   // +0x44

    public int Field46 { get => IntAt(0x46); set => SetInt(0x46, value); }   // +0x46 (zeroed on alloc; no other usage yet)
    public short SortKey { get => ShortAt(0x4c); set => SetShort(0x4c, value); }   // +0x4c depth/priority sort key (SpriteListBubbleSortPass)
    public short SpawnPosY { get => ShortAt(0x4e); set => SetShort(0x4e, value); } // +0x4e spawn/world Y (explosion/streak spawners)
    public short SpawnPosX { get => ShortAt(0x50); set => SetShort(0x50, value); } // +0x50 spawn/world X
    public short UpdaterFlag { get => ShortAt(0x52); set => SetShort(0x52, value); }   // +0x52 state/death flag (-1 gates HandleProjectileDeath)
    public int ObjectPtr { get => IntAt(0x54); set => SetInt(0x54, value); }       // +0x54 ship/spob/object record address (raw EvoMemory ptr)
    public int UpdaterPayload { get => IntAt(0x58); set => SetInt(0x58, value); }   // +0x58 per-updater payload (tractor corner index, etc.)
}

// Registry mapping the int "node pointer" the ported code passes around (UPP
// dispatch args, free-list/link chains, escort-node globals 0x1008a738..44) to
// the managed object. Handles live at 0x68000000+ — a managed-only band, so a
// stale/foreign handle throws in At() instead of silently reading zeros: the
// migration tripwire for any un-converted raw pointer read.
public static class SpriteNodes
{
    public const int HandleBase = 0x68000000;
    private const int Stride = 0x100;

    private static readonly Dictionary<int, SpriteNode> _store = new();
    private static int _nextHandle = HandleBase;

    public static SpriteNode Register()
    {
        _nextHandle += Stride;
        var node = new SpriteNode(_nextHandle);
        _store[_nextHandle] = node;
        return node;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static SpriteNode At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);
}
