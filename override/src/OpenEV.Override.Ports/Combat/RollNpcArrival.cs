using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_10065ef4 (EV Override-11.c 42453-42517) — roll which kind of NPC arrives in a
// system: 1-in-7 a pers, else 1-in-7 a random eligible fleet (which spawns its own ships
// and reports none here), else a standard system-arrival NPC. If a single ship was
// spawned, drop it at a random bearing ~1000+ units out, point it back at the origin, and
// arm it as a fresh jump-in. Returns the spawned ship index, or -1.
public static class RollNpcArrival
{
    public static int Run(int systemId)
    {
        int newShipId;
        if (SeedEvoRng.Run(7) == 0)
            newShipId = SpawnPers.Run(systemId, 1, -1);
        else if (SeedEvoRng.Run(7) == 0)
        {
            newShipId = -1;
            SpawnRandomEligibleFleet.Run(systemId);
        }
        else
            newShipId = SpawnSystArrivalNpc.Run((short)systemId, 2);

        var shipIndex = (short)newShipId;
        if (shipIndex == -1)
            return -1;

        // Sum the spread band 50, 48.835, ... while > 0 in 1.165 steps.
        var offsetAccum = ShipStatConstants.SpawnZeroDefault;
        for (var spreadStep = ShipStatConstants.SpawnSpreadStart;
             spreadStep > ShipStatConstants.SpawnZeroDefault;
             spreadStep -= ShipStatConstants.SpawnSpreadStep)
            offsetAccum += spreadStep;

        var ship = Core.Model.GameData.Ships[shipIndex];
        ship.PosY = ShipStatConstants.SpawnZeroDefault;
        ship.PosX = ShipStatConstants.SpawnZeroDefault;
        var arrivalBearing = (int)SeedEvoRng.Run(360);
        EvMath.OffsetByHeading(ShipStatConstants.SpawnBaseOffset + offsetAccum, arrivalBearing, ref ship.PosX, ref ship.PosY);
        ship.Heading = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, ShipStatConstants.SpawnZeroDefault, ShipStatConstants.SpawnZeroDefault);
        ship.DockedSpobIndex = -2;
        ship.PriorSystem = -1;
        ship.VelY = ShipStatConstants.SpawnZeroDefault;
        ship.VelX = ShipStatConstants.SpawnZeroDefault;
        ship.JumpWindupTimer = -999;   // jump-armed / just-arrived sentinel
        ship.AiTickStamp = (int)MacToolbox.TickCount();
        EvMath.OffsetByHeading(ShipStatConstants.SpawnSpreadStart, ship.Heading, ref ship.VelX, ref ship.VelY);
        ShipAi.SetStateWindDown(ShipTable.Ships[shipIndex]);
        return newShipId;
    }
}
