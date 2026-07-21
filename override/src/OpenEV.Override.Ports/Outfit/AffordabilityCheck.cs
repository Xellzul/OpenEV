// Port of FUN_1003a0ac (EV Override-11.c lines 23762-23859).

using System;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// Returns 1 when the currently selected outfit row can be bought: passes the
// CannotBuyOutfit gate, fits the free mass, passes the per-ModType rules
// (ammo needs a weapon with capacity, cargo needs holds space, Map/StatusClear
// are one-shot per visit), and the player can afford the quantized price.
public static class AffordabilityCheck
{
    public static int Run()
    {
        short freeMass = (short)ShipDerivedStats.FreeMassSpace();
        if (OutfitShopState.SelectedRow == -1)
        {
            return 0;
        }

        short sel = OutfitShopState.SelectedRow;
        var outfit = OutfitTable.Store[sel];
        var player = ShipTable.Player;
        int canBuy;
        if ((byte)CannotBuyOutfit.Run(sel) == 0 && (outfit.Mass <= freeMass || outfit.Mass < 1))
        {
            if (outfit.ModType[0] == OutfitModType.Ammo)
            {
                if (outfit.ModValue[0] < 0)
                {
                    canBuy = 1;
                }
                else if ((WeaponGuidanceType)GameData.Weapons[outfit.ModValue[0]].GuidanceType == WeaponGuidanceType.CarriedShip)
                {
                    canBuy = CanSpawnAnotherSubMunition.Run(
                        (short)(GameData.Weapons[outfit.ModValue[0]].AmmoLink - 128), sel, outfit.ModValue[0]) ? 1 : 0;
                }
                else
                {
                    canBuy = 1;
                }
            }
            else if (outfit.ModType[0] == OutfitModType.Cargo)
            {
                if (outfit.ModValue[0] < 0)
                {
                    // NegativeHoldsFlag = "holds expandable" flag from the 'shïp' loader (resource Holds < 0 -> 0).
                    if (GameData.ShipClasses[player.ShipClass].NegativeHoldsFlag == 0)
                    {
                        canBuy = 0;
                    }
                    else
                    {
                        short cargoMax = (short)ShipDerivedStats.EffectiveCargoMax();
                        short carried = (short)ShipDerivedStats.TotalMassCarried(player);
                        if (Math.Abs((int)outfit.ModValue[0]) < cargoMax - carried)
                        {
                            canBuy = 1;
                        }
                        else
                        {
                            canBuy = 0;
                        }
                    }
                }
                else
                {
                    canBuy = 1;
                }
            }
            else if (outfit.ModType[0] == OutfitModType.Map)
            {
                if (OutfitShopState.MapOutfitBought == 0)
                {
                    canBuy = 1;
                }
                else
                {
                    canBuy = 0; // one Map purchase per outfitter visit
                }
            }
            else if (outfit.ModType[0] == OutfitModType.StatusClear)
            {
                if (OutfitShopState.StatusClearBought == 0)
                {
                    canBuy = 1;
                }
                else
                {
                    canBuy = 0; // one StatusClear purchase per outfitter visit
                }
            }
            else
            {
                canBuy = 1;
            }
        }
        else
        {
            canBuy = 0;
        }
        if (canBuy != 0)
        {
            int spobByteOff = player.NavTargetSpob * 0x48;
            int price = PriceQuantize.Run(
                (int)SpaceportGlobals.ShopPriceScale[0], outfit.Cost, (short)spobByteOff, outfit.TechLevel,
                GameData.Spobs[player.NavTargetSpob].TechLevel);
            if (player.Credits < price)
            {
                canBuy = 0;
            }
        }
        return canBuy;
    }
}
