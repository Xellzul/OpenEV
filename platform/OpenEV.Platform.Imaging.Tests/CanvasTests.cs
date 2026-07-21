using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

public class CanvasTests
{
    private static (byte r, byte g, byte b, byte a) Px(Rgba8Image img, int x, int y)
    {
        int o = (y * img.Width + x) * 4;
        return (img.Pixels[o], img.Pixels[o + 1], img.Pixels[o + 2], img.Pixels[o + 3]);
    }

    // QuickDraw RGB primitives stamp the alpha provenance tag (254) instead of 255
    // (Canvas.RgbDrawnTag — the cloak screen-palette remap dispatches on it).
    private const byte Tag = Canvas.RgbDrawnTag;

    [Fact]
    public void FillRect_Opaque_SetsExactPixelsAndLeavesOthers()
    {
        var img = new Rgba8Image(4, 4);
        new Canvas(img).FillRect(new RectI(1, 1, 2, 2), new RgbaColor(10, 20, 30, 255));

        Assert.Equal(((byte)10, (byte)20, (byte)30, Tag), Px(img, 1, 1));
        Assert.Equal(((byte)10, (byte)20, (byte)30, Tag), Px(img, 2, 2));
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), Px(img, 0, 0)); // untouched
    }

    [Fact]
    public void FillRect_OutOfBounds_ClipsWithoutThrowing()
    {
        var img = new Rgba8Image(4, 4);
        new Canvas(img).FillRect(new RectI(-5, -5, 100, 100), RgbaColor.White);
        Assert.Equal(((byte)255, (byte)255, (byte)255, Tag), Px(img, 0, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, Tag), Px(img, 3, 3));
    }

    [Fact]
    public void Blit_OpaqueEqualSize_IsExactCopy()
    {
        var src = new Rgba8Image(2, 1);
        src.SetPixel(0, 0, 255, 0, 0, 255);
        src.SetPixel(1, 0, 0, 255, 0, 255);
        var dst = new Rgba8Image(2, 1);
        new Canvas(dst).Blit(src, new RectI(0, 0, 2, 1), RgbaColor.White);

        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Px(dst, 0, 0));
        Assert.Equal(((byte)0, (byte)255, (byte)0, (byte)255), Px(dst, 1, 0));
    }

    [Fact]
    public void Blit_TransparentSource_LeavesDestUnchanged()
    {
        var dst = new Rgba8Image(1, 1);
        new Canvas(dst).FillRect(new RectI(0, 0, 1, 1), new RgbaColor(200, 0, 0, 255));
        var src = new Rgba8Image(1, 1); // all zero → a == 0
        new Canvas(dst).Blit(src, new RectI(0, 0, 1, 1), RgbaColor.White);
        Assert.Equal(((byte)200, (byte)0, (byte)0, Tag), Px(dst, 0, 0));   // FillRect tag survives a transparent blit
    }

    [Fact]
    public void Blit_NearestNeighbor_Scales2x()
    {
        var src = new Rgba8Image(2, 1);
        src.SetPixel(0, 0, 255, 0, 0, 255);
        src.SetPixel(1, 0, 0, 0, 255, 255);
        var dst = new Rgba8Image(4, 1);
        new Canvas(dst).Blit(src, new RectI(0, 0, 4, 1), RgbaColor.White);

        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Px(dst, 0, 0));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Px(dst, 1, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Px(dst, 2, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Px(dst, 3, 0));
    }

    [Fact]
    public void InvertRect_SingleInvertNegatesAndForcesOpaque()
    {
        var img = new Rgba8Image(1, 1);
        img.SetPixel(0, 0, 10, 20, 30, 128);
        new Canvas(img).InvertRect(new RectI(0, 0, 1, 1));
        Assert.Equal(((byte)245, (byte)235, (byte)225, Tag), Px(img, 0, 0));
    }

    [Fact]
    public void InvertRect_AppliedTwice_IsIdentityForOpaquePixels()
    {
        var img = new Rgba8Image(3, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                img.SetPixel(x, y, (byte)(x * 30), (byte)(y * 40), (byte)(x + y), 255);
        var before = (byte[])img.Pixels.Clone();

        var c = new Canvas(img);
        c.InvertRect(new RectI(0, 0, 3, 3));
        c.InvertRect(new RectI(0, 0, 3, 3));

        // rgb round-trips exactly; alpha now carries the RGB-drawn tag.
        for (int i = 0; i < before.Length; i += 4)
        {
            Assert.Equal(before[i], img.Pixels[i]);
            Assert.Equal(before[i + 1], img.Pixels[i + 1]);
            Assert.Equal(before[i + 2], img.Pixels[i + 2]);
            Assert.Equal(Tag, img.Pixels[i + 3]);
        }
    }

    [Fact]
    public void Blit_FadeTint_HalfBrightnessOverBlack()
    {
        var dst = new Rgba8Image(1, 1);
        new Canvas(dst).FillRect(new RectI(0, 0, 1, 1), RgbaColor.Black);
        var src = new Rgba8Image(1, 1);
        src.SetPixel(0, 0, 255, 255, 255, 255);

        new Canvas(dst).Blit(src, new RectI(0, 0, 1, 1), RgbaColor.White.Scale(0.5f));

        // ts = 255*127/255 = 127 (rgb,a). over the tagged black (a=254): rgb = 127,
        // a = 127 + 254*128/255 = 254.
        var (r, g, b, a) = Px(dst, 0, 0);
        Assert.InRange(r, (byte)126, (byte)128);
        Assert.InRange(g, (byte)126, (byte)128);
        Assert.InRange(b, (byte)126, (byte)128);
        Assert.Equal((byte)254, a);
    }

    [Fact]
    public void StrokeLine_ZeroLength_PaintsPenDot()
    {
        var img = new Rgba8Image(4, 4);
        new Canvas(img).StrokeLine(1, 1, 1, 1, 2, 2, RgbaColor.White);
        Assert.Equal(((byte)255, (byte)255, (byte)255, Tag), Px(img, 1, 1));
        Assert.Equal(((byte)255, (byte)255, (byte)255, Tag), Px(img, 2, 2));
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), Px(img, 0, 0));
    }

    [Fact]
    public void StrokeLine_Horizontal_DrawsContinuousRun()
    {
        var img = new Rgba8Image(5, 1);
        new Canvas(img).StrokeLine(0, 0, 4, 0, 1, 1, RgbaColor.White);
        for (int x = 0; x < 5; x++)
            Assert.Equal(Tag, Px(img, x, 0).a);
    }

    [Fact]
    public void StrokeLine_ThickHorizontal_BandHangsBelowTheLine()
    {
        // QuickDraw pen semantics: the 2×2 pen hangs below-right of the pen point,
        // so a horizontal stroke at y=1 covers rows 1..2 (never the row above).
        var img = new Rgba8Image(6, 4);
        new Canvas(img).StrokeLine(0, 1, 4, 1, 2, 2, RgbaColor.White);
        for (int x = 0; x < 6; x++)
        {
            Assert.Equal((byte)0, Px(img, x, 0).a);
            Assert.Equal(x <= 5 ? Tag : (byte)0, Px(img, x, 1).a);   // 0..4 path + 1px pen overhang at 5
            Assert.Equal(Px(img, x, 1).a, Px(img, x, 2).a);
            Assert.Equal((byte)0, Px(img, x, 3).a);
        }
    }

    [Fact]
    public void StrokeLine_ThickDiagonal_StampsPenRectAtEveryPathPixel()
    {
        // 45° diagonal with a 2×2 pen: every path pixel (k,k) carries a full 2×2
        // stamp — no notches along the band (the union contains (k,k),(k+1,k),
        // (k,k+1),(k+1,k+1) for every k).
        var img = new Rgba8Image(8, 8);
        new Canvas(img).StrokeLine(0, 0, 5, 5, 2, 2, RgbaColor.White);
        for (int k = 0; k <= 5; k++)
        {
            Assert.Equal(Tag, Px(img, k, k).a);
            Assert.Equal(Tag, Px(img, k + 1, k).a);
            Assert.Equal(Tag, Px(img, k, k + 1).a);
            Assert.Equal(Tag, Px(img, k + 1, k + 1).a);
        }
        Assert.Equal((byte)0, Px(img, 3, 0).a);   // off-band stays clear
        Assert.Equal((byte)0, Px(img, 0, 3).a);
    }
}
