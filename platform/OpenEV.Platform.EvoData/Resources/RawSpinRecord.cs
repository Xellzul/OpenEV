using OpenEV.Platform.ResourceFork;

namespace OpenEV.Platform.EvoData.Resources;

public record RawSpinRecord(
    short SpritesId, short MasksId,
    short XSize, short YSize,
    short XTiles, short YTiles)
{
    public static RawSpinRecord Load(byte[] data)
    {
        var r = new BigEndianSpanReader(data);
        return new RawSpinRecord(
            r.ReadInt16(), r.ReadInt16(),
            r.ReadInt16(), r.ReadInt16(),
            r.ReadInt16(), r.ReadInt16());
    }
}
