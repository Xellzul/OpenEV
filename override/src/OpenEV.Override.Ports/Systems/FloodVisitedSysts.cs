using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// FUN_1005cd50 — clears the per-system "visited" flags and seeds a connectivity
// flood from startSyst, capping the recursive walk at depthLimit hyperspace hops
// (MarkConnectedSystemsRecursive stops once its hop count exceeds this cap).
// Decompile: EV Override-11.c lines 38425-38447.
public static class FloodVisitedSysts
{
    public static void Run(short startSyst, short depthLimit)
    {
        for (int systIndex = 0; systIndex < SystTable.Count; systIndex++)
        {
            GalaxyMapGlobals.VisitedSystemFlags[systIndex] = 0;
        }

        GalaxyMapGlobals.FloodDepthCursor = depthLimit;
        MarkConnectedSystemsRecursive.Run(startSyst, 0);
    }
}
