using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_1006dc58 (EV Override-11.c lines 44915-44961). If the player owns any
// AutoRefuel outfit, top the tank up from credits (capped by what the player can
// afford) and mark the HUD panels dirty.
public static class TickPassiveOutfitTopup
{
    public static void Run()
    {
        bool needsTopup = false;
        for (short slot = 0; slot < OutfitTable.Count; slot = (short)(slot + 1))
        {
            var outfit = OutfitTable.Store[slot];
            if ((outfit.ModType[0] == OutfitModType.AutoRefuel ||
                 outfit.ModType[1] == OutfitModType.AutoRefuel) &&
                0 < OwnedOutfitGrid.Store[slot])
            {
                needsTopup = true;
                break;
            }
        }
        if (needsTopup)
        {
            var player = ShipTable.Player;
            short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(player);
            if (player.Fuel < (float)fuelMax)
            {
                fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(player);
                short amount = (short)(int)((float)fuelMax - player.Fuel);
                if (player.Credits < amount)
                {
                    amount = (short)player.Credits;
                }
                player.Fuel += (float)amount;
                player.Credits -= amount;
                WorldState.ShieldEnergyBarDirty = 1;
                WorldState.HudStatusPanelDirty = 1;
            }
        }
    }
}
