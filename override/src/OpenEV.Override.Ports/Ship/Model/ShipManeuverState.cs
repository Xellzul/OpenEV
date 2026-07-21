// Root namespace so unqualified `ShipManeuverState` resolves in every OpenEV.Override.Ports.* file.
namespace OpenEV.Override.Ports;

// A ship's immediate AI maneuver / sub-state (ShipRec.AiManeuverState, backed by Field0xa76).
// UpdateShipAiObjective (FUN_10001d64) picks it from the high-level ShipAiState + geometry, and
// UpdateShipAiSteering (FUN_1000366c) dispatches exactly one movement handler per value. Names are
// the game's own debug labels (DrawAiManeuverStateLabel) expanded and cross-checked against the
// decompile executor behaviour; the cited line numbers are the FUN_1000366c handler blocks. Only
// values 0..17 are ever written. (The debug label for state 6 renders as garbage "T+F" because its
// label-string pointer lands in the float-constant pool — but the state itself is the live core
// dogfight maneuver, so it is named for its behaviour, not the broken label.)
public enum ShipManeuverState : short
{
    None = 0,               // "LoJack": no maneuver / neutral cruise (board-opportunity check)      2710
    KillSpeed = 1,          // "KillSpd": retro-brake to a stop                                      2713-2745
    FlyToStellar = 2,       // "FlyStel": aim at the stellar/nav point and thrust                    2748-2783
    FlyToHyperExit = 3,     // "FlyHypD": align to the hyperspace-departure vector                   2786-2799
    HyperJump = 4,          // "HypJmp": execute the jump; leave the system when the charge completes 2801-2831
    RunAway = 5,            // "RunAwy": flee directly away from the target (parting shots if fleeing) 2848-2864
    TurnAndFire = 6,        // "T+F" = Turn+Fire: core dogfight — aim/lead, fire, close in           2865-2958
    MissileAttack = 7,      // "Missle": stand-off homing/missile attack                             3027-3059
    DockInCarrier = 8,      // "Dock": dock/land back into the parent carrier bay                    3351-3435
    Chase = 9,              // "Chase": full-speed pursuit                                           3252-3306
    HyperArriveZoom = 10,   // "Zoom": hyperspace-arrival warp-in slowdown (only from HyperIn)       3436-3441
    ChaseSlow = 11,         // "ChasSlw": speed-limited careful approach                             3186-3251
    FormationFly = 12,      // "FormFly": fly a fixed offset relative to the leader                  3307-3350
    JumpWithParent = 13,    // "JmpPar": jump in lockstep with the parent (escort follows leader out) 2832-2847
    HoldAndFire = 14,       // "WaitTarg": near-stop, hold facing target and fire (heavy warships)   3155-3185
    Board = 15,             // "Board": board/salvage the disabled target                           3060-3154
    VeerOff = 16,           // "VeerOff": break off the attack run (±135° pass), then resume TurnAndFire 2959-2980
    Afterburner = 17,       // "AfterBurn": afterburner attack dash                                  2981-3025
}
