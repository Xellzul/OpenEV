namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

// FUN_100083c0 — EV Override-11.c lines 4500-4516. True if any NPC ship
// (slots 1..35) has engaged an ally or carrier.
public static class AnyShipEngaged
{
    public static bool Run()
    {
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            if (ShipAi.HasEngagedAllyOrCarrier(ShipTable.Ships[shipIndex]))
                return true;
        }
        return false;
    }
}
