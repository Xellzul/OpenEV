using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005e948 — the player ship's resale value: a fraction of the ship-class
// price plus a fraction of every owned, non-persistent outfit's value
// (price x quantity). Returns the rounded-down credit total.
//
// The decompile expresses each int->double conversion with the PowerPC
// float cast idiom; that whole pattern is just (double)(int)v, so it is
// written directly here.
// Decompile: EV Override-11.c lines 39335-39363.
public static class ComputeShipResaleValue
{
    public static uint Run()
    {
        ShipClassRecord playerClass = GameData.ShipClasses[GameData.Player.ShipClass];

        uint resaleValue = (uint)(MathConstants.Quarter * playerClass.Cost);

        foreach (OutfitRec outfit in OutfitTable.Outfits)
        {
            short owned = OwnedOutfitGrid.Store[outfit.Index];
            // Skip outfits the player doesn't own or that are persistent (no resale).
            if (owned > 0 && outfit.PersistentFlagSet == 0)
            {
                double outfitValue = outfit.Cost * owned;
                // Truncated back to uint EVERY iteration, not just once at the end —
                // matches the decompile's per-iteration fctiwz; don't hoist the cast
                // out of the loop, it changes the accumulated result.
                resaleValue = (uint)(CommodityPricing.ResaleValueScale * outfitValue + (double)(int)resaleValue);
            }
        }

        return resaleValue;
    }
}
