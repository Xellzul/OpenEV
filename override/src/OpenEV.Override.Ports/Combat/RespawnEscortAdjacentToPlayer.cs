using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_100677f4 (EV Override-11.c 42993-43050) — respawn a player-owned escort next
// to the player: scatter it to a random ring 50..99 units away at a random bearing,
// copy the player's system/heading/position, then either park it with zero velocity
// (player idle) or — if the player is mid jump-windup — shove it ~one decaying-series
// length behind the player, give it forward velocity, and arm its own windup timer so
// it jumps alongside. Ships not owned by the player (owner slot != 0) are left alone.
public static class RespawnEscortAdjacentToPlayer
{
    public static void Run(ShipRec ship)
    {
        if (ship.OwnerSlot != 0) return;

        ship.DockedSpobIndex = -2;
        ship.PriorSystem = -2;
        ship.DesiredAccel = ShipStatConstants.SpawnZeroDefault;
        ship.DesiredSpeed = ShipStatConstants.SpawnZeroDefault;
        ship.HasSelectedWeapon = 0;
        ship.DudeSpawnIndex = -1;
        ship.PersIndex = -1;
        ship.GrudgeMissionIndex = -1;
        ship.CurrentSystem = Core.Model.GameData.Player.CurrentSystem;
        ship.Heading = Core.Model.GameData.Player.Heading;
        ship.PosX = Core.Model.GameData.Player.PosX;
        ship.PosY = Core.Model.GameData.Player.PosY;

        // Scatter onto a random ring 50..99 units around the player at a random bearing.
        short roll = (short)SeedEvoRng.Run(50);   // extsh: roll 0..49
        float ringRadius = roll + 50;
        int ringAngle = (int)SeedEvoRng.Run(360);
        {
            float px = ship.PosX, py = ship.PosY;
            // The decompile's `>> 0x20` on the angle is a register-pair artifact of
            // FUN_1005d9c4's r3:r4 return; SeedEvoRng.Run gives the roll directly, so pass it
            // whole (shifting it out collapsed every escort onto heading 0 — one axis, no ring).
            EvMath.OffsetByHeading(ringRadius, ringAngle, ref px, ref py);
            ship.PosX = px; ship.PosY = py;
        }

        if (Core.Model.GameData.Player.JumpWindupTimer == 0)
        {
            ship.VelY = ShipStatConstants.SpawnZeroDefault;
            ship.VelX = ShipStatConstants.SpawnZeroDefault;
        }
        else
        {
            // Sum a step that decays from 45 by 1.165/iteration while positive, then push the
            // escort that far back along the reversed heading (behind the jumping player).
            float pushbackDist = ShipStatConstants.SpawnZeroDefault;
            float speedStep = ShipStatConstants.RespawnSpeedStep;
            while (speedStep > ShipStatConstants.SpawnZeroDefault)
            {
                pushbackDist = (float)((double)pushbackDist + (double)speedStep);
                speedStep = (float)((double)speedStep - (double)ShipStatConstants.SpawnSpreadStep);
            }
            {
                float px = ship.PosX, py = ship.PosY;
                EvMath.OffsetByHeading(pushbackDist, (ship.Heading + 180) % 360, ref px, ref py);
                ship.PosX = px; ship.PosY = py;
            }

            ship.VelY = ShipStatConstants.SpawnZeroDefault;
            ship.VelX = ShipStatConstants.SpawnZeroDefault;
            ship.JumpWindupTimer = -999;
            ship.AiTickStamp = 0;
            {
                float vx = ship.VelX, vy = ship.VelY;
                EvMath.OffsetByHeading(ShipStatConstants.SpawnSpreadStart, ship.Heading, ref vx, ref vy);
                ship.VelX = vx; ship.VelY = vy;
            }
        }

        ShipAi.SetStateHyperWindupAndPropagate(ship);
    }
}
