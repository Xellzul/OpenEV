using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.GalaxyMap;

// FUN_1005cf28 (EV Override-11.c lines 38477-38509)
public static class MarkGalaxyMapClustersForSyst
{
    public static void Run(short systemIndex)
    {
        short[] rect = new short[4];
        var syst = SystTable.Store[systemIndex];

        for (short clusterIndex = 0; clusterIndex < MapNebulaTable.Count; clusterIndex++)
        {
            var cluster = MapNebulaTable.Store[clusterIndex];
            if (cluster.Charted == 0 && cluster.Width > 0 && cluster.Height > 0)
            {
                MacToolbox.SetRect(rect, cluster.X, cluster.Y,
                                   (short)(cluster.X + cluster.Width),
                                   (short)(cluster.Y + cluster.Height));
                MacToolbox.InsetRect(rect, 8, 8);
                int systemPoint = ((syst.YPos & 0xffff) << 16) | (syst.XPos & 0xffff);
                if (MacToolbox.PtInRect(systemPoint, rect))
                {
                    cluster.Charted = 1;
                }
            }
        }
    }
}
