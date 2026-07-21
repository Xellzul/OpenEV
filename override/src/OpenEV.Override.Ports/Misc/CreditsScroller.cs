using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Misc;

// Graduated port of FUN_10041ba0 (EV Override-11.c lines 27084-27307) — the
// scrolling credits / registration-dialog text. Called from AboutEvoModal with
// STR# 20001 (credits) / 20002 (registration) / 20003 (already-registered).
//
// Lines scroll upward through a centred window: each frame draws the visible
// lines into the offscreen BACKDROP GWorld, CopyBits the window to screen,
// then black-fills the offscreen window again. Returns 1 when the scroll ran
// to completion, 0 on user cancel (click / 0x2c / 0x39), or the
// ReleaseResource code when the STR# is missing/empty.
//
// Fully managed: credit lines are C# strings (GetIndString → string); the
// data-seg constants used below (65535.0 / the i2d bias / 0.03125 for the
// edge-fade grey ramp, "   " for the <REG> replacement prefix) are PEF-dump
// literals, not live TOC reads.
public static class CreditsScroller
{
    // fadeOutToBlack: composite-fade the outgoing screen (the title) to black before the
    // credits appear.
    // DEVIATION (faithful): FUN_10041ba0 takes no such parameter — it always runs
    // AnimatePaletteColorCycle(16, ScreenFadeCTab), the Mac's CLUT fade (see NO-OP note
    // at the call site below). fadeOutToBlack is the faithful analogue for a true-colour
    // renderer: it modulates the composited frame instead. Callers currently diverge —
    // AboutEvoModal passes true for all three of its calls, ShowIntroCutsceneAndStartMusic's
    // id-20000 call passes the false default (it pairs its own composite fade in
    // RunGameSessionLauncher, so this avoids double-fading that transition).
    public static int Run(int strListId, bool fadeOutToBlack = false)
    {
        int strListHandle = MacToolbox.GetResource(MacResType.StringList, strListId);
        if (strListHandle == 0) return 0;

        MacToolbox.HLock(strListHandle);
        // BlockMoveData(*strListHandle, &lineCount, 2) — the STR# count is the
        // first big-endian short of the resource data.
        short lineCount = MacToolbox.ReadResourceShort(strListHandle, 0);
        MacToolbox.HUnlock(strListHandle);
        MacToolbox.HPurge(strListHandle);
        int resultCode = MacToolbox.ReleaseResource(strListHandle);
        if (lineCount <= 0) return resultCode;

        // Composite fade-to-black of the outgoing title. ClearScreenFade (after the black
        // PaintRect) reveals the now-black buffer so the credits are visible.
        if (fadeOutToBlack)
            MacToolbox.ScreenFadeToColor(16, 0, 0, 0);
        // NO-OP: ScreenFadeCTab is never seeded (see its own doc in Palette.cs), so this
        // CLUT fade — the original's only fade mechanism — draws nothing; it's kept as the
        // faithful call, with fadeOutToBlack above standing in as the visible analogue.
        AnimatePaletteColorCycle.Run(16, Palette.ScreenFadeCTab);
        SetGamePortAndDevice.Run();                                  // restore screen port
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // Background rect = port rect with the top raised 20px.
        short[] bgRect =
        {
            (short)(GlobalState.PortTop - 20), GlobalState.PortLeft,
            GlobalState.PortBottom, GlobalState.PortRight,
        };
        MacToolbox.PaintRect(bgRect);
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 1);
        MacToolbox.PaintRect(bgRect);
        // The screen buffer is now black; reveal it (the fade-out left FadeLevel at 0)
        // so the scrolling credits below draw at full brightness.
        if (fadeOutToBlack)
            MacToolbox.ClearScreenFade();

        short maxWidth = 0;
        MacToolbox.TextFont(20);
        MacToolbox.TextSize(24);
        if (128 < lineCount) lineCount = 128;

        // Load each credit line as a managed string (the Mac staged a Str255 +
        // a 256-byte NewPtr copy, FUN_10076178 capped at 250 bytes incl. the
        // trailing NUL; the managed copy below caps content at 249 chars).
        string[] lines = new string[lineCount];
        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            string line = MacToolbox.GetIndString((short)strListId, (short)(lineIndex + 1));
            short w = (short)MacToolbox.StringWidth(line);
            if (maxWidth < w) maxWidth = w;
            // "<REG>" prefix → 3-space prefix + the registered owner name
            // (managed string now — the Credits_RegBuf boundary buffer is gone).
            if (line.StartsWith("<REG>"))
            {
                GetRegisteredOwnerName.Run(out string ownerName);
                line = "   " + ownerName;    // *(toc-0x65e8) = "   "
            }
            lines[lineIndex] = line.Length > 249 ? line[..249] : line;
        }
        if (maxWidth < 32) maxWidth = 32;

        // Centred scroll Rect: collapse to the port centre point, inset to
        // (maxWidth/2+8) × 160, shift left 10px. InsetRect/OffsetRect below mutate
        // all four fields of the merged 8-byte {top,left,bottom,right} Rect, even
        // though the decompile's local_58/local_54 variable split makes it look
        // like only (top,left) changes — the ASM proves both pairs share one Rect.
        int hcenter = (GlobalState.PortLeft + GlobalState.PortRight) / 2;
        int vcenter = (GlobalState.PortTop + GlobalState.PortBottom) / 2;
        short[] scrollRect = { (short)vcenter, (short)hcenter, (short)vcenter, (short)hcenter };
        MacToolbox.InsetRect(scrollRect, (short)(-((maxWidth / 2) + 8)), -160);
        MacToolbox.OffsetRect(scrollRect, -10, 0);
        short[] scrollWindow = (short[])scrollRect.Clone();

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);   // draw offscreen
        MacToolbox.TextFont(20);
        MacToolbox.TextSize(24);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(RenderGlobals.BackdropPortRect);

        int scrollTop = scrollRect[0];
        int scrollBottom = scrollRect[2];
        int scrollLeft = scrollWindow[1];

        for (int scrollOffset = 0; ; scrollOffset++)
        {
            resultCode = 1;
            if (lineCount * 30 + (scrollBottom - scrollTop) < (short)scrollOffset) break;
            int frameStartTick = (int)MacToolbox.TickCount();
            for (int lineIndex = 0; (short)lineIndex < lineCount; lineIndex++)
            {
                int lineY = (scrollBottom - scrollOffset) + lineIndex * 30;
                short lineYShort = (short)lineY;
                if (scrollTop <= lineYShort && lineYShort <= scrollBottom + 24)
                {
                    MacToolbox.ForeColor(QuickDrawColor.White);
                    bool draw = true;
                    // Fade band near the bottom edge (within 32 px of scrollBottom).
                    if (System.Math.Abs(lineYShort - scrollBottom) < 33)
                    {
                        if (lineYShort < scrollBottom)
                            MacToolbox.RGBForeColor(FadeGray(scrollBottom - lineYShort));
                        else
                            draw = false;
                    }
                    // Fade band near the top edge (within 32 px of scrollTop+24).
                    if (System.Math.Abs(lineYShort - (scrollTop + 24)) < 33)
                    {
                        if (scrollTop + 24 < lineYShort)
                            MacToolbox.RGBForeColor(FadeGray(lineYShort - (scrollTop + 24)));
                        else
                            draw = false;
                    }
                    if (draw)
                    {
                        MacToolbox.MoveTo(scrollLeft + 10, lineY);
                        MacToolbox.DrawString(lines[(short)lineIndex]);
                    }
                }
            }
            SetGamePortAndDevice.Run();
            MacToolbox.ForeColor(QuickDrawColor.Black);
            // Blit the offscreen scroll window to screen, then black-fill it
            // offscreen for the next frame.
            MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2,
                                scrollWindow, scrollRect, 0, 0);
            SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(scrollWindow);
            Keymap.RefreshCachedKeymap();
            if (MacToolbox.Button()) { resultCode = 0; break; }
            if (Keymap.TestCachedKeymapBit(0x2c) != 0) { resultCode = 0; break; }
            if (Keymap.TestCachedKeymapBit(0x39) != 0) { resultCode = 0; break; }
            int delayTicks;
            if (Keymap.TestCachedKeymapBit(0x75) != 0) delayTicks = 4;            // up arrow → slower
            else delayTicks = Keymap.TestCachedKeymapBit(0x76) != 0 ? 0 : 2;      // down arrow → fastest, else normal
            // DEVIATION (faithful): the Mac busy-waited on TickCount with no sleep; this
            // yields the CPU instead so the title thread doesn't peg a core while the
            // MonoGame thread renders. Exits at the same TickCount threshold, but under
            // default host timer resolution Sleep(1) can overshoot by up to ~15ms, so the
            // loop may exit up to roughly one sleep-quantum late.
            while ((uint)((int)MacToolbox.TickCount() - frameStartTick) < (uint)delayTicks)
                System.Threading.Thread.Sleep(1);
        }

        SetGamePortAndDevice.Run();
        return resultCode;
    }

    // Edge-fade colour: grey ramp from the data-seg constants (PEF dump:
    // 65535.0 × i2d(distance) × 0.03125 — i.e. grey16 = distance × 2048).
    // Mac built an RGBColor with r=g=b=grey16; the port's RGBForeColor takes
    // 0xRRGGBB, so take the high byte and replicate across channels.
    private static uint FadeGray(int distance)
    {
        int v = (int)(65535.0 * distance * 0.03125);
        int g = (v >> 8) & 0xff;
        return (uint)((g << 16) | (g << 8) | g);
    }
}
