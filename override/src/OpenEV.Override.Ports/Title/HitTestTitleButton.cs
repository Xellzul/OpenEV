using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10042a1c (EV Override-11.c lines 27585-27692).
// Hit-tests the mouse point against the 6 title buttons plus the EVO-logo
// strip, and while the button is held repaints the pressed button (CopyBits
// from the pressed-button PICT strip staged in the ANIM scratch GWorld) /
// restores the released one from the BACKDROP GWorld. Returns the pressed
// index: 0..5 = buttons, 6 = the logo strip rect, -1 = none.
//
// Fully managed: the Mac stack-pair rect walks (local_cc/local_9c/local_64 —
// `+ idx * 2` int-arith = idx*8 bytes spanning SIX contiguous stack rects, and
// the hit scan's `< 7` deliberately running one rect PAST local_64 into the
// adjacent auStack_34 logo rect) are modelled as explicit short[4] arrays.
public static class HitTestTitleButton
{
    public static int Run(int mousePoint)
    {
        short[][] buttons = TitleScreenGlobals.ButtonRects;
        short[] arena = TitleScreenGlobals.InnerArenaRect;
        int animKey = GWorldPort.ScratchPort + 2;        // CopyBits src key: ANIM scratch stage
        int screenKey = GlobalState.ActivePortPixmap + 2;  // CopyBits dst key: on-screen port
        int backdropKey = RenderGlobals.BackdropGWorld + 2;  // CopyBits src key: backdrop GWorld (restore)

        SetGamePortAndDevice.Run();

        // hitRects[0..5] = the 6 button rects; hitRects[6] (built below) = the EVO logo strip.
        var hitRects = new short[7][];
        var dstRects = new short[6][];
        for (int i = 0; i < buttons.Length; i++)
        {
            hitRects[i] = (short[])buttons[i].Clone();
            dstRects[i] = (short[])buttons[i].Clone();
        }
        hitRects[6] = Rect((short)(arena[1] + 95), (short)(arena[0] + 70),
                           (short)(arena[1] + 547), (short)(arena[0] + 160));

        // Clamp each pressed-repaint dst rect to the 87px button-cell width:
        // left column keeps its right edge, right column keeps its left edge.
        for (int i = 0; i < 5; i += 2) dstRects[i][1] = (short)(dstRects[i][3] - 87);
        // Decompile shifts dstRects[2]/[3] up 1px (top/bottom -1) between the two clamp
        // loops (ASM loc_42AF4) — both rects are read by the press/restore CopyBits
        // below, so the vertical shift must survive into the blit.
        MacToolbox.OffsetRect(dstRects[2], 0, -1);
        MacToolbox.OffsetRect(dstRects[3], 0, -1);
        for (int i = 1; i < 6; i += 2) dstRects[i][3] = (short)(dstRects[i][1] + 87);

        // Pressed-button source rects in the PICT-strip ANIM stage: row 0
        // left/right cells, rows 1/2 offset down by 59 / 118.
        var srcRects = new short[6][];
        srcRects[0] = Rect(0, 0, 87, 59);
        srcRects[1] = Rect(87, 0, 174, 59);
        srcRects[2] = (short[])srcRects[0].Clone();
        srcRects[3] = (short[])srcRects[1].Clone();
        srcRects[4] = (short[])srcRects[0].Clone();
        srcRects[5] = (short[])srcRects[1].Clone();
        MacToolbox.OffsetRect(srcRects[2], 0, 59);
        MacToolbox.OffsetRect(srcRects[3], 0, 59);
        MacToolbox.OffsetRect(srcRects[4], 0, 118);
        MacToolbox.OffsetRect(srcRects[5], 0, 118);

        // Initial hit scan (last match wins, as the original).
        int hit = -1;
        for (int i = 0; i < hitRects.Length; i++)
            if (MacToolbox.PtInRect(mousePoint, hitRects[i]))
                hit = i;

        if (hit != -1)
        {
            if (hit >= 0 && hit < 6)
                MacToolbox.CopyBits(animKey, screenKey, srcRects[hit], dstRects[hit], 0, 0);

            while (MacToolbox.StillDown())
            {
                HoverOrbDrawErase.Run();
                int mousePt = MacToolbox.GetMouse();
                int newHit = -1;
                for (int i = 0; i < hitRects.Length; i++)
                    if (MacToolbox.PtInRect(mousePt, hitRects[i]))
                        newHit = i;
                int oldHit = hit;
                hit = newHit;
                if (newHit != oldHit)
                {
                    // Press-new/revert-old are one atomic on-screen update — batch them
                    // so a host drain can't land between the two and show both buttons
                    // "pressed" at once (same draw-queue race as the ship-render fix).
                    MacToolbox.BeginDrawBatch();
                    if (newHit >= 0 && newHit < 6)
                        MacToolbox.CopyBits(animKey, screenKey, srcRects[newHit], dstRects[newHit], 0, 0);
                    if (oldHit >= 0 && oldHit < 6)
                        MacToolbox.CopyBits(backdropKey, screenKey, dstRects[oldHit], dstRects[oldHit], 0, 0);
                    MacToolbox.EndDrawBatch();
                }
                // No extra pacing needed here: MacToolbox.StillDown() itself sleeps 8ms
                // while the button is down (same fix, same rationale as TitleMainLoop
                // .RunSetupOnce's drain loop), which already caps this tight spin.
            }
        }
        MacToolbox.InvalRect(GlobalState.PortRect);
        return hit;
    }

    // {top,left,bottom,right} Rect from SetRect-style (left,top,right,bottom) args.
    private static short[] Rect(short left, short top, short right, short bottom)
        => new short[] { top, left, bottom, right };
}
