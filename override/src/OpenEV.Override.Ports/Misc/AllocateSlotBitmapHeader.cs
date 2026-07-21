using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1001e6d8 (EV Override-11.c lines 13594-13647).
//
// Builds one sprite-cell bitmap header (frame record) for the CURRENT sprite-load slot
// (RenderGlobals.SpriteLoadSlotIndexSaved) and RETURNS its handle. MANAGED: the decompile
// wrote the NewPtr(0x2e) pointer through an out-cell; the managed SpriteFrame registry
// returns it directly instead — see Graphics.Model.SpriteFrame for the field-by-field
// offset layout (+0x00 ColorRef .. +0x14 ColorRowBytes) and Graphics.Model.SlotGWorlds
// for the per-slot sprite/mask record tables this reads from.
public static class AllocateSlotBitmapHeader
{
    public static int Run(short width, short height, short offsetX, short offsetY)
    {
        // NewPtr(0x2e) + fatal-exit on null in the decompile — the managed registry cannot fail.
        SpriteFrame frame = SpriteFrames.Register();

        // dest rect = {0,0,height,width} OffsetRect by (offsetX, offsetY).
        short top = offsetY, left = offsetX;

        int slot = RenderGlobals.SpriteLoadSlotIndexSaved;
        SlotGWorldRecord maskRec = SlotGWorlds.Mask[slot];
        SlotGWorldRecord spriteRec = SlotGWorlds.Sprite[slot];

        short maskRowBytes = (short)(maskRec.RowBytes & 0x3fff);
        frame.MaskRowBytes = maskRowBytes;
        // Truncating division by 8 (srawi+addze in the ASM) — do not simplify to a bare >>3.
        frame.MaskBase = maskRec.PixBase + top * maskRowBytes
            + (left >> 3) + ((left < 0 && (left & 7) != 0) ? 1 : 0);

        // Cell rect (+0xc): SetRect{0,0,height,width} offset by (offsetX,offsetY) then offset
        // back by (-offsetX,-offsetY) in the ASM nets out to exactly {0,0,height,width}.
        frame.BoundsTop = 0;
        frame.BoundsLeft = 0;
        frame.BoundsBottom = height;
        frame.BoundsRight = width;

        // NB the decompile multiplies top by the RAW spriteRec rowBytes field here (only the
        // stored header+0x14 is masked) — identical values regardless: the managed PixMap's
        // RowBytes field never carries the Mac's high flag bits at all (MacToolbox.GetPixMapRowBytes),
        // so the &0x3fff mask is a no-op on either read here.
        short rowBytes = (short)(spriteRec.RowBytes & 0x3fff);
        frame.ColorRowBytes = rowBytes;
        int bits = left * GlobalState.RenderMode;
        // Truncating division by 8 — do not simplify to a bare >>3.
        frame.ColorRef = spriteRec.PixBase + top * rowBytes
            + (bits >> 3) + ((bits < 0 && (bits & 7) != 0) ? 1 : 0);

        // Zeroed tail +0x1a..0x2d (MaskRgn/ColorRowTable/MaskRowTable/RerenderUpp/CustomDrawUpp)
        // = the fresh object's defaults.
        return frame.Handle;
    }
}
