using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1004173c (EV Override-11.c lines 26933-27013): the boot-time loading/
// progress bar. Loads the 'spït' 128 record for the bar total, draws an expanding
// bordered bar centred at the bottom of the screen, then hands off to
// RedrawCreditsProgressBar. Called once from the boot orchestrator (GameBootSequence).
//
// The bar rect / three RGBColor records / progress doubles live in
// Graphics.Model.BootProgress (formerly the boot-allocator pointer slots
// 0x10081090..0x100810a8). Window portRect reads are Core.Model.GlobalState.Port* fields; palette
// installs go through the guarded Graphics.Model.Palette CTab accessors (0 while
// unseeded, so the install no-ops).
public static class AnimateBootProgressBar
{
    public static void Run()
    {
        // GetResource('spït', 128); its first short is the bar total.
        int spitHandle = MacToolbox.GetResource(MacResType.Spit, 128);
        // The decompile reads an UNINITIALIZED register when 'spït' 128 is missing (a
        // quirk); 0 here keeps Total 0 so the hand-off draws an empty bar.
        short barTotal = 0;
        if (spitHandle != 0)
        {
            barTotal = MacToolbox.ReadResourceShort(spitHandle, 0);
            MacToolbox.HPurge(spitHandle);
            MacToolbox.ReleaseResource(spitHandle);
        }
        // The decompile's float cast is the PPC
        // i2d idiom == (double)v exactly; barTotal is already a short, so a plain assign fits.
        BootProgress.Total = barTotal;
        BootProgress.Current = 0.0;

        // The three RGBColor records (16-bit channels → packed high-byte 0xRRGGBB):
        // fill {0,0xffff,0}, mid {0,40000,0}, frame {25000,25000,25000} grey.
        BootProgress.BarFillColor = UiColorConstants.BootBarFillColor;
        BootProgress.BarMidColor = UiColorConstants.BootBarMidColor;
        BootProgress.BarFrameColor = UiColorConstants.BootBarFrameColor;

        // Centre a 200px-wide bar near the bottom of the window portRect (top@0xc,
        // left@0xe, bottom@0x10, right@0x12). The signed /2 matches the decompile's
        // srawi+addze truncating divide (not a plain shift).
        int center = ((int)GlobalState.PortLeft + GlobalState.PortRight) / 2;
        short bottom = GlobalState.PortBottom;
        if (GlobalState.PortBottom - GlobalState.PortTop < 481)
            MacToolbox.SetRect(BootProgress.BarRect, (short)(center - 100), (short)(bottom - 13), (short)(center + 100), (short)(bottom - 3));
        else
            MacToolbox.SetRect(BootProgress.BarRect, (short)(center - 100), (short)(bottom - 23), (short)(center + 100), (short)(bottom - 13));

        // Install the "loading"/credits palette (the guarded accessor reads 0 while the
        // cell is unseeded, so the install no-ops).
        Palette.InstallScreenPalette(Palette.CreditsPaletteCTab, 0);

        // Expanding-box animation: start the local rect inset 4px vertically, grow it
        // 1px/pass until it reaches the bar rect.
        short[] box = { BootProgress.BarRect[0], BootProgress.BarRect[1], BootProgress.BarRect[2], BootProgress.BarRect[3] };
        short[] inner = new short[4];
        MacToolbox.InsetRect(box, 0, 4);
        // delayTicksOut replaces the Mac BSS tick cell 0x1008f724 (host bridge).
        int[] delayTicksOut = new int[1];
        while (box[2] <= BootProgress.BarRect[2])
        {
            // FrameRect + interior PaintRect both land on the live screen buffer and must
            // present together each pass; batch them so the host's independently-clocked
            // drain can't present one without the other.
            MacToolbox.BeginDrawBatch();
            MacToolbox.RGBForeColor((uint)BootProgress.BarFrameColor);
            MacToolbox.FrameRect(box);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            inner[0] = box[0]; inner[1] = box[1]; inner[2] = box[2]; inner[3] = box[3];
            MacToolbox.InsetRect(inner, 1, 1);
            MacToolbox.PaintRect(inner);
            MacToolbox.EndDrawBatch();
            MacToolbox.InsetRect(box, 0, -1);
            MacToolbox.Delay(2, delayTicksOut);
        }

        // Restore the screen palette.
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);

        // The decompile calls RedrawCreditsProgressBar UNCONDITIONALLY — do not add a
        // Total!=0 guard: with Total==0 the num/den divide is NaN → (int)NaN==0 →
        // degenerate rect → the fill PaintRect/FrameRect early-return, and the bar
        // CopyBits still flushes (an empty bar), exactly as the original ran it.
        RedrawCreditsProgressBar.Run();
    }
}
