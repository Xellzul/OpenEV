namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;

// FUN_1006615c — EV Override-11.c lines 42518-42588. Spawns one NPC "dude" ship of type
// dudeIndex into systemIndex: allocates a ship slot, rolls a ship class from the dude's
// weighted class list, initialises govt / AI / weapon slots, and returns the slot (or -1).
public static class SpawnDudeShip
{
    public static int Run(short dudeIndex, short systemIndex)
    {
        int result = AllocateShipSlot.Run(systemIndex, 1);
        short shipSlot = (short)result;
        if (shipSlot == -1)
            return -1;

        var dude = Core.Model.GameData.DudeSpawns[dudeIndex];
        short classRoll = (short)PickWeightedSlot.Run(dude);

        var ship = ShipTable.Ships[shipSlot];
        if (classRoll < 0 || classRoll > 3)
        {
            // No valid class rolled — release the slot.
            ship.IsActive = 0;
            ship.HasWorldSpriteNode = 0;
            return -1;
        }

        ship.DudeSpawnIndex = dudeIndex;
        ship.ShipClass = dude.ShipClass[classRoll];
        ship.Govt = dude.Govt;

        var cls = Core.Model.GameData.ShipClasses[ship.ShipClass];
        ship.AiBehaviorType = dude.AiType < ShipAiType.WimpyTrader ? cls.InherentAI : dude.AiType;

        ShipAi.ResetAiToIdle(ship);

        for (int slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            ship.WeaponSlotType[slot] = cls.DefaultWeaponType[slot];
            // Faithful quirk: the carried-ship test indexes the weapon table by the SLOT index,
            // not by the weapon type just assigned (cls.DefaultWeaponType[slot]).
            if ((WeaponGuidanceType)Core.Model.GameData.Weapons[slot].GuidanceType == WeaponGuidanceType.CarriedShip)
            {
                ship.WeaponSlotAmmo[slot] = cls.DefaultWeaponAmmo[slot];
            }
            else
            {
                ship.WeaponSlotAmmo[slot] =
                    (short)(int)(ShipStatConstants.NpcWeaponAmmoScale * cls.DefaultWeaponAmmo[slot]);
            }
        }

        ship.Shield = cls.Shield;
        return result;
    }
}
