using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1006afb8 — EV Override-11.c lines 44042-44093. Spawns the spob's government
// (tribute) defender ship, scattered near the spob with a random heading at full
// class speed, set to call for help and engage the player. Returns the new slot, or -1.
public static class SpawnGovtDefender
{
    public static int Run(short spobIndex)
    {
        var spob = Core.Model.GameData.Spobs[spobIndex];
        if (spob.DefenseDude == -1)
        {
            return -1;
        }

        int result = SpawnDudeShip.Run(spob.DefenseDude, spob.System);
        short slot = (short)result;
        if (slot == -1)
        {
            return result;
        }

        var ship = Core.Model.GameData.Ships[slot];
        ship.DefendedSpobIndex = spobIndex;
        ship.SalvageClaimed = 1;
        ship.AiActionTimer = 0;
        ship.AiBehaviorType = ShipAiType.Warship;
        ship.GrudgeMissionIndex = -1;
        ship.OwnerSlot = -1;
        ship.Govt = spob.Govt;

        // Scatter within 0..79 of the spob, biased -40 to centre on it.
        ship.PosX = (float)(spob.XPos + (short)SeedEvoRng.Run(80) - 40);
        ship.PosY = (float)(spob.YPos + (short)SeedEvoRng.Run(80) - 40);
        ship.Heading = (short)SeedEvoRng.Run(360);
        float zero = ShipStatConstants.SpawnZeroDefault;
        ship.VelY = zero;
        ship.VelX = zero;
        EvMath.OffsetByHeading((double)Core.Model.GameData.ShipClasses[ship.ShipClass].Speed,
            ship.Heading, ref ship.VelX, ref ship.VelY);
        ShipAi.CallForDefendersAndEngagePlayer(ShipTable.Ships[slot]);

        return result;
    }
}
