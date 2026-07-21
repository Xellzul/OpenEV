// Root namespace so unqualified `MissionGoalKind` resolves in every OpenEV.Override.Ports.* file
// (Combat/Dialog/Ship all compare against it), same convention as ShipAiType.
namespace OpenEV.Override.Ports;

// What the player must do to a mission's special ships (MissionRecord.MissionGoalType,
// +0x0a, res+0x26 mïsn "ShipGoal"). Names + values are the mïsn TMPL's ShipGoal choice
// list (editor/src/OpenEV.Editor.Schema/Schemas.More.cs, MissionShipGoals), cross-verified
// against every dispatch site: UpdateMissionStatusFlags.cs (the authoritative per-goal
// completion/fail logic), RunFleetSpawner.cs (Escort scatters spawn position; Rescue
// spawns the ship pre-disabled/derelict), IsPlayerEngagementTarget.cs, ApplyShipDamage.cs,
// UpdateShipAiSteering.cs (ChaseOff tallies DepartedShipCount on jump-out wind-up).
public enum MissionGoalKind : short
{
    None = -1,          // no special-ship goal; UpdateMissionStatusFlags auto-completes
    DestroyAll = 0,     // kill every special ship (DestroyedShipCount vs GoalThreshold)
    Disable = 1,         // disable, don't kill, the special ships (DisabledShipCount; a kill fails it)
    Board = 2,           // board the special ships (BoardedShipCount)
    Escort = 3,          // protect the special ships while active (GrudgeMissionIndex-tracked)
    Observe = 4,         // be in the special ships' system once they're all spawned
    RescueDisabled = 5,  // board the special ships, spawned pre-disabled/derelict (BoardedShipCount)
    ChaseOff = 6,        // destroy OR drive off (DestroyedShipCount + DepartedShipCount vs GoalThreshold)
}
