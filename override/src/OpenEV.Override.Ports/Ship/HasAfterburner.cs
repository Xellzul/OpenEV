using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Ship;

// FUN_100610c0 (EV Override-11.c 40604-40626) — does a freshly spawned ship get an afterburner?
// AfterburnerAlways → yes; AfterburnerAdvancedRating → yes once the player's combat rating beats a
// roll (NPCs gain afterburners as the player advances); otherwise no. Result stored in ship +0x73.
public static class HasAfterburner
{
    public static bool Run(ShipRec ship)
    {
        var classFlags = GameData.ShipClasses[ship.ShipClass].Flags;
        if ((classFlags & ShipFlags.AfterburnerAlways) != 0)
            return true;
        if ((classFlags & ShipFlags.AfterburnerAdvancedRating) == 0)
            return false;

        short roll = (short)SeedEvoRng.Run(1344);
        return WorldState.PlayerCombatRating >= roll + 256;
    }
}
