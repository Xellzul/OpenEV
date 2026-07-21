namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Sound.Model;

// FUN_100065d8 — EV Override-11.c lines 3775-3815. If the ship has a live, in-system target,
// auto-fire its first ready carried-ship ("special") weapon slot: select it, play the fire sound,
// re-arm the slot's reload countdown, and spend one round. A slot still reloading (or one that
// SpawnSpecialWeaponShip rejects) aborts the whole pass without firing.
public static class AutoFireSpecialAtTarget
{
    public static void Run(ShipRec s)
    {
        if (s.TargetSlot == -1 ||
            s.CurrentSystem != Core.Model.GameData.Ships[s.TargetSlot].CurrentSystem ||
            Core.Model.GameData.Ships[s.TargetSlot].IsActive == 0)
            return;

        for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (s.WeaponSlotType[slot] <= 0 ||
                s.WeaponSlotAmmo[slot] <= 0 ||
                (WeaponGuidanceType)Core.Model.GameData.Weapons[slot].GuidanceType != WeaponGuidanceType.CarriedShip)
                continue;

            if (0.0 < s.WeaponSlotReload[slot])   // slot still reloading
                return;
            s.SelectedWeaponSlot = slot;
            if (SpawnSpecialWeaponShip.Run(s.Ptr, slot) == 0)
                return;

            PlayPositionalSound.Run(-1,
                CombatSoundCells.WeaponSoundTable[Core.Model.GameData.Weapons[s.SelectedWeaponSlot].FireSound], 5,
                s.PosX, s.PosY, ShipTable.PosX, ShipTable.PosY);   // listener = player ship record[0]
            s.WeaponSlotReload[slot] = Core.Model.GameData.Weapons[slot].ReloadTime / (float)s.WeaponSlotType[slot];
            s.WeaponSlotAmmo[slot] = (short)(s.WeaponSlotAmmo[slot] - 1);
            return;
        }
    }
}
