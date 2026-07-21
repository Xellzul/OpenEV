using System;
using System.IO;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

public class SoftwareFontTests
{
    // Locate any real TTF so the test runs on a normal dev box (Win/Mac/Linux).
    // Returns null if none found (CI without fonts) → tests become no-ops.
    private static byte[]? LoadAnyFont()
    {
        string[] candidates =
        {
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\verdana.ttf",
            "/Library/Fonts/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        };
        foreach (var p in candidates)
            if (File.Exists(p)) return File.ReadAllBytes(p);
        return null;
    }

    [Fact]
    public void MeasureWidth_NonEmptyText_IsPositiveAndMonotonic()
    {
        var ttf = LoadAnyFont();
        if (ttf is null) return; // no font available — skip
        var font = new SoftwareFont(ttf);

        int wA = font.MeasureWidth("A", 24);
        int wAB = font.MeasureWidth("AB", 24);
        Assert.True(wA > 0, "single glyph width should be positive");
        Assert.True(wAB > wA, "two glyphs should be wider than one");
        Assert.Equal(0, font.MeasureWidth("", 24));
    }

    [Fact]
    public void DrawText_RendersVisiblePixels()
    {
        var ttf = LoadAnyFont();
        if (ttf is null) return;
        var font = new SoftwareFont(ttf);

        var img = new Rgba8Image(120, 40);
        var canvas = new Canvas(img);
        canvas.Clear(RgbaColor.Black);
        font.DrawText(canvas, "Test", 4, 4, RgbaColor.White, 24);

        // Count pixels that moved away from pure black — glyphs must have drawn.
        int lit = 0;
        for (int i = 0; i < img.Pixels.Length; i += 4)
            if (img.Pixels[i] > 16) lit++;
        Assert.True(lit > 20, $"expected rendered glyph pixels, got {lit}");
    }

    [Fact]
    public void DrawText_TintColorsTheGlyphs()
    {
        var ttf = LoadAnyFont();
        if (ttf is null) return;
        var font = new SoftwareFont(ttf);

        var img = new Rgba8Image(120, 40);
        var canvas = new Canvas(img);
        canvas.Clear(RgbaColor.Black);
        font.DrawText(canvas, "X", 4, 4, new RgbaColor(255, 0, 0, 255), 28);

        // Some pixel should be reddish (R high, G/B low) — confirms tint multiply.
        bool reddish = false;
        for (int i = 0; i < img.Pixels.Length; i += 4)
            if (img.Pixels[i] > 120 && img.Pixels[i + 1] < 80 && img.Pixels[i + 2] < 80) { reddish = true; break; }
        Assert.True(reddish, "tinted glyph should produce red pixels");
    }
}
