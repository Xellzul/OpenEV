// Root namespace so unqualified `ShipAiState` resolves in every OpenEV.Override.Ports.* file.
namespace OpenEV.Override.Ports;

// A ship's high-level AI objective (ShipRec.AiState, backed by Field0xa74). Chosen by the
// per-frame objective step (UpdateShipAiObjective / FUN_10001d64), which then picks the
// AiManeuverState sub-state (ShipManeuverState) that UpdateShipAiSteering executes. Names are
// the game's own debug labels (DrawAiStateLabel) expanded and cross-checked against the
// decompile behaviour (FUN_1000001c dispatcher + the per-type behaviour funcs). Only values
// 0..13 are ever written.
public enum ShipAiState : short
{
    Idle = 0,             // "HiJack": default/root, no active order — pick next objective here (hostiles hunt from it)
    GoToStellar = 1,      // "GoStel": fly to a spaceport/planet nav point and land
    HyperOut = 2,         // "HypOut": depart to hyperspace with no target
    DefendRetreat = 3,    // "DefRet": flee-and-jump under fire
    AttackShip = 4,       // "FightSh": main combat — attack a ship
    ReturnToParent = 5,   // "GoHome": return to and dock with parent carrier
    Wait = 6,             // "Wait": hold position / loiter
    Inspect = 7,          // "Inspect": approach and scan a ship (customs/police)
    HyperIn = 8,          // "JumpIn": just arrived from hyperspace
    Refuel = 9,           // "Refuel": land on a planet to refuel
    EscortParent = 10,    // "FlyPr": escort — formation-fly with parent
    HyperWithParent = 11, // "HypPr": jump to hyperspace in sync with parent
    GuardPlayer = 12,     // "Protect": guard the player (formation near player, follow into hyper)
    Plunder = 13,         // "Plunder": board/plunder a disabled ship
}
