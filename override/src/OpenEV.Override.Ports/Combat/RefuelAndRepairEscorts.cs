using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006ba80 (EV Override-11.c 44273-44309) — top up every escort owned by this ship (active, same
// OwnerSlot, not disabled): restore its shield to the class max and refill each weapon slot's ammo to
// the class default. Half the refueled escorts (rounded down) each drive a world-spawn tick, and if
// this is the player (slot 0) the escort wages are charged.
public static class RefuelAndRepairEscorts
{
    public static void Run(ShipRec ship)
    {
        short refueledCount = 0;
        for (short escortIndex = 1; escortIndex < ShipTable.Count; escortIndex++)
        {
            var escort = ShipTable.Ships[escortIndex];
            if (escort.IsActive == 0
                || ship.SlotIndex != escort.OwnerSlot
                || ShipDerivedStats.IsDisabled(escort))
                continue;

            // The original raw-copies the class's shield word into the escort's shield cell. That cell
            // holds a whole-number value modelled here as float, so a numeric copy is value-identical.
            escort.Shield = Core.Model.GameData.ShipClasses[escort.ShipClass].Shield;
            for (short weaponIndex = 0; weaponIndex < ShipRecord.WeaponSlotCount; weaponIndex++)
                escort.WeaponSlotAmmo[weaponIndex] =
                    Core.Model.GameData.ShipClasses[escort.ShipClass].DefaultWeaponAmmo[weaponIndex];
            refueledCount++;
        }

        // Half the refueled escorts (signed truncation toward zero) each drive a world-spawn tick.
        refueledCount = (short)(int)(refueledCount * ShipStatConstants.Half);
        for (short i = 0; i < refueledCount; i++)
            TickWorldDailyEvents.Run();

        if (ship.SlotIndex == 0)
            PayEscortWages.Run(1);
    }
}
