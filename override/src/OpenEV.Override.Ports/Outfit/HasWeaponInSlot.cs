using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005b71c — does the player have a weapon in fire-mode slot `slotIndex`?
// Slots 0-5 are the ship's direct gun slots; slots 6+ map through the
// weapon-slot -> outfit-index table to a junk special-weapon quantity.
// Decompile: EV Override-11.c lines 37724-37743.
public static class HasWeaponInSlot
{
    public static bool Run(short slotIndex)
    {
        if (slotIndex < 6)
        {
            return GameData.Player.CargoHold[slotIndex] > 0;
        }

        // Special weapon-slot tabs (6+) map to a junk outfit index, or -1.
        short outfitIndex = WeaponSlotOutfitMap.Store[slotIndex - 6];
        return outfitIndex != -1 && GameData.Junk[outfitIndex].PlayerQty > 0;
    }
}
