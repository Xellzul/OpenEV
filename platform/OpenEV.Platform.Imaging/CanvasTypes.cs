using System;

namespace OpenEV.Platform.Imaging;

// Integer rectangle (left, top, width, height) — the managed stand-in for the
// MonoGame Rectangle the draw closures used. Top-down screen space, matching
// Rgba8Image row order.
public readonly struct RectI
{
    public readonly int X, Y, Width, Height;

    public RectI(int x, int y, int width, int height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"RectI({X},{Y},{Width},{Height})";
}

// Straight (non-premultiplied) RGBA8 colour — the managed stand-in for the
// MonoGame Color the draw closures used. Channel order matches Rgba8Image
// (R,G,B,A bytes).
public readonly struct RgbaColor : IEquatable<RgbaColor>
{
    public readonly byte R, G, B, A;

    public RgbaColor(byte r, byte g, byte b, byte a)
    {
        R = r; G = g; B = b; A = a;
    }

    public RgbaColor(byte r, byte g, byte b) : this(r, g, b, 255) { }

    public static RgbaColor White => new(255, 255, 255, 255);
    public static RgbaColor Black => new(0, 0, 0, 255);
    public static RgbaColor Transparent => new(0, 0, 0, 0);

    /// True for fully-opaque white — the common "no tint" case, used to skip
    /// the per-channel multiply in Blit.
    public bool IsOpaqueWhite => R == 255 && G == 255 && B == 255 && A == 255;

    /// Scale every channel (incl. alpha) by `f` in [0,1]. Reproduces MonoGame's
    /// `Color.White * f` used for the screen-fade composite (alpha scales too,
    /// so the image blends toward the cleared FadeColor as f → 0).
    public RgbaColor Scale(float f)
    {
        if (f <= 0f) return Transparent;
        if (f >= 1f) return this;
        return new RgbaColor(
            (byte)(R * f), (byte)(G * f), (byte)(B * f), (byte)(A * f));
    }

    public bool Equals(RgbaColor o) => R == o.R && G == o.G && B == o.B && A == o.A;
    public override bool Equals(object? o) => o is RgbaColor c && Equals(c);
    public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
    public override string ToString() => $"RgbaColor({R},{G},{B},{A})";
}
