namespace OpenEV.Platform.Imaging;

public sealed class ColorTable
{
    public readonly struct Rgba { public readonly byte R, G, B, A; public Rgba(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; } }
    private readonly Rgba[] _colors;
    public ColorTable(Rgba[] colors) { _colors = colors; }
    public Rgba Get(int index)
    {
        if (index < 0 || index >= _colors.Length) return new Rgba(0, 0, 0, 255);
        return _colors[index];
    }
    public int Count => _colors.Length;
}
