using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Title.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10046a88 (EV Override-11.c lines 29478-29566).
//
// Draws the PICT 8000 backdrop into the BACKDROP GWorld and on screen, then —
// while the intro pulse is armed — stages the closed-button strip (PICT 8300)
// in the ANIM GWorld and blits its two halves over the 6 button rects. A held
// mouse button disarms the pulse, ending the intro.
public static class DrawClosedButtons
{
    public static void Run()
    {
        short[] arena = TitleScreenGlobals.InnerArenaRect;
        // Local copy — most readers of BackdropRect clone it (the decompile
        // stack-copies it too); only InitTitleBackdrop writes the shared rect directly.
        short[] backdropRect = (short[])TitleScreenGlobals.BackdropRect.Clone();

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.DrawPicture(TitleScreenGlobals.Pict8000Handle, backdropRect);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.DrawPicture(TitleScreenGlobals.Pict8000Handle, backdropRect);

        if (TitleScreenGlobals.ButtonRevealPulse && MacToolbox.Button())
        {
            TitleScreenGlobals.ButtonRevealPulse = false;
        }
        if (TitleScreenGlobals.ButtonRevealPulse)
        {
            int pict = MacToolbox.GetPicture(8300);   // closed-button strip
            if (pict != 0)
            {
                // Stage the closed-button strip into the ANIM scratch GWorld.
                GWorldPort.SetActivePortScratch();
                short[] stageRect = Rect(0, 0, 480, 59);
                MacToolbox.DrawPicture(pict, stageRect);
                MacToolbox.HPurge(pict);
                MacToolbox.ReleaseResource(pict);
                SetGamePortAndDevice.Run();

                // The 6 button rects (3 rows × 2 cols), indexed [col + row]
                // (same layout as AnimateRowReveal / InitTitleRects).
                short[][] rows = new short[6][];
                rows[0] = Rect(arena[1], (short)(arena[0] + 244),
                               (short)(arena[1] + 240), (short)(arena[0] + 303));
                rows[1] = Rect((short)(arena[3] - 237), (short)(arena[0] + 241),
                               (short)(arena[3] + 3), (short)(arena[0] + 300));
                rows[2] = Offset(rows[0], 71);
                rows[4] = Offset(rows[0], 139);
                rows[3] = Offset(rows[1], 71);
                rows[5] = Offset(rows[1], 139);

                // Strip halves: col 0 = right half of the stage, col 1 = left half.
                short[][] stripSrc = { Rect(240, 0, 480, 59), Rect(0, 0, 240, 59) };

                for (short col = 0; col < stripSrc.Length; col = (short)(col + 1))
                {
                    for (short row = 0; row < rows.Length; row = (short)(row + 2))
                    {
                        MacToolbox.CopyBits(GWorldPort.ScratchPort + 2,
                                            GlobalState.ActivePortPixmap + 2,
                                            stripSrc[col], rows[col + row], 0, 0);
                    }
                }
            }
        }

        Palette.FadeOut(16);
        if (!TitleScreenGlobals.ButtonRevealPulse)
        {
            DrawPilotInfo.Run(1);
        }
        else
        {
            // Copy the composed SCREEN back into the BACKDROP GWorld over the backdrop rect.
            MacToolbox.CopyBits(GlobalState.ActivePortPixmap + 2,
                                RenderGlobals.BackdropGWorld + 2,
                                backdropRect, backdropRect, 0, 0);
        }
    }

    // {top,left,bottom,right} Rect from SetRect-style (left,top,right,bottom) args.
    private static short[] Rect(short left, short top, short right, short bottom)
        => new short[] { top, left, bottom, right };

    // A copy of `r` offset down by `dy`.
    private static short[] Offset(short[] r, short dy)
    {
        short[] c = (short[])r.Clone();
        MacToolbox.OffsetRect(c, 0, dy);
        return c;
    }
}
