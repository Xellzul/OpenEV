namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Outfit.Model;

// Port of FUN_1005f0b8 (EV Override-11.c 39609-39665) — can another sub-munition (carried-fighter)
// ship of this class still be launched, or is the live count already at the outfit's MaximumCount?
public static class CanSpawnAnotherSubMunition
{
    public static bool Run(short subMunitionType, short weaponIndex, short launcherIndex)
    {
        short sourceSlotIndex;
        short slotOrCount;

        if (weaponIndex == -1 && launcherIndex == -1)
        {
            // Faithful restructure of a C while whose test reset sourceSlotIndex to -1 every iteration
            // (comma-operator) and recorded the slot only via a second comma in the OR's last term.
            sourceSlotIndex = -1;
            for (slotOrCount = 0; ; slotOrCount = (short)(slotOrCount + 1))
            {
                sourceSlotIndex = -1;
                if (slotOrCount >= ShipRecord.WeaponSlotCount) break;
                bool slotNotSubMunitionLauncher =
                    Core.Model.GameData.Player.WeaponSlotType[slotOrCount] < 1 ||
                    (WeaponGuidanceType)Core.Model.GameData.Weapons[slotOrCount].GuidanceType != WeaponGuidanceType.CarriedShip;
                if (!slotNotSubMunitionLauncher)
                {
                    sourceSlotIndex = slotOrCount;
                    if (subMunitionType + 128 == Core.Model.GameData.Weapons[slotOrCount].AmmoLink) break;
                }
            }
            if (sourceSlotIndex == -1)
                return false;
            for (slotOrCount = 0; slotOrCount < OutfitTable.Count; slotOrCount = (short)(slotOrCount + 1))
            {
                // Same comma-operator restructure: find the outfit whose Ammo mod points at this slot.
                short ammoOutfitIndex = weaponIndex;
                for (short bank = 0; ; bank = (short)(bank + 1))
                {
                    ammoOutfitIndex = weaponIndex;
                    if (bank >= OutfitRecord.ModBankCount) break;
                    bool modNotAmmo = OutfitTable.Outfits[slotOrCount].ModType[bank] != OutfitModType.Ammo;
                    if (!modNotAmmo)
                    {
                        ammoOutfitIndex = slotOrCount;
                        if (sourceSlotIndex == OutfitTable.Outfits[slotOrCount].ModValue[bank]) break;
                    }
                }
                weaponIndex = ammoOutfitIndex;
            }
            if (weaponIndex == -1)
                return false;
            slotOrCount = Core.Model.GameData.Player.WeaponSlotAmmo[sourceSlotIndex];
        }
        else
        {
            if (weaponIndex == -1 || launcherIndex == -1)
                return false;
            slotOrCount = OwnedOutfitGrid.Store[weaponIndex];
        }
        // Count the player's live carried-fighter ships (AiBehaviorType 5) of this sub-munition class.
        for (sourceSlotIndex = 1; sourceSlotIndex < ShipTable.Count; sourceSlotIndex = (short)(sourceSlotIndex + 1))
        {
            if (Core.Model.GameData.Ships[sourceSlotIndex].IsActive != 0 &&
                subMunitionType == Core.Model.GameData.Ships[sourceSlotIndex].ShipClass &&
                Core.Model.GameData.Ships[sourceSlotIndex].OwnerSlot == 0 &&
                Core.Model.GameData.Ships[sourceSlotIndex].AiBehaviorType == ShipAiType.NavalFighter &&
                Core.Model.GameData.Ships[sourceSlotIndex].GrudgeMissionIndex == -1)
            {
                slotOrCount = (short)(slotOrCount + 1);
            }
        }
        return slotOrCount < OutfitTable.Outfits[weaponIndex].MaximumCount;
    }
}
