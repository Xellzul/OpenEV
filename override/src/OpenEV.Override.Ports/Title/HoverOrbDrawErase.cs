using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_100468a8 (EV Override-11.c lines 29415-29472).
public static class HoverOrbDrawErase
{
    public static void Run()
    {
        // Advance the orb atlas frame every 2 ticks.
        if ((uint)TitleScreenGlobals.OrbAnimTickTimer + 2U <= MacToolbox.TickCount())
        {
            TitleScreenGlobals.OrbAnimTickTimer = (int)MacToolbox.TickCount();
            short next = (short)(TitleScreenGlobals.OrbAnimFrame + 1);
            TitleScreenGlobals.OrbAnimFrame = next > 3 ? (short)0 : next;
        }
        short frame = TitleScreenGlobals.OrbAnimFrame;

        // Orb source-frame rect: computed (25px-wide cell, advanced per frame)
        // but never consumed — same as the original; BlitSpriteToWindow derives
        // the atlas cell straight from the frame index instead.
        short[] orbFrameRect = new short[4];
        MacToolbox.SetRect(orbFrameRect, 0, 100, 25, 125);
        MacToolbox.OffsetRect(orbFrameRect, (short)(frame * 25), 0);

        // Hit-test the mouse against the 6 buttons (last match wins, as the original).
        int mousePoint = MacToolbox.GetMouse();
        short hovered = -1;
        for (short i = 0; i < TitleScreenGlobals.ButtonRects.Length; i++)
        {
            if (MacToolbox.PtInRect(mousePoint, TitleScreenGlobals.ButtonRects[i]))
                hovered = i;
        }

        // Erase-then-draw is one atomic on-screen hover-state update: both target the
        // live screen buffer, and this function runs on essentially every idle title
        // frame, so batch them — a host drain caught between the erase and the redraw
        // would show the old orb gone and the new one not yet painted (the same
        // draw-queue race as the ship-render flicker fix).
        MacToolbox.BeginDrawBatch();

        // Erase the previously-hovered orb: blit the backdrop GWorld back over
        // it (rect grown 3px, matching the original).
        short prevHover = TitleScreenGlobals.LastHoveredOrb;
        if (hovered != prevHover && prevHover > -1)
        {
            short[] prevOrbRect = (short[])TitleScreenGlobals.OrbRects[prevHover].Clone();
            MacToolbox.InsetRect(prevOrbRect, -3, -3);
            MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2,
                prevOrbRect, prevOrbRect, 0, 0);
        }

        // Paint the orb over the newly-hovered button (skipped while the app
        // is in the background): the frame'th spïn-900 record from the table at
        // toc+0x6c40 (SpriteFrameTables.HoverOrbFrames, built by the boot's
        // LoadSpriteSheetsAndGWorlds) — a 32×32 cell whose PICT-8006 mask is a
        // ~25px disc, so CopyMask paints a ROUND orb and the corners keep the
        // backdrop. (An earlier port passed frame-index sentinels into a
        // BlitSpriteToWindow fast path that stamped unmasked square PICT-8001
        // cells — same art inside the disc, wrong silhouette.)
        if (hovered != -1 && !TitleScreenGlobals.InBackground)
        {
            short[] dstRect = TitleScreenGlobals.OrbRects[hovered];
            // Pack {top,left} into positionPacked (V<<16 | H) for BlitSpriteToWindow.
            int packedTopLeft = ((dstRect[0] & 0xffff) << 16) | (dstRect[1] & 0xffff);
            BlitSpriteToWindow.Run(SpriteFrameTables.HoverOrbFrames[frame], packedTopLeft, true);
        }

        MacToolbox.EndDrawBatch();

        TitleScreenGlobals.LastHoveredOrb = hovered;
    }
}
