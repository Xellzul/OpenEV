using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10079e24 (EV Override-11.c lines 51979-52045): build a sprite FRAME's
// per-depth colour cell + row tables. spriteFrame is the SpriteFrames handle; the cell
// records and pixel buffers stay raw Mac heap blocks (the blitter boundary). Only the
// direct-colour path runs live (host pins RenderMode = 0): ColorRef becomes the pixel
// buffer, ColorRowTable / MaskRowTable the row tables. The 4-bit / 1-bit software
// colour-cell pipelines are tripwired — re-derive from FUN_10079e24 if a sub-8-bit
// mode ever returns.
public static class BuildSpriteScaleTable
{
    public static void Run(int spriteFrame, int scaleKind)
    {
        var f = SpriteFrames.At(spriteFrame);
        int boundsTopLeft = f.BoundsTopLeftPacked;     // frame rect {0,0,h,w}
        int boundsBotRight = f.BoundsBotRightPacked;
        short kind = (short)scaleKind;
        short right = (short)boundsBotRight;             // frame width
        short bottom = (short)((uint)boundsBotRight >> 16);  // frame height

        if (kind == 4 || kind == 1)
        {
            // DEAD (host pins RenderMode = 0): the 4-bit / 1-bit colour-cell pipelines never
            // build. Tripwire instead of silent raw-heap writes:
            throw new System.NotSupportedException(
                "BuildSpriteScaleTable: sub-8-bit depth requested (RenderMode pinning changed?) — re-derive the colour-cell branch.");
        }
        // Direct colour — the live path.
        f.ColorRowBytes = PixMapRowBytes(right);
        f.ColorRef = CheckedAllocClear.Run(bottom * f.ColorRowBytes);
        f.ColorRowTable = BuildPixMapRowTable.Rebuild(f.ColorRowTable, boundsTopLeft, boundsBotRight,
                 f.ColorRef, f.ColorRowBytes, kind);
        f.MaskRowTable = BuildPixMapRowTable.Rebuild(f.MaskRowTable, boundsTopLeft, boundsBotRight,
                 f.MaskBase, f.MaskRowBytes, 1);
    }

    // Port of FUN_100796fc (EV Override-11.c lines 51728-51742): PixMap rowBytes for a
    // `pixelWidth`-wide row — ((width*depth)+0x1f)>>3, masked to 0x1ffc, WITHOUT the 0x8000
    // PixMap flag (GWorldPort.PixMapRowBytesWithFlag is the flagged sibling FUN_10079670).
    // Depth = 1 bit when the colour-QuickDraw flag (ctx+0xc6) is 0, else the main GDevice
    // pixmap's pixelSize.
    internal static short PixMapRowBytes(int pixelWidth)
    {
        uint raw;
        if (GlobalState.ColorQuickDrawFlag == 0)
        {
            raw = ((uint)pixelWidth + 0x1f) >> 3;
        }
        else
        {
            MacToolbox.GetDevicePixMapFields(GlobalState.GDevice, out _, out _, out short depth);
            raw = (uint)(pixelWidth * depth + 0x1f) >> 3;
        }
        return (short)(raw & 0x1ffc);
    }
}
