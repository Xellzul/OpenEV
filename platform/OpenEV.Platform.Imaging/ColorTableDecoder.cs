using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.Imaging;

public static class ColorTableDecoder
{
    public static ColorTable Read(ref BigEndianSpanReader r)
    {
        int ctSeed = r.ReadInt32();
        short ctFlags = r.ReadInt16();
        short ctSize = r.ReadInt16();
        int count = ctSize + 1;

        var colors = new ColorTable.Rgba[count];
        for (int i = 0; i < count; i++)
        {
            short value = r.ReadInt16();
            ushort cr = r.ReadUInt16();
            ushort cg = r.ReadUInt16();
            ushort cb = r.ReadUInt16();
            colors[i] = new ColorTable.Rgba(
                Gamma.Correct((byte)(cr >> 8)),
                Gamma.Correct((byte)(cg >> 8)),
                Gamma.Correct((byte)(cb >> 8)),
                255);
        }
        return new ColorTable(colors);
    }
}
