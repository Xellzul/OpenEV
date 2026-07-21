using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005b698 — can the player take on one more unit of commodity `commodityType`?
// True (1) only when they have enough credits AND spare cargo capacity.
// Decompile: EV Override-11.c lines 37695-37723.
public static class CanBuyCommodity
{
    public static int Run(short commodityType)
    {
        // Money gate: the commodity's final price must not exceed the player's credits.
        if (ShipTable.Player.Credits < CommodityPricing.FinalPrice[commodityType])
        {
            return 0;
        }

        // Room gate: total cargo carried must be below total cargo capacity
        // (the player's hold + every qualifying escort's hold).
        short capacity = (short)TotalMassWithEscorts.Run();
        short carried = (short)ShipDerivedStats.TotalMassCarried(ShipTable.Player);
        return carried < capacity ? 1 : 0;
    }
}
