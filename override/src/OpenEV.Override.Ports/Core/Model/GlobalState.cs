using System.Collections.Generic;

namespace OpenEV.Override.Ports.Core.Model;

// Typed managed home for the GLOBAL STATE RECORD — the in-game render-context
// struct the decompile reaches through `_DAT_10080d08` (slot 0x10080d08,
// accessor formerly ResourceGlobals.GlobalStateRecord).
//
// The slot is a single 4-byte pointer cell; the body is a large Mac record
// (>0x110 bytes) allocated once and shared by the whole render subsystem
// (InitRenderWindow, GWorldPort, the sprite/HUD/offscreen-GWorld tree, plus the
// title GrafPort). Each value now has ONE home — a real C# field here.
//
// OFFSETS: the decompile types the record pointer `int *`, so its `piVar3 + K`
// is BYTE offset K*4 while `*(short*)((int)piVar3 + N)` is byte N. The byte
// offsets below are the resolved values (an earlier transcription mis-scaled several —
// e.g. `piVar3+7` is byte 0x1c, `piVar3+0x19` is byte 0x64, `piVar3+0x32..0x3d`
// are bytes 0xc8..0xf4).
//
// The GWorld "ports" at +0x1e / +0x38 / +0x8a / +0xa4 are each an embedded
// 3-field sub-record {port, GDevice, rowTable} the GWorld helper tree walks by
// base+0/+4/+8 (so +0x8a → port +0x8a, GDevice +0x8e, rowTable +0x92). The 3rd
// field is DecodePictResource's per-row pixmap-address table (BuildPixMapRowTable),
// NOT a raw pixel-buffer base — renamed from the legacy `BasePtr` transcription label
// (Rule 12; matches DecodePictResource.cs's own `rowTable` naming and
// SlotGWorldRecord.RowTable, the sibling per-slot record's same field).
public static class GlobalState
{
    // The HUD play-area clip Rect {top,left,bottom,right} — was the BSS record
    // behind ptr cell 0x10080ef4. The Mac seeder FUN_10052b38
    // (= Combat.SpawnHudOverlayNodes) SetRects it to the
    // bottom-left comm/chatter box at boot AND enter-ship (BuildShipSpriteTable);
    // zero only in the gap before the first SpawnHudOverlayNodes runs.
    public static readonly short[] HudPlayAreaClipRect = new short[4];

    // The scrolling-starfield band rect — was the BSS record behind ptr cell
    // 0x10081230; RunMainGameLoop owns it.
    public static readonly short[] StarfieldScrollRect = new short[4];

    // ── On-screen port / device ──
    public static int ActivePortPixmap;   // +0x00  window handle / on-screen port (CopyBits +2 key)
    public static int GDevice;            // +0x04  main GDevice handle
    public static int PixMapRowTableBase; // +0x08  ptr to the per-row pixmap table (NewPtr'd)

    // Port/blit rect (+0xc..+0x12), set from the draw rect.
    public static short PortTop;            // +0x0c
    public static short PortLeft;           // +0x0e
    public static short PortBottom;         // +0x10
    public static short PortRight;          // +0x12

    public static int ScreenBaseAddr;     // +0x14  = QD globals screenBits.baseAddr
    public static short ScreenRowBytes;     // +0x18
    public static short InnerRight;         // +0x1a  centred play-area width
    public static short InnerBottom;        // +0x1c  centred play-area height (decompile piVar3+7)

    // ── Offscreen / scratch GWorld sub-records ({port, GDevice, rowTable}) ──
    public static int OffscreenGameGWorld;     // +0x1e port
    public static int OffscreenGameGDevice;    // +0x22 GDevice
    public static int OffscreenGameRowTable;   // +0x26 rowTable
    public static int AnimScratchPort;         // +0x38 port (decompile piVar1[0xe])
    public static int AnimScratchGDevice;      // +0x3c GDevice
    public static int AnimScratchRowTable;     // +0x40 rowTable
    public static int ComposeScratchPort;      // +0x8a port (primary GWorld)
    public static int ComposeScratchGDevice;   // +0x8e GDevice
    public static int ComposeScratchRowTable;  // +0x92 rowTable (DisposeGWorldRecord +8)
    public static int SecondaryGWorldPort;     // +0xa4 port
    public static int SecondaryGWorldGDevice;  // +0xa8 GDevice
    public static int SecondaryGWorldRowTable; // +0xac rowTable

    // Cached primary-GWorld geometry (InstallPrimaryGWorldPort): the packed
    // {top,left}/{bottom,right} bounds ints.
    public static int PrimaryCacheTopLeftPacked;  // +0x96
    public static int PrimaryCacheBotRightPacked; // +0x9a
    public static int PrimaryCacheBase;           // +0x9e
    public static short PrimaryCacheRowBytes;       // +0xa2
    // Cached secondary-GWorld geometry (InstallSecondaryGWorldPort).
    public static int SecondaryCacheTopLeftPacked;  // +0xb0
    public static int SecondaryCacheBotRightPacked; // +0xb4
    public static int SecondaryCacheBase;           // +0xb8
    public static short SecondaryCacheRowBytes;       // +0xbc

    public static int SpriteGWorldPort;       // +0xbe sprite GWorld port

    // ── Scroll / view ──
    public static int ScrollOffsetX;          // +0x52
    public static int ScrollOffsetY;          // +0x56

    // Window bounds Rect (+0x6a..+0x70).
    public static short WindowBoundsTop;      // +0x6a
    public static short WindowBoundsLeft;     // +0x6c
    public static short WindowBoundsBottom;   // +0x6e
    public static short WindowBoundsRight;    // +0x70

    // Drawable rect (local) +0x100..+0x106 and its global copy +0x108..+0x10e.
    public static short DrawRectTop;          // +0x100
    public static short DrawRectLeft;         // +0x102
    public static short DrawRectBottom;       // +0x104
    public static short DrawRectRight;        // +0x106
    public static short DrawRectGlobalTop;    // +0x108
    public static short DrawRectGlobalLeft;   // +0x10a
    public static short DrawRectGlobalBottom; // +0x10c
    public static short DrawRectGlobalRight;  // +0x10e

    // ── Cached content widths / scroll (SetScrollViewPosition) ──
    public static short PrimaryContentWidth;   // +0x30
    public static short SecondaryContentWidth; // +0x4a
    public static short ScrollVert;            // +0x5c

    // Primary (offscreen-game) stage Rect (+0x2a..+0x30) — its right edge IS
    // PrimaryContentWidth. (RunGameSessionLauncher black-clears it.)
    public static short PrimaryStageTop;       // +0x2a
    public static short PrimaryStageLeft;      // +0x2c
    public static short PrimaryStageBottom;    // +0x2e
    public static short[] PrimaryStageRect =>
        new[] { PrimaryStageTop, PrimaryStageLeft, PrimaryStageBottom, PrimaryContentWidth };

    // Anim-scratch stage Rect (+0x44..+0x4a) — its right edge IS SecondaryContentWidth.
    // (DrawTitleSecondaryPict black-clears it; CacheMapNebulaBackgrounds reads +0x44/+0x46.)
    public static short ScratchStageTop;       // +0x44
    public static short ScratchStageLeft;      // +0x46
    public static short ScratchStageBottom;    // +0x48
    public static short[] ScratchStageRect =>
        new[] { ScratchStageTop, ScratchStageLeft, ScratchStageBottom, SecondaryContentWidth };

    // ── Sprite-loop / list state ──
    public static short ScrollHoriz;              // +0x5a (horiz scroll)
    public static byte SpriteLoopEnabled;    // +0x5e
    public static short SpriteLoopStart;      // +0x60
    public static short SpriteLoopEnd;        // +0x62
    public static short SpriteLoopValue;      // +0x64
    public static int FrameCallbackUpp;    // +0x74 per-frame callback UPP (UpdateWindowRegionLayout)
    public static int SpriteListHead;       // +0x78
    // +0x7c dirty-rect list (was a head-inserted chain of NewPtr(0xc) nodes
    // {rect topLeft, rect botRight, next}; managed list now — Insert(0, rect)
    // preserves the Mac LIFO walk order). Each entry is a {top,left,bottom,right}
    // short[4]. UpdateWindowRegionLayout consumes + clears it per frame.
    public static readonly List<short[]> DirtyRects = new();
    public static byte SpriteListLock;       // +0x80
    public static int SpriteListHead2;      // +0xc2
    public static int SpriteFreeListHead;   // +0x110 (render-node free list)
    public static int OffscreenGWorldA;        // +0x82
    public static int OffscreenGWorldADevice;  // +0x86

    // The render temp region (was *PTR_DAT_100812a0 → cell → RgnHandle; both
    // levels collapsed to one MacRegions handle). Lazily created by InitRenderWindow;
    // BlitSpriteByDepth stages sprite mask regions in it.
    public static int TempRegion;

    // ── Render mode / depth selection ──
    public static short RenderMode;           // +0x72 pixel-depth selector
    public static byte ColorQuickDrawFlag;   // +0xc6 (1 = colour QuickDraw available)
    public static byte NonColorQuickDrawFlag;            // +0xc7 (set 1 when ColorQuickDrawFlag is 0)
    public static int CurrentDepthRenderer;   // +0xf8 depth-selected PR sprite renderer
    public static int CurrentDepthRendererPM; // +0xfc depth-selected PM sprite renderer

    // 12 sprite-blitter code-fragment slots at BYTE offsets 0xc8..0xf4 (decompile
    // piVar3+0x32..0x3d, int-indexed). PR/PM pairs for depths 1/2/4/8/0x10/0x20:
    // index 0,2,4,6,8,10 = PR (the per-depth renderers copied into CurrentDepthRenderer),
    // 1,3,5,7,9,11 = PM. Element i is byte 0xc8 + i*4.
    public static readonly int[] SpriteBlitterFrags = new int[12];

    // ── Packed-int views (the decompile copies/offsets these rects as ints) ──
    private static int Pack(short hi, short lo) => (hi << 16) | (lo & 0xffff);

    public static int PortTopLeftPacked
    { get => Pack(PortTop, PortLeft); set { PortTop = (short)(value >> 16); PortLeft = (short)value; } }
    public static int PortBotRightPacked
    { get => Pack(PortBottom, PortRight); set { PortBottom = (short)(value >> 16); PortRight = (short)value; } }
    public static int WindowBoundsTopLeftPacked
    { get => Pack(WindowBoundsTop, WindowBoundsLeft); set { WindowBoundsTop = (short)(value >> 16); WindowBoundsLeft = (short)value; } }
    public static int WindowBoundsBotRightPacked
    { get => Pack(WindowBoundsBottom, WindowBoundsRight); set { WindowBoundsBottom = (short)(value >> 16); WindowBoundsRight = (short)value; } }
    public static int DrawRectTopLeftPacked
    { get => Pack(DrawRectTop, DrawRectLeft); set { DrawRectTop = (short)(value >> 16); DrawRectLeft = (short)value; } }
    public static int DrawRectBotRightPacked
    { get => Pack(DrawRectBottom, DrawRectRight); set { DrawRectBottom = (short)(value >> 16); DrawRectRight = (short)value; } }

    // ── Managed Rect views ({top,left,bottom,right} short[4]) for OffsetRect etc. ──
    public static short[] PortRect
    {
        get => new[] { PortTop, PortLeft, PortBottom, PortRight };
        set { PortTop = value[0]; PortLeft = value[1]; PortBottom = value[2]; PortRight = value[3]; }
    }
    public static short[] DrawRect
    {
        get => new[] { DrawRectTop, DrawRectLeft, DrawRectBottom, DrawRectRight };
        set { DrawRectTop = value[0]; DrawRectLeft = value[1]; DrawRectBottom = value[2]; DrawRectRight = value[3]; }
    }
    public static short[] DrawRectGlobal
    {
        get => new[] { DrawRectGlobalTop, DrawRectGlobalLeft, DrawRectGlobalBottom, DrawRectGlobalRight };
        set { DrawRectGlobalTop = value[0]; DrawRectGlobalLeft = value[1]; DrawRectGlobalBottom = value[2]; DrawRectGlobalRight = value[3]; }
    }
    public static short[] WindowBoundsRect
    {
        get => new[] { WindowBoundsTop, WindowBoundsLeft, WindowBoundsBottom, WindowBoundsRight };
        set { WindowBoundsTop = value[0]; WindowBoundsLeft = value[1]; WindowBoundsBottom = value[2]; WindowBoundsRight = value[3]; }
    }
}
