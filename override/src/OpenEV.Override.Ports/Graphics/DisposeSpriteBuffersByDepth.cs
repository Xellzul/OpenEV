using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10079cc4 (EV Override-11.c lines 51931-51975): dispose a sprite FRAME's
// per-depth colour cell + row tables. spriteFrame is the SpriteFrames handle; the colour
// cells / row tables it points at are raw Mac heap blocks (BuildSpriteScaleTable /
// BuildPixMapRowTable allocations), so their DisposePtr calls stay genuine toolbox frees.
public static class DisposeSpriteBuffersByDepth
{
    public static void Run(int spriteFrame, short depth)
    {
        var f = SpriteFrames.At(spriteFrame);

        if (depth == 4 || depth == 1)
        {
            // DEAD (host pins RenderMode = 0): the 4-bit colour-cell walk (+0/+4/+8 buffers,
            // +0x20/+0x24 row tables) and the 1-bit BitMap free — including the ORIGINAL Mac
            // bug where the 1-bit branch never zeroes ColorRef (dangling on the freed record) —
            // never run. Tripwire:
            throw new System.NotSupportedException(
                "DisposeSpriteBuffersByDepth: sub-8-bit depth (RenderMode pinning changed?) — re-derive from FUN_10079cc4.");
        }
        // Direct-colour path (the live path): ColorRef is the pixel buffer itself.
        MacToolbox.DisposePtr(f.ColorRef);
        f.ColorRef = 0;
        if (f.ColorRowTable != 0)
        {
            MacToolbox.DisposePtr(f.ColorRowTable);
        }
        f.ColorRowTable = 0;
        if (f.MaskRowTable != 0)
        {
            MacToolbox.DisposePtr(f.MaskRowTable);
        }
        f.MaskRowTable = 0;
    }
}
