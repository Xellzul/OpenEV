namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007a16c (EV Override-11.c lines 52090-52101): reinitialize a sprite FRAME's
// scaled buffers — dispose the per-depth colour cell built at oldDepth, then rebuild the
// scale table at newDepth. spriteFrame is the SpriteFrames handle.
public static class SpriteReinitToDepth
{
    public static void Run(int spriteFrame, short oldDepth, short newDepth)
    {
        DisposeSpriteBuffersByDepth.Run(spriteFrame, oldDepth);
        BuildSpriteScaleTable.Run(spriteFrame, newDepth);
    }
}
