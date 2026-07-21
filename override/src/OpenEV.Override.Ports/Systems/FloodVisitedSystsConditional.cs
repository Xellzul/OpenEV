using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// FUN_1005c5cc — clears the per-system "visited" flags and, when floodFlag is -1,
// kicks off a kill-impact propagation from startSyst (srcGovt/column feed
// PropagateSystemKillImpact's government-relation weighting and legal-status column).
// Decompile: EV Override-11.c lines 38243-38270.
public static class FloodVisitedSystsConditional
{
    public static void Run(short startSyst, short srcGovt, short column, short floodFlag)
    {
        System.Array.Clear(GalaxyMapGlobals.VisitedSystemFlags, 0, SystTable.Count);

        if (floodFlag == -1)
        {
            // MathConstants.One stands in for the impact-seed double _DAT_10082280
            // (dumped PEF data-seg constant; see MathConstants.cs).
            PropagateSystemKillImpact.Run(MathConstants.One, startSyst, srcGovt, column);
        }
    }
}
