using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_100095a4 (EV Override-11.c 5018-5033) — halt the ship's AI movement: unless it is already in
// AiState 9 (jumping out), reset its AI state and maneuver to idle; then clear its nav target, jump
// windup timer, and desired accel/speed.
public static class ResetToOriginUnlessJumping
{
    public static void Run(ShipRec ship)
    {
        if (ship.AiState != ShipAiState.Refuel)
        {
            ship.AiState = ShipAiState.Idle;
            ship.AiManeuverState = ShipManeuverState.None;
        }
        ship.NavTargetSpob = -1;
        ship.JumpWindupTimer = 0;
        ship.DesiredAccel = 0f;
        ship.DesiredSpeed = 0f;
    }
}
