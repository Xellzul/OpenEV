using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_10014ae8 (EV Override-11.c lines 10200-10398) — prepares a ship
// BOARDING session against the player's target: rolls the bribe pool
// from the pers mission credits (or the target's hull value for a flag-0x40
// dude), picks the salvage commodity from the dude's commodity flag bits with
// a cargo quantity from the class holds, picks a random armed weapon slot to
// offer, rolls the fuel for sale from the class fuel, and derives the
// capture odds (player crew + 0.1x escort crews + marine outfits, vs 15x the
// target's crew, x100, +/-rng jitter, clamped 1..75).
//
// The ShipClassTable/OutfitTable/OwnedOutfitGrid reads route through the
// managed Store records.
public static class InitTradeSession
{
    public static void Run()
    {
        short targetSlot = GameData.Player.TargetSlot;

        ushort dudeFlags;
        if (GameData.Ships[targetSlot].DudeSpawnIndex == -1)
        {
            dudeFlags = 0;
        }
        else
        {
            dudeFlags = (ushort)GameData.DudeSpawns[GameData.Ships[targetSlot].DudeSpawnIndex].Flags;
        }

        if ((dudeFlags & 0x40) == 0)
        {
            if (GameData.Ships[targetSlot].PersIndex == -1)
            {
                DialogScratch.BoardingSalvageCredits = -1;
            }
            else
            {
                short creditsInThousands = (short)(GameData.Pers[GameData.Ships[targetSlot].PersIndex].Credits / 1000);
                if (CommodityPricing.PriceSlopeBuy * creditsInThousands <= CommodityPricing.PriceLinearThreshold)
                {
                    DialogScratch.BoardingSalvageCredits =
                        (int)(CommodityPricing.PriceOuterScale * CommodityPricing.PriceSlopeBuy * creditsInThousands);
                }
                else
                {
                    short jitter = (short)SeedEvoRng.Run((short)(int)(CommodityPricing.PriceSlopeBuy * creditsInThousands));
                    DialogScratch.BoardingSalvageCredits = (int)(CommodityPricing.PriceOuterScale *
                        (CommodityPricing.PriceSlopeBuy * creditsInThousands + jitter));
                }
            }
        }
        else
        {
            short costInThousands = (short)(GameData.ShipClasses[GameData.Ships[targetSlot].ShipClass].Cost / 1000);
            double price;
            if (CommodityPricing.PriceSlopeSell * costInThousands <= CommodityPricing.PriceLinearThreshold)
            {
                price = CommodityPricing.PriceOuterScale * CommodityPricing.PriceSlopeSell * costInThousands;
            }
            else
            {
                short jitter = (short)SeedEvoRng.Run((short)(int)(CommodityPricing.PriceSlopeSell * costInThousands));
                price = CommodityPricing.PriceOuterScale *
                    (CommodityPricing.PriceSlopeSell * costInThousands + jitter);
            }
            DialogScratch.BoardingSalvageCredits = (int)price;
            if (DialogScratch.BoardingSalvageCredits < 1000)
            {
                DialogScratch.BoardingSalvageCredits = 1000;
            }
        }
        if (DialogScratch.BoardingSalvageCredits < 1)
        {
            DialogScratch.BoardingSalvageCredits = -1;
        }

        if (((int)(short)dudeFlags & 0xffbfU) == 0)
        {
            DialogScratch.BoardingSalvageCargoIndex = -1;
        }
        else
        {
            bool matchFound;
            do
            {
                DialogScratch.BoardingSalvageCargoIndex = (short)SeedEvoRng.Run(7);
                short commodityIndex = DialogScratch.BoardingSalvageCargoIndex;
                matchFound =
                    (commodityIndex == 0 && (dudeFlags & 1) != 0) ||
                    (commodityIndex == 1 && (dudeFlags & 2) != 0) ||
                    (commodityIndex == 2 && (dudeFlags & 4) != 0) ||
                    (commodityIndex == 3 && (dudeFlags & 8) != 0) ||
                    (commodityIndex == 4 && (dudeFlags & 0x10) != 0) ||
                    (commodityIndex == 5 && (dudeFlags & 0x20) != 0);
            } while (!matchFound);

            // Class holds, signed halve with the decompile's odd-negative rounding
            // (srawi+addze -- do not simplify to a bare >>1, they diverge for negative holds).
            short holds = GameData.ShipClasses[GameData.Ships[targetSlot].ShipClass].Holds;
            int halfHolds = (holds >> 1) + (holds < 0 && (holds & 1) != 0 ? 1 : 0);
            DialogScratch.BoardingSalvageCargoQty = (short)SeedEvoRng.Run((short)halfHolds);
            DialogScratch.BoardingSalvageCargoQty = (short)(halfHolds + DialogScratch.BoardingSalvageCargoQty);
        }

        short armedSlotCount = 0;
        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (GameData.Ships[targetSlot].WeaponSlotAmmo[slot] > 0 && HasArmedWeaponSlot.Run(slot))
            {
                armedSlotCount++;
            }
        }
        if (armedSlotCount < 1)
        {
            DialogScratch.BoardingSalvageAmmoType = -1;
        }
        else
        {
            short pickedSlot;
            bool slotArmed;
            do
            {
                do
                {
                    pickedSlot = (short)SeedEvoRng.Run(ShipRecord.WeaponSlotCount);
                } while (GameData.Ships[targetSlot].WeaponSlotAmmo[pickedSlot] < 1);
                slotArmed = HasArmedWeaponSlot.Run(pickedSlot);
            } while (!slotArmed);
            DialogScratch.BoardingSalvageAmmoQty = GameData.Ships[targetSlot].WeaponSlotAmmo[pickedSlot];
            DialogScratch.BoardingSalvageAmmoType = pickedSlot;
        }

        DialogScratch.BoardingSalvageFuel =
            (short)SeedEvoRng.Run((short)(GameData.ShipClasses[GameData.Ships[targetSlot].ShipClass].BaseFuel / 10));
        DialogScratch.BoardingSalvageFuel = (short)(DialogScratch.BoardingSalvageFuel * 10);

        // Capture-odds crew pool: player crew + 0.1 per eligible escort crew, then marine outfits.
        int crewPool = GameData.ShipClasses[GameData.Player.ShipClass].Crew;
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            var ship = GameData.Ships[shipIndex];
            if (IsEligibleEscort(ship))
            {
                double escortCrew = GameData.ShipClasses[ship.ShipClass].Crew;
                crewPool = (int)(CommodityPricing.PriceTotalSlope * escortCrew + (double)(short)crewPool);
            }
        }
        // Marine outfits: ModValue x owned adds to the pool. FUN_10014ae8 has no owned>0
        // guard (unlike most SumOutfitModValue callers), so guardOwnedPositive is off here.
        crewPool += OutfitTable.SumOutfitModValue(OutfitModType.Marines, guardOwnedPositive: false);

        short targetCrew = GameData.ShipClasses[GameData.Ships[targetSlot].ShipClass].Crew;
        DialogScratch.BoardingCaptureChance = (short)(int)(CommodityPricing.PriceCurveBarScale *
            ((double)(short)crewPool / (CommodityPricing.PriceCurveDivisor * targetCrew)));
        short oddsJitter = (short)SeedEvoRng.Run(21);
        DialogScratch.BoardingCaptureChance = (short)(DialogScratch.BoardingCaptureChance + (10 - oddsJitter));
        if (DialogScratch.BoardingCaptureChance < 1)
        {
            DialogScratch.BoardingCaptureChance = 1;
        }
        if (DialogScratch.BoardingCaptureChance > 75)
        {
            DialogScratch.BoardingCaptureChance = 75;
        }
    }

    // FUN_10014ae8 10350-10358 -- escort crew-pool eligibility: active, owned by the player
    // (slot 0), AI type 6 (escort), no grudge mission, class crew > 9, class InherentAI > 2.
    private static bool IsEligibleEscort(ShipRecord ship)
    {
        var shipClass = GameData.ShipClasses[ship.ShipClass];
        return ship.IsActive != 0 && ship.OwnerSlot == 0 && ship.AiBehaviorType == ShipAiType.Escort &&
               ship.GrudgeMissionIndex == -1 && shipClass.Crew > 9 && shipClass.InherentAI > ShipAiType.BraveTrader;
    }
}
