using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.GalaxyMap;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_1005cdc8 (EV Override-11.c lines 38448-38476). Recursive flood-fill:
// marks each unvisited, depth-in-range system as visited, tags its map clusters,
// then recurses into its shown hyperlink neighbors (depth capped by FloodDepthCursor).
public static class MarkConnectedSystemsRecursive
{
    public static void Run(int systemIndex, int depth)
    {
        short sysIdx = (short)systemIndex;

        if ((short)depth <= GalaxyMapGlobals.FloodDepthCursor &&
            GalaxyMapGlobals.VisitedSystemFlags[sysIdx] == 0)
        {
            GalaxyMapGlobals.VisitedSystemFlags[sysIdx] = 1;
            MarkGalaxyMapClustersForSyst.Run(sysIdx);
            if (SystTable.Store[sysIdx].Visited < 2)
            {
                SystTable.Store[sysIdx].Visited = 2;
            }
            for (short childSlot = 0; childSlot < SystRecord.HyperLinkCount; childSlot = (short)(childSlot + 1))
            {
                if (SystTable.Store[sysIdx].HyperLink[childSlot] != -1 &&
                    SystTable.Store[SystTable.Store[sysIdx].HyperLink[childSlot]].ShownFlag != 0)
                {
                    Run(SystTable.Store[sysIdx].HyperLink[childSlot], depth + 1);
                }
            }
        }
    }
}
