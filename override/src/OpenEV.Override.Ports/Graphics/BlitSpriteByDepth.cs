using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_100779c8 (EV Override-11.c lines 50708-50842) — the per-depth sprite blitter.
// The frame is a SpriteFrames object, the local src/dst rects are managed short[4]s, and the
// compose-port pixmap staging goes through the managed MacPixMap.
public static class BlitSpriteByDepth
{
    // width = rect right = left + width; height = rect bottom = top + height — callers pass
    // frame bounds +0x12 (width) then +0x10 (height).
    public static void Run(int spriteFrame, int clipSource, int dstPort, int srcPosPacked, int dstPosPacked, short width, short height)
    {
        var f = SpriteFrames.At(spriteFrame);
        int tempRgn = GlobalState.TempRegion;
        int clipRgn = clipSource == 0 ? 0 : SpriteNodes.At(clipSource).ClipRgn;

        // The src/dst rects: {top,left} = the packed position (v<<16|h), bottom = top + height,
        // right = left + width.
        var srcRect = new short[4];
        var dstRect = new short[4];
        srcRect[0] = (short)(srcPosPacked >> 16);   // top
        srcRect[1] = (short)srcPosPacked;           // left
        srcRect[2] = (short)(srcRect[0] + height);  // bottom
        srcRect[3] = (short)(srcRect[1] + width);   // right
        dstRect[0] = (short)(dstPosPacked >> 16);
        dstRect[1] = (short)dstPosPacked;
        dstRect[2] = (short)(dstRect[0] + height);
        dstRect[3] = (short)(dstRect[1] + width);

        // Play-area clamp (port-only — no decompile counterpart). The ORIGINAL never clips here:
        // sprites land in the full-size offscreen GWorld and the SCREEN is protected by
        // UpdateWindowRegionLayout's composite blits plus RepaintGameWindow's flush. The host
        // collapses offscreen==screen (the composite is a CopyBits self-copy no-op), so without
        // this the sprites would overdraw the 144px status panel until its next refresh. Trim the
        // 1:1 src/dst pair by the same per-edge amounts the composite pass clamps to, when
        // blitting into the game-world target (title/dialog paths leave the guard false).
        if (dstPort == GlobalState.OffscreenGameGWorld && GlobalState.InnerRight > 0)
        {
            if (dstRect[1] < 0) { srcRect[1] -= dstRect[1]; dstRect[1] = 0; }
            if (GlobalState.InnerRight < dstRect[3])
            {
                srcRect[3] -= (short)(dstRect[3] - GlobalState.InnerRight);
                dstRect[3] = GlobalState.InnerRight;
            }
            if (GlobalState.InnerBottom < dstRect[2])
            {
                srcRect[2] -= (short)(dstRect[2] - GlobalState.InnerBottom);
                dstRect[2] = GlobalState.InnerBottom;
            }
            if (dstRect[3] <= dstRect[1] || dstRect[2] <= dstRect[0])
                return;   // fully outside the play area
        }

        if (GlobalState.RenderMode == 4)
        {
            // DEAD (host pins RenderMode = 0). The Mac 4-bit path picks the even/odd colour-cell
            // half by src/dst column parity and CopyMask/CopyBits'es it. Re-derive vs FUN_1007b6ec
            // if a 4-bit mode returns; tripwire meanwhile:
            throw new System.NotSupportedException(
                "BlitSpriteByDepth: 4-bit blit requested (RenderMode pinning changed?) — re-derive the colour-cell branch.");
        }
        else if (GlobalState.RenderMode < 2)
        {
            // The LIVE path (host pins RenderMode = 0 in-game).
            if (f.MaskRgn == 0)
            {
                // srcBits = ColorRef (the host ResolveTexture key); maskBits = MaskBase (ignored by
                // the host CopyMask — alpha textures).
                MacToolbox.CopyMask(f.ColorRef, f.MaskBase, dstPort + 2, srcRect, srcRect, dstRect);
            }
            else
            {
                MacToolbox.CopyRgn(f.MaskRgn, tempRgn);
                MacToolbox.OffsetRgn(tempRgn, dstRect[1] - srcRect[1], dstRect[0] - srcRect[0]);
                if (clipRgn != 0)
                    MacToolbox.SectRgn(clipRgn, tempRgn, tempRgn);
                MacToolbox.CopyBits(f.ColorRef, dstPort + 2, srcRect, dstRect, 0, tempRgn);
            }
        }
        else
        {
            // >=2-bit depths — dead under pinned RenderMode 0; kept faithful in shape. NOTE: here
            // the decompile stores the header POINTER ITSELF (*param_1 = ColorRef) as the compose
            // pixmap's baseAddr — NOT a read through it (original asymmetry, preserved).
            var composePm = MacPixMaps.At(MacToolbox.GetPortPixMap(GlobalState.ComposeScratchPort));
            composePm.LegacyBaseAddr = f.ColorRef;
            composePm.SetBounds(f.BoundsTopLeftPacked, f.BoundsBotRightPacked);
            composePm.RowBytes = (ushort)MacToolbox.BitOr((int)f.ColorRowBytes, unchecked((int)0xffff8000));
            if (f.MaskRgn == 0)
            {
                MacToolbox.CopyMask(GlobalState.ComposeScratchPort + 2, f.MaskBase, dstPort + 2, srcRect, srcRect, dstRect);
            }
            else
            {
                MacToolbox.CopyRgn(f.MaskRgn, tempRgn);
                MacToolbox.OffsetRgn(tempRgn, dstRect[1] - srcRect[1], dstRect[0] - srcRect[0]);
                if (clipRgn != 0)
                    MacToolbox.SectRgn(clipRgn, tempRgn, tempRgn);
                MacToolbox.CopyBits(GlobalState.ComposeScratchPort + 2, dstPort + 2, srcRect, dstRect, 0, tempRgn);
            }
        }
    }
}
