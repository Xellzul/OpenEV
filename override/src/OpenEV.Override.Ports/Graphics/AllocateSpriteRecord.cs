namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007c2d4 (EV Override-11.c lines 53264-53279): allocate a fresh
// render node (no insert-after, no reuse) seeded with an initial state and screen
// position. Forwards to AddSpriteRenderNode.
public static class AllocateSpriteRecord
{
    public static int Run(short initState, short initPosX, short initPosY, int spriteHandle) =>
        AddSpriteRenderNode.Run(0, 0, initState, initPosX, initPosY, spriteHandle);
}
