using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1005a940 from EV Override-11.c lines 37342-37446.
//
// Effective ECM-jamming level against a SHIP for one jam type (0..3):
//   * the player (ship.SlotIndex == 0): scan all outfits for the Jammer mod
//     (type 10) with owned count > 0 — ModValue bit (1<<jamType) = level 1
//     (first loop), bit (0x10<<jamType) = level 2 (second loop);
//   * an NPC with a pers link (ship.Govt != -1): the government's InherentJamming
//     flag word, same bit layout; flag bit 0x100 with ship.AiBehaviorType in {1,2}
//     downgrades the level by 1 (clamped at 0).
// Level 2 always holds; level 1 only on a coin flip (rng(2)==0); else 0.
// Re-derived onto the typed managed OutfitTable/OwnedOutfitGrid stores + the
// ShipRecord. jamType is only ever 0..3, so the original's four explicit
// per-type bit branches fold to (1<<jamType) / (0x10<<jamType).
public static class JammingLevel
{
    public static int Run(ShipRecord ship, short jamType)
    {
        int jamLevel = 0;
        if (ship.SlotIndex == 0)
        {
            for (short i = 0; i < OutfitTable.Count; i++)
            {
                for (short bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                {
                    if (OutfitTable.Store[i].ModType[bank] == OutfitModType.Jammer &&
                       0 < OwnedOutfitGrid.Store[i] &&
                       (OutfitTable.Store[i].ModValue[bank] & (1 << jamType)) != 0)
                    {
                        jamLevel = 1;
                    }
                }
            }
            for (short i = 0; i < OutfitTable.Count; i++)
            {
                for (short bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                {
                    if (OutfitTable.Store[i].ModType[bank] == OutfitModType.Jammer &&
                       0 < OwnedOutfitGrid.Store[i] &&
                       (OutfitTable.Store[i].ModValue[bank] & (0x10 << jamType)) != 0)
                    {
                        jamLevel = 2;
                    }
                }
            }
        }
        else if (ship.Govt != -1)
        {
            int persFlags = (ushort)GameData.Governments[ship.Govt].InherentJamming;
            if ((persFlags & (1 << jamType)) != 0)
            {
                jamLevel = 1;
            }
            if ((persFlags & (0x10 << jamType)) != 0)
            {
                jamLevel = 2;
            }
            if ((ship.AiBehaviorType == ShipAiType.WimpyTrader || ship.AiBehaviorType == ShipAiType.BraveTrader) && (persFlags & 0x100) != 0)
            {
                jamLevel -= 1;
                if ((short)jamLevel < 0)
                {
                    jamLevel = 0;
                }
            }
        }
        if ((short)jamLevel != 2)
        {
            if ((short)jamLevel == 1 && (short)SeedEvoRng.Run(2) == 0)
            {
                jamLevel = 1;
            }
            else
            {
                jamLevel = 0;
            }
        }
        return jamLevel;
    }
}
