namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;

// Port of FUN_1005b014 (EV Override-11.c 37521-37550) — how many days a hyperjump takes:
// 1 base, +1 if the ship's class mass exceeds 99, +1 more if it exceeds 199, plus any
// HyperJumpDays-granting outfits the player owns (player ship only). Never fewer than 1.
// Sole caller is TickShipAI's hyperjump-arrival block, which runs TickWorldDailyEvents
// this many times (TickShipAI.cs ~1070).
public static class EffectiveHyperJumpDays
{
    public static int Run(ShipRec ship)
    {
        int jumpDays = 1;
        short mass = Core.Model.GameData.ShipClasses[ship.ShipClass].Mass;
        if (mass > 99)
            jumpDays = 2;
        if (mass > 199)
            jumpDays++;

        if (ship.SlotIndex == 0)
        {
            for (short outfitIndex = 0; outfitIndex < OutfitTable.Count; outfitIndex++)
            {
                var outfit = OutfitTable.Outfits[outfitIndex];
                for (short slotIndex = 0; slotIndex < OutfitRecord.ModBankCount; slotIndex++)
                {
                    if (outfit.ModType[slotIndex] == OutfitModType.HyperJumpDays &&
                        OwnedOutfitGrid.Store[outfitIndex] > 0)
                    {
                        jumpDays += outfit.ModValue[slotIndex] * OwnedOutfitGrid.Store[outfitIndex];
                    }
                }
            }
        }

        if ((short)jumpDays < 1)
            jumpDays = 1;
        return jumpDays;
    }
}
