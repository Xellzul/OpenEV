using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10046674 (EV Override-11.c lines 29363-29409).
// Lays out the 6 orb rects and 6 button rects (3 rows × 2 columns) relative to
// the inner-arena rect, then resets the hover-orb animation state.
//
// Fully managed via TitleScreenGlobals (rect arrays + the hover-anim fields
// shared with HoverOrbDrawErase — see that class's alias map).
public static class InitTitleRects
{
    public static void Run()
    {
        short[] arena = TitleScreenGlobals.InnerArenaRect;  // {top, left, bottom, right}; bottom unused here
        short[][] orb = TitleScreenGlobals.OrbRects;
        short[][] btn = TitleScreenGlobals.ButtonRects;

        // Orb rects: row 0 left/right, then rows 1/2 copied down from row 0/1.
        // (Mirrors the decompile's rect0->rect4->rect2 / rect1->rect5->rect3
        // copy chain — net-equivalent to copying straight from the base rect.)
        MacToolbox.SetRect(orb[0], (short)(arena[1] + 126), (short)(arena[0] + 261),
                                   (short)(arena[1] + 151), (short)(arena[0] + 286));
        MacToolbox.SetRect(orb[1], (short)(arena[3] + -144), (short)(arena[0] + 259),
                                   (short)(arena[3] + -121), (short)(arena[0] + 284));
        Copy(orb[0], orb[4]);
        Copy(orb[4], orb[2]);
        Copy(orb[1], orb[5]);
        Copy(orb[5], orb[3]);
        MacToolbox.OffsetRect(orb[2], 0, 71);
        MacToolbox.OffsetRect(orb[4], 0, 139);
        MacToolbox.OffsetRect(orb[3], -1, 71);   // row-1 right column nudges 1px left
        MacToolbox.OffsetRect(orb[5], -2, 139);  // row-2 right column nudges 2px left

        // Button hit-test rects: same row/column layout, no nudge.
        MacToolbox.SetRect(btn[0], arena[1], (short)(arena[0] + 244),
                                   (short)(arena[1] + 240), (short)(arena[0] + 303));
        MacToolbox.SetRect(btn[1], (short)(arena[3] + -237), (short)(arena[0] + 241),
                                   (short)(arena[3] + 3), (short)(arena[0] + 300));
        Copy(btn[0], btn[4]);
        Copy(btn[4], btn[2]);
        Copy(btn[1], btn[5]);
        Copy(btn[5], btn[3]);
        MacToolbox.OffsetRect(btn[2], 0, 71);
        MacToolbox.OffsetRect(btn[4], 0, 139);
        MacToolbox.OffsetRect(btn[3], 0, 71);
        MacToolbox.OffsetRect(btn[5], 0, 139);

        // Reset the hover-orb animation state.
        TitleScreenGlobals.OrbAnimFrame = 0;
        TitleScreenGlobals.OrbAnimTickTimer = (int)MacToolbox.TickCount();
        TitleScreenGlobals.LastHoveredOrb = -1;
    }

    private static void Copy(short[] src, short[] dst) => src.CopyTo(dst, 0);
}
