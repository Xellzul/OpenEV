using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1005ea80 (EV Override-11.c lines 39364-39379).
//
// True when the player has an equipped weapon (WeaponSlotType +0x74 > 0) whose ammo
// link (+0x08) is weaponType — i.e. an armed launcher that uses weaponType as its ammo.
public static class HasArmedWeaponSlot
{
    public static bool Run(short weaponType)
    {
        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (weaponType == Core.Model.GameData.Weapons[slot].AmmoLink &&
                Core.Model.GameData.Player.WeaponSlotType[slot] > 0)
            {
                return true;
            }
        }
        return false;
    }
}
