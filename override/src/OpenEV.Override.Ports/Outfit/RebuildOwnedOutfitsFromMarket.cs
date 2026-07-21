using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// Port of FUN_10059920 (EV Override-11.c lines 36853-36882). For each weapon
// (ModType Weapon) / ammo (ModType Ammo) outfit whose slot index (ModValue[0])
// matches a ship weapon slot, mirror the player's WeaponSlotType/Ammo into the
// owned-outfit grid; clamp any resulting negative count to 0.
public static class RebuildOwnedOutfitsFromMarket
{
    public static void Run()
    {
        var player = GameData.Player;
        for (short slotIndex = 0; slotIndex < ShipRecord.WeaponSlotCount; slotIndex = (short)(slotIndex + 1))
        {
            for (short outfitIndex = 0; outfitIndex < OutfitTable.Count; outfitIndex = (short)(outfitIndex + 1))
            {
                var outfit = OutfitTable.Outfits[outfitIndex];
                if (outfit.ModType[0] == OutfitModType.Weapon && slotIndex == outfit.ModValue[0])
                {
                    OwnedOutfitGrid.Store[outfitIndex] = player.WeaponSlotType[slotIndex];
                }
                if (outfit.ModType[0] == OutfitModType.Ammo && slotIndex == outfit.ModValue[0])
                {
                    OwnedOutfitGrid.Store[outfitIndex] = player.WeaponSlotAmmo[slotIndex];
                }
                if (OwnedOutfitGrid.Store[outfitIndex] < 0)
                {
                    OwnedOutfitGrid.Store[outfitIndex] = 0;
                }
            }
        }
    }
}
