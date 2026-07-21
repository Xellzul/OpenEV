using System;

namespace OpenEV.Platform.Imaging;

// Plain RGBA8 pixel buffer (R,G,B,A bytes, top-down rows). Compatible with
// MonoGame's Texture2D.SetData<byte> when texture format is SurfaceFormat.Color.
public sealed class Rgba8Image
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public Rgba8Image(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }


    public void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        int o = (y * Width + x) * 4;
        Pixels[o + 0] = r;
        Pixels[o + 1] = g;
        Pixels[o + 2] = b;
        Pixels[o + 3] = a;
    }
}
