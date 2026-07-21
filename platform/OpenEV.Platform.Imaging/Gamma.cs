using System;

namespace OpenEV.Platform.Imaging;

// Mac Standard Gamma DAC-ramp lookup (encode exponent 1.8/2.61). EVO's artwork and
// palettes were authored for a classic-Mac display; the Mac's video DAC applied a
// hardware LUT of v^(1.8/2.61) so its native-γ~2.61 CRT netted the famous 1.8. The
// same raw values sent to a modern Windows/sRGB monitor (~2.2) render visibly darker.
// This LUT applies the SAME DAC ramp the Mac hardware did — measured 2026-07-02
// against SheepShaver screenshots (fitted exponent 0.693-0.706 across five surfaces
// vs 1.8/2.61 = 0.690, every sample within ~2/255), and confirmed by the user as the
// look to match. (The previous 1.8/2.2 ratio reproduced a real Mac CRT's NET 1.8
// appearance instead; user chose the SheepShaver/DAC-ramp look, which is brighter on
// a modern panel.) Applied two ways:
//   1. during pixel decode (RleDecoder / QuickDrawBitmapDecoder / ColorTableDecoder)
//      for sprites, PICTs and CLUTs, and
//   2. to colours painted DIRECTLY through QuickDraw (the RgbaColor overload below),
//      so HUD/radar/dialog colours match the brightened art beside them.
// This is an INTENTIONAL porting-layer addition (not in the decompiled source) — its
// goal is faithfulness to the original's APPEARANCE, not its raw framebuffer bytes.
public static class Gamma
{
    private const double ClassicMacGamma = 1.8;
    private const double MacCrtNativeGamma = 2.61;
    private static readonly byte[] LookupTable = new byte[256];
    private static readonly byte[] InverseTable = new byte[256];

    static Gamma()
    {
        for (int i = 0; i < 256; i++)
        {
            double corrected = Math.Pow(i / 255.0, ClassicMacGamma / MacCrtNativeGamma) * 255.0;
            LookupTable[i] = (byte)Math.Clamp(Math.Round(corrected), 0, 255);
            double raw = Math.Pow(i / 255.0, MacCrtNativeGamma / ClassicMacGamma) * 255.0;
            InverseTable[i] = (byte)Math.Clamp(Math.Round(raw), 0, 255);
        }
    }
    public static byte Correct(byte value) => LookupTable[value];

    // Analytic inverse of the DAC ramp: display byte -> the raw Mac framebuffer value that
    // Correct() would brighten to it. Used where the host must reason in the Mac's own
    // colour space about pixels ALREADY corrected into the buffer (the cloak screen-palette
    // remap does its nearest-CLUT-entry matching on Mac values, like the Color Manager's
    // inverse table did).
    public static byte Uncorrect(byte value) => InverseTable[value];

    // The same DAC-ramp correction for a fully-formed colour, alpha preserved. Used by the
    // MacToolbox QuickDraw colour entry points (the ForeColor/BackColor constant map,
    // RGBForeColor, and the screen FadeColor) so colours drawn DIRECTLY to screen —
    // which never pass through a pixel decoder — get the same brightening as the
    // decoded sprite/PICT art. Only mid-channel values shift (pure 0/255 primaries are
    // curve fixed points), so the pure red/blue/cyan/etc. QuickDraw constants are
    // unchanged; greens and the RGBForeColor UI tints brighten to match the art.
    public static RgbaColor Correct(RgbaColor c)
        => new RgbaColor(Correct(c.R), Correct(c.G), Correct(c.B), c.A);
}
