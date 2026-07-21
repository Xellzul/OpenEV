using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Systems;

// Port of FUN_1005e2d8 (EV Override-11.c lines 39114-39135): true when systIndex
// has at least one stellar slot pointing at a spob that isn't flagged Uninhabited.
public static class HasVisibleStellars
{
    public static bool Run(short systIndex)
    {
        short visibleCount = 0;
        foreach (short spobIdx in SystTable.Store[systIndex].StellarLink)
        {
            if (spobIdx != -1 && (GameData.Spobs[spobIdx].Flags & (int)SpobFlags.Uninhabited) == 0)
            {
                visibleCount = (short)(visibleCount + 1);
            }
        }
        return visibleCount != 0;
    }
}
