// Port of FUN_100419cc (EV Override-11.c lines 27029-27083).
// Called by AdvanceCreditsScrollProgress + AnimateBootProgressBar.
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Graphics;

public static class RedrawCreditsProgressBar
{
    public static void Run()
    {
        short[] barRect = BootProgress.BarRect;

        // Install the credits palette (the guarded accessor reads 0 while the cell
        // is unseeded, so the install no-ops).
        Palette.InstallScreenPalette(Palette.CreditsPaletteCTab, 0);

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // The colour cells hold RGBColor records (seeded by AnimateBootProgressBar);
        // the decompile passes each by address to RGBForeColor.
        MacToolbox.RGBForeColor((uint)BootProgress.BarFrameColor);
        MacToolbox.FrameRect(barRect);

        // Working copy of the rect, inset by 1.
        short[] inner = { barRect[0], barRect[1], barRect[2], barRect[3] };
        MacToolbox.InsetRect(inner, 1, 1);

        // fill edge = scale * (num / den) + insetLeft. The decompile's
        // float cast term is the PPC i2d
        // idiom (bias @0x10082098) == (double)left.
        int fillEdge = (int)(198.0 /* scale, *(toc-0x65d0) @0x10082090 */
                      * (BootProgress.Current / BootProgress.Total)
                      + (double)inner[1]);
        short clampedFill = (short)fillEdge;
        short origRight = barRect[3];
        if (origRight <= clampedFill) clampedFill = (short)(origRight - 1);
        inner[3] = clampedFill;

        // Filled portion.
        MacToolbox.RGBForeColor((uint)BootProgress.BarFillColor);
        MacToolbox.InsetRect(inner, 1, 1);
        MacToolbox.PaintRect(inner);

        // Border (inset back out by 1).
        MacToolbox.RGBForeColor((uint)BootProgress.BarMidColor);
        MacToolbox.InsetRect(inner, -1, -1);
        MacToolbox.FrameRect(inner);
        MacToolbox.ForeColor(QuickDrawColor.Black);

        // Unfilled remainder: left = fill edge, right = origRight-1; paint it.
        inner[1] = inner[3];
        inner[3] = (short)(origRight - 1);
        MacToolbox.PaintRect(inner);

        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // dst = screen key (*(toc-0x7958) is the 0x10080d08 slot).
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, GlobalState.ActivePortPixmap + 2, barRect, barRect, 0, 0);

        // Restore the screen palette.
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
    }
}
