using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100463f0 (EV Override-11.c lines 29284-29362).
// First-entry intro cutscene (PICT 8200 + scrolling text), run once from
// RunGameSessionLauncher on the player's first Enter Ship — not a win/victory
// screen. Lives in Misc/ beside its sole real caller.
public static class ShowIntroCutsceneAndStartMusic
{
    public static void Run()
    {
        // TOC note: the decompile reads the fade cell 0x10080e00 both as an ABSOLUTE
        // literal (line 29299) and as toc-0x7860 (line 29322) — only GameToc reconciles
        // them (GameToc-0x7860 == 0x10080e00); under _toc the PICT 8200 palette read
        // title scratch → the image drew invisibly. B5: both reads route through
        // Palette.ScreenFadeCTab, and **(toc-0x76f8) through Palette.ScreenPaletteCTab —
        // no raw TOC math left here.

        // Render context piVar1 (*0x10080d08) -> GlobalState.
        AnimatePaletteColorCycle.Run(16, Palette.ScreenFadeCTab);
        // Composite analogue of the CLUT fade above (host substrate; the CLUT ramp is inert
        // in the true-colour renderer): fade the outgoing screen to the fade-cell colour
        // (black). Guarded no-op when already faded; the reveal is the AnimatePaletteTransition
        // bridge below — without this pair the PICT/scroll played behind a stale composite.
        MacToolbox.ScreenFadeToColor(16, Palette.ScreenFadeCTab);
        // Host present gate: open AFTER the fade above so the buffer swap happens
        // while the screen is dark, matching the Mac's fade->draw-dark->reveal
        // order — don't reorder. Launcher re-closes it post-cutscene.
        MacToolbox.GameSceneReady = true;

        // Sentinel movie ID no real movie can match -> PlayMovieById always returns
        // 1 ("show your own fallback"), so the PICT + scroll intro below always runs.
        bool showFallbackIntro = PlayMovieById.Run(-32767, 1) != 0;
        if (showFallbackIntro)
        {
            int pictureHandle = MacToolbox.GetPicture(8200);
            int colorTable = MacToolbox.GetCTable(1001);
            SetGamePortAndDevice.Run();
            Palette.InstallScreenPalette(colorTable, 0);
            MacToolbox.ForeColor(QuickDrawColor.Black);

            // Menu-bar-included rect: port rect with top pulled up 20px.
            short[] menuBarRect =
            {
                (short)(GlobalState.PortTop - 20), GlobalState.PortLeft,
                GlobalState.PortBottom, GlobalState.PortRight,
            };
            int window = GlobalState.ActivePortPixmap;
            MacToolbox.RectRgn(MacToolbox.GetPortClipRgn(window), menuBarRect);
            MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(window), menuBarRect);
            MacToolbox.PaintRect(menuBarRect);

            // Centre PICT 8200's frame within the port rect, then draw it there
            // (RectCenter mutates the rect in place).
            short[] pictRect = GlobalState.PortRect;
            RectCenter.Run(pictureHandle, pictRect);
            MacToolbox.DrawPicture(pictureHandle, pictRect);
            if (pictureHandle != 0)
            {
                MacToolbox.HPurge(pictureHandle);
                MacToolbox.ReleaseResource(pictureHandle);
            }

            SetGamePortAndDevice.Run();
            Palette.InstallColorEntries(Palette.ScreenFadeCTab, 0);

            int soundHandle = LoadSndResource.Run(30003);
            if (soundHandle != 0)
            {
                SndPlay.Run(soundHandle, 10, 128, 128);
            }

            if (!MacToolbox.Button())
            {
                // No click yet: play the full slow reveal.
                AnimatePaletteTransition.Run(384, colorTable);
                MacToolbox.ScreenFadeToImage(384);   // composite analogue: the slow PICT reveal
            }
            else
            {
                // Button already held: skip straight to the fast reveal.
                AnimatePaletteTransition.Run(16, colorTable);
                MacToolbox.ScreenFadeToImage(16);    // composite analogue (skip-fast reveal)
            }
            Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);

            int startTicks = (int)MacToolbox.TickCount();
            short keyDown;
            do
            {
                int nowTicks = (int)MacToolbox.TickCount();
                if (599 < (uint)(nowTicks - startTicks)) break;
                if (MacToolbox.Button()) break;
                // BUG FIX (Pass-1 mis-rendering, restores ASM fidelity): decompile passes
                // FUN_1005f964(0x2c)/(0x39), but that helper's big-endian word/bit indexing
                // (word=arg>>4, bit=arg&0xf) tests real ADB key (arg^8), not arg itself — see
                // Keymap.TestLiveKeymapBit's "Caller keycode space" note. Real keys are
                // Return/Space, not Slash/CapsLock — the ASM-exact pair, not "any key"; don't
                // generalize to a full scan.
                keyDown = (short)Keymap.TestLiveKeymapBit(MacKeycode.Return);
                if (keyDown != 0) break;
                keyDown = (short)Keymap.TestLiveKeymapBit(MacKeycode.Space);
            } while (keyDown == 0);

            if (soundHandle != 0)
            {
                FlushMixQueueEntries.Run(soundHandle);
                MacToolbox.DisposePtr(soundHandle);
            }
            Palette.InstallScreenPalette(colorTable, 0);
        }
        LoadAndStartSoundPair.Run(30001);
        CreditsScroller.Run(20000);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
    }
}
