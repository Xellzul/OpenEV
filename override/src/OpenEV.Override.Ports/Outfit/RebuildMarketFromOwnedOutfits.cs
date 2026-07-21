using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_10059a44 (EV Override-11.c lines 36883-36907). Clears the player's
// weapon slots, then re-fills WeaponSlotType/Ammo from the owned-outfit grid: for
// every owned weapon/ammo outfit, mirror its owned count into the ship weapon slot
// its ModValue names.
public static class RebuildMarketFromOwnedOutfits
{
    public static void Run()
    {
        var player = GameData.Player;
        for (short index = 0; index < ShipRecord.WeaponSlotCount; index = (short)(index + 1))
        {
            player.WeaponSlotType[index] = 0;
        }
        for (short index = 0; index < OutfitTable.Count; index = (short)(index + 1))
        {
            var outfit = OutfitTable.Store[index];
            if (outfit.ModType[0] == OutfitModType.Weapon)
            {
                player.WeaponSlotType[outfit.ModValue[0]] = OwnedOutfitGrid.Store[index];
            }
            if (outfit.ModType[0] == OutfitModType.Ammo)
            {
                player.WeaponSlotAmmo[outfit.ModValue[0]] = OwnedOutfitGrid.Store[index];
            }
        }
    }
}
