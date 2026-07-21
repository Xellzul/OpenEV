using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10008430 (EV Override-11.c lines 4522-4545).
//
// True if any active NPC (ship slots 1..35, excluding ship itself) has engaged an
// ally/carrier AND is an enemy of ship by the pers/govt rules.
public static class HasEngagedEnemyInWindow
{
    public static bool Run(ShipRec ship)
    {
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            if (Core.Model.GameData.Ships[shipIndex].IsActive != 0 &&
                shipIndex != ship.SlotIndex &&
                ShipAi.HasEngagedAllyOrCarrier(ShipTable.Ships[shipIndex]) &&
                ArePersEnemies.Run(ship, ShipTable.Ships[shipIndex]))
            {
                return true;
            }
        }
        return false;
    }
}
