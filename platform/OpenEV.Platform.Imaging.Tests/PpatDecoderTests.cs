using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

public class PpatDecoderTests
{
    private static (byte r, byte g, byte b, byte a) Px(Rgba8Image img, int x, int y)
    {
        int o = (y * img.Width + x) * 4;
        return (img.Pixels[o], img.Pixels[o + 1], img.Pixels[o + 2], img.Pixels[o + 3]);
    }

    [Fact]
    public void Decode_TooSmall_ReturnsNull()
    {
        Assert.Null(PpatDecoder.Decode(new byte[10]));
    }

    [Fact]
    public void Decode_OldOneBitPattern_ExpandsTo8x8BlackWhite()
    {
        // patType 0 → the 8-byte pat1Data at +20 is an 8×8 1-bit pattern (bit set = black).
        var d = new byte[28];
        // patType = 0 (bytes 0..1), patMap/patData = 0 → 1-bit path.
        d[20] = 0xFF;  // row 0: all bits set → all black
        d[21] = 0x00;  // row 1: no bits set → all white
        for (int y = 2; y < 8; y++) d[20 + y] = 0xAA;

        var img = PpatDecoder.Decode(d);
        Assert.NotNull(img);
        Assert.Equal(8, img!.Width);
        Assert.Equal(8, img.Height);
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), Px(img, x, 0));        // black row
            Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Px(img, x, 1));  // white row
        }
    }
}

public class CanvasFillPatternTests
{
    private static (byte r, byte g, byte b, byte a) Px(Rgba8Image img, int x, int y)
    {
        int o = (y * img.Width + x) * 4;
        return (img.Pixels[o], img.Pixels[o + 1], img.Pixels[o + 2], img.Pixels[o + 3]);
    }

    [Fact]
    public void FillPattern_Tiles2x2AcrossTarget()
    {
        var pat = new Rgba8Image(2, 2);
        pat.SetPixel(0, 0, 10, 0, 0, 255);
        pat.SetPixel(1, 0, 0, 20, 0, 255);
        pat.SetPixel(0, 1, 0, 0, 30, 255);
        pat.SetPixel(1, 1, 40, 40, 40, 255);

        var dst = new Rgba8Image(4, 4);
        new Canvas(dst).FillPattern(new RectI(0, 0, 4, 4), pat);

        // Tiling repeats every 2px, phase-anchored to the rect origin.
        Assert.Equal(((byte)10, (byte)0, (byte)0, (byte)255), Px(dst, 0, 0));
        Assert.Equal(((byte)0, (byte)20, (byte)0, (byte)255), Px(dst, 1, 0));
        Assert.Equal(((byte)10, (byte)0, (byte)0, (byte)255), Px(dst, 2, 0));  // wrapped
        Assert.Equal(((byte)0, (byte)0, (byte)30, (byte)255), Px(dst, 0, 1));
        Assert.Equal(((byte)40, (byte)40, (byte)40, (byte)255), Px(dst, 3, 3)); // (1,1) of tile
    }

    [Fact]
    public void FillPattern_ClipsToTarget_NoThrow()
    {
        var pat = new Rgba8Image(2, 2);
        pat.SetPixel(0, 0, 1, 2, 3, 255);
        var dst = new Rgba8Image(4, 4);
        // Rect extends past the target — must clip, not throw.
        new Canvas(dst).FillPattern(new RectI(-3, -3, 100, 100), pat);
        Assert.Equal((byte)255, Px(dst, 0, 0).a);
    }
}
