namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007e3e4 (EV Override-11.c lines 54630-54656). The original stores its 8 args
// to the stack, then tail-calls through FUN_1008062c into the UPP held in blitProc — either a
// sprite frame's own CustomDrawUpp override or the per-depth SpriteBlitterFrags[] renderer —
// passing it spriteFrame/clipSource/dstPort/srcPosPacked/dstPosPacked/width/height.
//
// The port has no Mixed Mode Manager, so this forwards straight to BlitSpriteByDepth and drops
// blitProc. Both branches are identical, not merely analogous:
//   - DefaultSpriteRenderer's TVector resolves (via tools/resolve_tvec.py against the shipped
//     binary) directly to FUN_100779c8 = BlitSpriteByDepth itself, and SelectSpriteRenderersByDepth
//     (FUN_1007a950) always assigns CurrentDepthRendererPM = DefaultSpriteRenderer while
//     RenderMode is pinned to 0 (every real run of the port) — so the "no override" blitProc IS
//     BlitSpriteByDepth's own code, not a lookalike.
//   - CustomDrawUpp (frame +0x2a) is zeroed by its sole allocator (FUN_1001e6d8) and never
//     written anywhere else in the decompile — that override branch is dead in the ORIGINAL too.
// Do not restore a real UPP dispatch here — it would resolve to nothing on this host and
// silently drop every sprite blit again.
public static class InvokeSpriteBlitUpp
{
    public static void Run(int spriteFrame, int clipSource, int dstPort, int srcPosPacked,
                     int dstPosPacked, short width, short height, int blitProc) =>
        BlitSpriteByDepth.Run(spriteFrame, clipSource, dstPort, srcPosPacked, dstPosPacked, width, height);
}
