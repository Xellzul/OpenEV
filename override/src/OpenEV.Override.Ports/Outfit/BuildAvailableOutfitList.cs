using System;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1005fcd4 (EV Override-11.c lines 39949-40003). Builds the outfitter's
// available-outfit row map (outfit index -> display row, -1 = empty) for the spob the
// player landed on.
public static class BuildAvailableOutfitList
{
    public static void Run(SpobRecord spob)
    {
        short[] rows = OutfitShopState.AvailableRowIndex;
        Array.Fill(rows, (short)-1, 0, OutfitShopState.RowCount);

        short outputCount = 0;
        for (short outfitIndex = 0; outfitIndex < OutfitTable.Count; outfitIndex++)
        {
            if (IsAvailableHere(spob, OutfitTable.Store[outfitIndex]) || WorldState.CheatShowAll != 0)
            {
                rows[outputCount] = outfitIndex;
                outputCount++;
            }
        }
    }

    private static bool IsAvailableHere(SpobRecord spob, OutfitRecord outfit)
    {
        // Tech gate: the spob's tech level must reach the outfit, OR one of its three
        // special-tech slots must match the outfit tech exactly.
        bool avail = spob.TechLevel >= outfit.TechLevel
                     || Array.IndexOf(spob.SpecialTech, outfit.TechLevel) >= 0;

        // Availability-bit gate. < 1000: the control bit must be SET (bit == -1 = ungated).
        // >= 1000: the (bit-1000) control bit must be CLEAR — a SET bit hides the outfit.
        short bit = outfit.AvailabilityBit;
        if (bit < 1000)
        {
            if (bit != -1 && ControlBits.Get(bit) == 0)
            {
                avail = false;
            }
        }
        else if (ControlBits.Get(bit - 1000) != 0)
        {
            avail = false;
        }
        return avail;
    }
}
