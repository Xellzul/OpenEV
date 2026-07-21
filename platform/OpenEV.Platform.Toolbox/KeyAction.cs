namespace OpenEV.Platform.Toolbox;

// The 45 EVO key-binding slots (Misc.ActiveKeyMap), each = one bindable game
// control. Members are named ActionN where N is the slot index (Action35 == slot
// 35) — rename them as the actions are identified. There is NO resource that
// labels these (prefs DITL 4001 rebind widgets are unlabeled userItems; STR# 129
// is the key-NAME table, not action labels), so the comments below are the only
// hints — derived from how TickShipAI reads each slot. Slots 33–44 are the
// DEBUG/CHEAT bank (read only when the debug-mode flag is set).
public enum KeyAction
{
    Action0  = 0,   // cycle selected secondary-weapon slot
    Action1  = 1,   // deselect secondary weapon
    FirePrimary  = 2,   // fire primary weapon
    Action3  = 3,   // fire secondary weapon
    Action4  = 4,
    Land = 5,   // land / request landing
    Action6  = 6,
    Action7  = 7,   // aim toward target / nav object
    Action8  = 8,   // cancel nav / clear destination
    Action9  = 9,   // open map (RunGalaxyMapDialog)
    Action10 = 10,  // cycle target ship (next/prev)
    Action11 = 11,  // target nearest ship
    Action12 = 12,  // engage autopilot
    Action13 = 13,  // cycle hyperspace destination
    Action14 = 14,  // hyperspace / jump
    Action15 = 15,
    Action16 = 16,  // read while ship disabled (line 897)
    Action17 = 17,  // escort command
    Action18 = 18,  // escort command
    Action19 = 19,  // escort command
    Action20 = 20,
    Action21 = 21,
    TurnLeft = 22,  // turn left (CCW)
    TurnRight = 23,  // turn right (CW)
    Action24 = 24,  // accelerate / thrust
    Action25 = 25,  // decelerate / reverse
    Action26 = 26,
    Action27 = 27,
    Action28 = 28,
    Action29 = 29,  // quick-select stellar 1..4
    Action30 = 30,
    Action31 = 31,
    Action32 = 32,
    Action33 = 33,  // debug: spawn ship
    Action34 = 34,  // debug: destroy target
    Action35 = 35,  // read in ship-disabled/dying gate (line 107); debug bank
    Action36 = 36,  // debug: disable target
    Action37 = 37,  // debug: capture target
    Action38 = 38,  // debug: reload + refuel
    Action39 = 39,
    Action40 = 40,  // debug: memory report
    Action41 = 41,  // debug: next ship class
    Action42 = 42,  // debug: prev ship class
    Action43 = 43,
    Action44 = 44,
}
