using System.Collections.Generic;

namespace OpenEV.Override.Ports.Graphics.Model;

// ONE sprite-frame record as a managed C# object — the per-rotation-frame
// records whose ADDRESSES fill the managed frame-pointer tables
// (Combat.Model.CombatGraphicsTables: WeaponGraphicsTable ship frames, WeaponDefTable,
// ExplosionGraphicsTable, PlanetSpriteRecordTable, SpriteFrameTables.*).
//
// Layout per the decompiled blitter FUN_100779c8 (EV Override-11.c:50708),
// addressed as `int *param_1`. ColorRef (+0x00) is the host ResolveTexture KEY:
// CopyMask/CopyBits resolve textures by key, never by reading pixel memory, so it
// flows straight through as CopyMask's srcBits. The embedded mask BitMap (+0x06)
// is passed by address to CopyMask but ignored by the host (sprite textures carry
// their own alpha) — kept for fidelity. Field offsets are named below.
public sealed class SpriteFrame
{
    public readonly int Handle;

    public int ColorRef;          // +0x00 — host texture key (original: colour cell pixel addr)
    public short CIconId;         // +0x04 — detached-cicn resource id (SpriteRerender's LoadDetachedCIcon)
    public int MaskBase;          // +0x06 — mask BitMap baseAddr (unused by the host — alpha masks)
    public short MaskRowBytes;    // +0x0a
    public short BoundsTop, BoundsLeft, BoundsBottom, BoundsRight;   // +0x0c..+0x12 — frame rect {0,0,h,w}
    public short ColorRowBytes;   // +0x14 — |0x8000 → compose-pixmap rowBytes
    public int NextInList2;       // +0x16 — next frame in the ctx+0xc2 rerender list (SpriteListHead2)
    public int MaskRgn;           // +0x1a — MacRegions handle (0 = CopyMask path)
    public int ColorRowTable;     // +0x1e — colour-buffer row table (BuildSpriteScaleTable)
    public int MaskRowTable;      // +0x22 — mask row table (BuildSpriteScaleTable)
    public int RerenderUpp;       // +0x26 — rerender-pass UPP (SpriteRerender dispatches it)
    public int CustomDrawUpp;     // +0x2a — custom blit proc (UpdateWindowRegionLayout/Blit* dispatch it instead of the depth blitter)

    internal SpriteFrame(int handle) => Handle = handle;

    public short Width => (short)(BoundsRight - BoundsLeft);
    public short Height => (short)(BoundsBottom - BoundsTop);
    public int BoundsTopLeftPacked => (BoundsTop << 16) | (ushort)BoundsLeft;
    public int BoundsBotRightPacked => (BoundsBottom << 16) | (ushort)BoundsRight;
}

// Registry mapping the int "frame-record pointer" stored in the frame-pointer
// tables (and in SpriteNode.SpritePtr) to the managed object. Handles at
// 0x70000000+ — a managed-only band, so a stale/foreign handle throws in At()
// instead of silently reading zeros: the tripwire for an un-converted raw read.
public static class SpriteFrames
{
    public const int HandleBase = 0x70000000;
    private const int Stride = 0x40;

    private static readonly Dictionary<int, SpriteFrame> _store = new();
    private static int _nextHandle = HandleBase;

    public static SpriteFrame Register()
    {
        _nextHandle += Stride;
        var frame = new SpriteFrame(_nextHandle);
        _store[_nextHandle] = frame;
        return frame;
    }

    /// Throws on a stale/foreign handle — the migration tripwire.
    public static SpriteFrame At(int handle) => _store[handle];
    public static bool IsHandle(int handle) => _store.ContainsKey(handle);
}
