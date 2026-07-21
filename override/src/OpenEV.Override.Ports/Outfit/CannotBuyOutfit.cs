using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005b388 — can the player NOT buy one more of outfit `outfitIndexArg`?
// Returns 1 = cannot buy, 0 = can buy, and passes the arg through unchanged when
// it is -1 (no outfit selected). "Cannot buy" when the per-outfit maximum is
// reached, or the ship's gun / turret slots are already full for that weapon kind.
// Decompile: EV Override-11.c lines 37612-37653.
public static class CannotBuyOutfit
{
    public static int Run(int outfitIndexArg)
    {
        short outfitIndex = (short)outfitIndexArg;
        if (outfitIndex == -1)
        {
            return outfitIndexArg;
        }

        OutfitRec outfit = OutfitTable.Outfits[outfitIndex];

        // Already own the per-outfit maximum → cannot buy another.
        if (OwnedOutfitGrid.Store[outfitIndex] >= outfit.MaximumCount)
        {
            return 1;
        }

        // Tally how many gun-type and turret-type outfits the player owns across every slot.
        short gunSlotsUsed = 0;
        short turretSlotsUsed = 0;
        foreach (OutfitRec o in OutfitTable.Outfits)
        {
            short owned = OwnedOutfitGrid.Store[o.Index];
            if ((o.Flags & OutfFlags.FixedGun) != 0) gunSlotsUsed += owned;
            if ((o.Flags & OutfFlags.Turret) != 0) turretSlotsUsed += owned;
        }

        ShipClassRecord playerClass = GameData.ShipClasses[GameData.Player.ShipClass];

        // Gun-type outfit but the ship's gun slots are full → cannot buy.
        if ((outfit.Flags & OutfFlags.FixedGun) != 0 && playerClass.MaxGun <= gunSlotsUsed)
        {
            return 1;
        }

        // Turret-type outfit but the ship's turret slots are full → cannot buy.
        if ((outfit.Flags & OutfFlags.Turret) != 0 && turretSlotsUsed >= playerClass.MaxTur)
        {
            return 1;
        }

        return 0;
    }
}
