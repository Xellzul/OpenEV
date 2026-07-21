using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat.Model;

// Managed sprite-frame POINTER tables for the combat/sprite subsystem — filled by
// LoadSpriteSheetsAndGWorlds (each entry is a 0x2e-byte sprite-cell "bitmap header"
// pointer from AllocateSlotBitmapHeader, or a sprite handle from LoadCIconToSprite /
// GetPicture). The header RECORDS are managed Graphics.Model.SpriteFrame objects now
// (looked up by handle via SpriteFrames.At in BlitSpriteByDepth / the host CopyBits
// bridge — the blitter boundary); the TABLES of pointers are managed int[] handle
// stores. The old raw-EvoMemory ranges behind both (BSS tables, heap header
// records) are gone now that EvoMemory itself was removed (OriginalGameStateTotalBytes).

// Per-weapon-graphic frame table ('spïn' 200..; was &DAT_1008cd90 + graphic*0x90 +
// frame*4). graphicIndex comes from WeaponTable[slot] field +0xa.
public static class WeaponDefTable
{
    public const int RecCount = 64, FrameCount = 36;
    public static readonly int[] Store = new int[RecCount * FrameCount];   // [graphic*36 + frame]
}

// Per-SHIP-CLASS heading frame table ('spïn' 0x80..; was &DAT_1008a748 +
// class*0x90 + (heading/10)*4 — the "WeaponGraphicsTable" name is historical).
public static class WeaponGraphicsTable
{
    public const int RecCount = 64, FrameCount = 36;
    public static readonly int[] Store = new int[RecCount * FrameCount];   // [class*36 + heading/10]
}

// Per-explosion-type frame table ('spïn' 400..402; was &DAT_1008f190 + type*0x28
// + frame*4). 3 types x 10 frames.
public static class ExplosionGraphicsTable
{
    public const int TypeCount = 3, FrameCount = 10;
    public static readonly int[] Store = new int[TypeCount * FrameCount];  // [type*10 + frame]
}

// Per-planet-graphic sprite header table ('spïn' 300..; was &DAT_1008cb48 +
// spobSpriteIndex*4; spobSpriteIndex = spob+0xa).
public static class PlanetSpriteRecordTable
{
    public const int Count = 64;
    public static readonly int[] Store = new int[Count];
}

// Carried-ship/fighter frame table ('spïn' 500; was &DAT_1008f208 + frame*4).
// The old "SpriteFrameDimTable.Ptr" form (= ReadInt(0x1008f208)) is Store[0].
public static class SpriteFrameDimTable
{
    public const int Count = 36;
    public static readonly int[] Store = new int[Count];
    public static int Ptr => Store[0];
}

// 'spïn' 700 (two debris frames) + the cicn nav/docking-ring frame quads + the
// lone cicn 20000. The old "DockingDebrisColorTables" RGBColor identity was a
// MISNOMER — these cells hold sprite-frame header/handle pointers the node
// tickers store into node+0x16.
public static class DockingDebrisFrameTables
{
    public static readonly int[] DebrisPair = new int[2];   // was 0x1008cc48/+4 ("DebrisColorTypeB/A")
    public static readonly int[] Cicn20000Cell = new int[1];   // was 0x1008cc54 (cicn 20000)
    public static readonly int[] DockingRingDim = new int[4];   // was 0x1008cd20 (cicn 10000..10003; old "Unlit" name)
    public static readonly int[] DockingRingLit = new int[4];   // was 0x1008cd30 (cicn 0x2714..; old "Lit" colour-table name)
}

// The remaining LoadSpriteSheetsAndGWorlds frame tables (old BSS homes noted).
public static class SpriteFrameTables
{
    public static readonly int[] Spin800Frames = new int[20];   // was 0x1008cc58 ('spïn' 800)
    public static readonly int[] Spin801Frames = new int[30];   // was 0x1008cca8 ('spïn' 0x321=801)
    public static readonly int[] TargetBrackets = new int[16];   // was 0x1008cd50 (cicn 0x2718.. 4x4 target brackets)
    public static readonly int[] HudOrbFrames = new int[2];    // was 0x1008f298 (cicn 18000/18001 — HUD blink orb lit/dim)
    public static readonly int[] HoverOrbFrames = new int[4];    // was 0x1008f2a0 == _toc+0x6c40 ('spïn' 900) — the TITLE hover orb's records (FUN_100468a8 blits these)
    public static readonly int[] StreakFrames = new int[64];   // was 0x1008f2b0 (cicn 1000.. 8x8 hyperspace streaks)
    public static readonly int[] CommFacePicts = new int[64];   // was 0x1008f5d0 (PICT 3000.. comm faces)

    // Loader-only state (no readers outside LoadSpriteSheetsAndGWorlds):
    public static int CTable1BitHandle;   // was 0x100870e0 (GetCTable(1))
    public static int CTable8BitHandle;   // was 0x100870e4 (GetCTable(8))
    public static byte HiResFlag;         // was 0x10087084 ('ëbug' bit 3)
}

// Per-weapon-slot name strings (was the 256-byte Pascal-string data-seg array
// &DAT_1009050c + weaponSlot*0x100, weaponSlot = ship+0x32; written by
// LoadWeaponResources from each 'wëap' resource's name, read by
// RedrawHudWeaponPanel). Managed string[] now — the resource name string is held
// directly (the Mac's raw-Pascal-copy-with-0xfe-cap is moot for resource names,
// always < 254 bytes). Pre-filled "" so an unloaded slot reads empty, matching
// the zero-length Pascal slot the old buffer decoded to.
public static class WeaponNameBuffer
{
    public static readonly string[] Names = NewEmpty(64);
    private static string[] NewEmpty(int n)
    {
        var a = new string[n];
        System.Array.Fill(a, "");
        return a;
    }
}

// Escort-spawn overlay-node handles (were the ptr cells 0x1008a738/3c/40/44 the escort
// spawner stored its render-node handles in). SpawnHudOverlayNodes writes the
// node fields through Graphics.Model.SpriteNodes.At(node) and stores each handle here.
public static class EscortSpawnRecord
{
    // The escort-overlay sprite-node handle (was the ptr cell 0x1008a744).
    public static int Handle;
    // The sibling node-handle cells SpawnHudOverlayNodes fills (were 0x1008a738/3c/40).
    // DockingRingNode is write-only in the port (the Mac kept it for an unported teardown/lookup
    // path); ReticleNode and HudOverlayNode are also read back (V2TitleAdapter's once-only
    // creation guard and the bracket/2x-speed-indicator draw paths, respectively).
    public static int DockingRingNode;  // was 0x1008a738 (SortKey 3, DockingRingDrawUpp/UpdateUpp)
    public static int ReticleNode;      // was 0x1008a73c (SortKey 7, ReticleDrawUpp/UpdateUpp)
    public static int HudOverlayNode;   // was 0x1008a740
}
