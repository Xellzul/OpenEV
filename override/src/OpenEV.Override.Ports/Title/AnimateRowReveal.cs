using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10046da0 (EV Override-11.c lines 29567-29726).
//
// Title-screen button-row reveal: restore the backdrop, load the three frame
// sounds (600-602), then for each of the 3 rows play a 16-frame PICT-strip
// reveal (PICTs 8300+), reverting each row to backdrop pixels when done, and
// finish with a "press pulse" that expands a line out to the bottom row's
// rect. Rects are managed short[4] {top,left,bottom,right} — no raw byte
// scratch involved (the EvoMemory arena that would have backed those in an
// early mechanical-transcription-style port is gone repo-wide).
public static class AnimateRowReveal
{
    public static void Run()
    {
        int activeKey = GlobalState.ActivePortPixmap + 2;   // on-screen port
        int animKey = GWorldPort.ScratchPort + 2;          // anim-scratch port (same one SetActivePortScratch stages into)
        int backdropKey = RenderGlobals.BackdropGWorld + 2;    // backdrop offscreen GWorld

        if (TitleScreenGlobals.ButtonRevealPulse)
        {
            // Stack COPY of the backdrop rect (decompile local_3c/_38); the arena
            // rect is read live, as the original.
            short[] backdropRect = (short[])TitleScreenGlobals.BackdropRect.Clone();
            short[] arena = TitleScreenGlobals.InnerArenaRect;

            // Restore the visible screen from the composed BACKDROP offscreen.
            short[] portRect = GlobalState.PortRect;
            MacToolbox.CopyBits(backdropKey, activeKey, portRect, portRect, 0, 0);

            // Repaint the clean backdrop (PICT 8000) into the offscreen BACKDROP GWorld.
            SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
            MacToolbox.DrawPicture(TitleScreenGlobals.Pict8000Handle, backdropRect);
            SetGamePortAndDevice.Run();
            if (TitleScreenGlobals.Pict8000Handle != 0)
            {
                MacToolbox.HPurge(TitleScreenGlobals.Pict8000Handle);
                MacToolbox.ReleaseResource(TitleScreenGlobals.Pict8000Handle);
                TitleScreenGlobals.Pict8000Handle = 0;
            }

            // Load the three frame sounds (600-602) into channels 1-3.
            for (short sndCh = 1; sndCh < SndChannelTable.Count; sndCh++)
                SndChannelTable.SetHandle(sndCh, LoadSndResource.Run(sndCh + 599));

            // The 6 button rects (3 rows x 2 cols), indexed [col + step]; rows 2-5
            // are the base rows (0/1) offset down by 71 / 139 (same layout as
            // DrawClosedButtons / InitTitleRects).
            short[][] rows = new short[6][];
            rows[0] = Rect(arena[1], (short)(arena[0] + 244),
                           (short)(arena[1] + 240), (short)(arena[0] + 303));
            rows[1] = Rect((short)(arena[3] + -237), (short)(arena[0] + 241),
                           (short)(arena[3] + 3), (short)(arena[0] + 300));
            rows[2] = Offset(rows[0], 71);
            rows[4] = Offset(rows[0], 139);
            rows[3] = Offset(rows[1], 71);
            rows[5] = Offset(rows[1], 139);

            // The two anim-source halves (right / left), and the DrawPicture dst rect.
            short[][] animSrc = { Rect(240, 0, 480, 59), Rect(0, 0, 240, 59) };
            short[] animDst = Rect(0, 0, 480, 59);

            for (short step = 0; step < 5 && !MacToolbox.Button(); step = (short)(step + 2))
            {
                SndPlay.Run(SndChannelTable.Handle(2), 1, 128, 128);
                for (int frameNum = 1; frameNum < 17 && !MacToolbox.Button(); frameNum++)
                {
                    int frameStart = (int)MacToolbox.TickCount();
                    if (frameNum == 12) SndPlay.Run(SndChannelTable.Handle(3), 1, 128, 128);
                    if (frameNum == 14) FlushMixQueueEntries.Run(SndChannelTable.Handle(2));

                    int pict = MacToolbox.GetPicture(frameNum + 8300);   // PICT 8301-8316
                    if (pict != 0)
                    {
                        // Draw this frame into the ANIM offscreen GWorld, then blit its two
                        // halves to the two columns of the current row.
                        GWorldPort.SetActivePortScratch();
                        MacToolbox.SetRect(animDst, 0, 0, 480, 59);   // re-set every frame, as the original (same values each time)
                        MacToolbox.DrawPicture(pict, animDst);
                        MacToolbox.HPurge(pict);
                        MacToolbox.ReleaseResource(pict);
                        SetGamePortAndDevice.Run();
                        // Both columns must land on screen together — a host drain caught
                        // between them would show one column on this anim frame and the
                        // other still on the last (same draw-queue race as the ship-render
                        // flicker fix).
                        MacToolbox.BeginDrawBatch();
                        for (int col = 0; col < 2; col++)
                            MacToolbox.CopyBits(animKey, activeKey, animSrc[col], rows[col + step], 0, 0);
                        MacToolbox.EndDrawBatch();

                        // Per-frame cap (~1 tick): yield the core instead of busy-spinning so
                        // the host's present thread isn't starved during the reveal (the
                        // port's host adaptation -- same cap, same animation pacing).
                        uint ticks = MacToolbox.TickCount();
                        while (ticks <= (uint)(frameStart + 1))
                        {
                            System.Threading.Thread.Sleep(1);
                            ticks = MacToolbox.TickCount();
                        }
                    }
                }
                // Revert this row to backdrop pixels — both columns as one atomic unit.
                MacToolbox.BeginDrawBatch();
                for (int col = 0; col < 2; col++)
                    MacToolbox.CopyBits(backdropKey, activeKey, rows[col + step], rows[col + step], 0, 0);
                MacToolbox.EndDrawBatch();
            }

            // Restore the backdrop over the whole reveal area.
            MacToolbox.CopyBits(backdropKey, activeKey, backdropRect, backdropRect, 0, 0);

            if (!MacToolbox.Button())
            {
                PressPulse(arena, activeKey, backdropKey);
            }
            else
            {
                for (short dch = 2; dch < SndChannelTable.Count; dch++)
                {
                    if (SndChannelTable.Handle(dch) != 0)
                    {
                        FlushMixQueueEntries.Run(SndChannelTable.Handle(dch));
                        MacToolbox.DisposePtr(SndChannelTable.Handle(dch));
                        SndChannelTable.SetHandle(dch, 0);
                    }
                }
                DrawPilotInfo.Run(1);
            }
        }

        // Tail (decompile 29704-29725): for channel 2, wait until it stops (or the
        // button is pressed -- flush + dispose), then clear the anim region and return.
        // Bug-for-bug: the `if (2 < ch)` test runs at the TOP of each iteration and ch
        // starts at 2, so the function returns right after channel 2 -- channel 3 is
        // never processed here.
        short ch = 2;
        while (true)
        {
            if (ch > 2)
            {
                GWorldPort.SetActivePortScratch();
                MacToolbox.ForeColor(QuickDrawColor.Black);
                // Don't recompute this rect as an unwired BSS rect -- that reads as an
                // inert {0,0,0,0} (a past port bug).
                MacToolbox.PaintRect(GlobalState.ScratchStageRect);
                SetGamePortAndDevice.Run();
                return;
            }
            if (SndChannelTable.Handle(ch) != 0)
            {
                while (true)
                {
                    if (CountMatchingSoundVoices.Run(SndChannelTable.Handle(ch)) == 0) break;
                    if (MacToolbox.Button()) { FlushMixQueueEntries.Run(SndChannelTable.Handle(ch)); break; }
                }
                MacToolbox.DisposePtr(SndChannelTable.Handle(ch));
                SndChannelTable.SetHandle(ch, 0);
            }
            ch = (short)(ch + 1);
        }
    }

    // The bottom-row "press pulse": expand a line at the rect's vertical centre out to
    // the full rect, blitting backdrop over it each step until it reaches the bottom.
    private static void PressPulse(short[] arena, int activeKey, int backdropKey)
    {
        short[] full = Rect((short)(arena[1] + 265), (short)(arena[0] + 260),
                            (short)(arena[1] + 380), (short)(arena[0] + 450));
        int sum = full[0] + full[2];   // top + bottom
        short centre = (short)((sum >> 1) + (sum < 0 && (sum & 1) != 0 ? 1 : 0));

        // Pulse rect: a zero-height line {centre, left, centre, right}.
        short[] pulse = Rect(full[1], centre, full[3], centre);

        // Loop until the pulse bottom reaches the original bottom.
        while (full[2] > pulse[2])
        {
            if (MacToolbox.Button())
            {
                full.CopyTo(pulse, 0);   // snap the pulse to the full rect
            }
            MacToolbox.InsetRect(pulse, 0, -5);   // grow vertically
            MacToolbox.CopyBits(backdropKey, activeKey, pulse, pulse, 0, 0);
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
