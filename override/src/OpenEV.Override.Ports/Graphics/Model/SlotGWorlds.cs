namespace OpenEV.Override.Ports.Graphics.Model;

// The two per-slot GWorld record tables (256 slots each) the boot sprite loader
// fills — one COLOUR GWorld + one 1-bit MASK GWorld per claimed slot
// (LoadIconPairForSlot; slot counter RenderGlobals.SpriteLoadSlotIndex at
// 0x1008a4e8, just past this table's old range).
//
// Was two adjacent direct BSS arrays, stride 0x1a = DecodePictResource's
// 7-field GWorld record:
//   sprite table &DAT_100870e8 (GameToc-0x1578), [0x100870e8, 0x10088ae8)
//   mask table   GameToc+0x488,                  [0x10088ae8, 0x1008a4e8)
// Record layout (int-indexed in the decompile):
//   +0x00 [0] primary port        +0x04 [1] GDevice (mask GWorlds: 0 = B&W)
//   +0x08 [2] pixmap row table    +0x0c [3] cached bounds {top,left}
//   +0x10 [4] cached bounds {bottom,right}      +0x14 [5] pixel base
//   +0x18 [6] rowBytes (short; stored &0x3fff by DecodePictResource)
//
// Decompile consumers (both converted to these managed records):
//   FUN_1001e4fc (LoadIconPairForSlot)     — fills a slot pair at boot
//   FUN_1001e6d8 (AllocateSlotBitmapHeader) — reads [5]/[6] of both records
//     (its absolute-form addressing 0x100870fc/0x10087100 = slot 0's +0x14/+0x18
//      under the slot*0x1a index — same table, not a separate one)
//
// NB the ORIGINAL PEF aliased the mixer/sound BSS block 0x10089368..0x1008a03a
// INSIDE the mask table (mask slots >= 83 overlap it — see SoundMixer /
// OriginalGameStateTotalBytes). Both subsystems are fully managed now.
public sealed class SlotGWorldRecord
{
    public int Port;                  // +0x00 — the offscreen GrafPort/CGrafPort
    public int GDevice;               // +0x04 — its GDevice (0 for the B&W mask ports)
    public int RowTable;              // +0x08 — software blitter row table
    public int BoundsTopLeftPacked;   // +0x0c — cached port bounds {top,left}
    public int BoundsBotRightPacked;  // +0x10 — cached port bounds {bottom,right}
    public int PixBase;               // +0x14 — pixel-buffer base
    public short RowBytes;            // +0x18 — rowBytes (&0x3fff at the producer)
}

public static class SlotGWorlds
{
    public const int SlotCount = 256;

    public static readonly SlotGWorldRecord[] Sprite = NewTable();   // was 0x100870e8 + idx*0x1a
    public static readonly SlotGWorldRecord[] Mask = NewTable();   // was 0x10088ae8 + idx*0x1a

    private static SlotGWorldRecord[] NewTable()
    {
        var table = new SlotGWorldRecord[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            table[i] = new SlotGWorldRecord();
        return table;
    }
}
