using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1006115c (EV Override-11.c 40627-40632) — a probability roll gated by the
// player's combat rating: rolls RNG(1344) and returns whether roll + 256 <= PlayerCombatRating
// (a higher rating passes more often). UpdateShipAiSteering uses it to gate an aligning
// maneuver for smart, light ships. Takes no ship input — it reads only the global rating.
public static class PassesCombatRatingRoll
{
    public static bool Run()
    {
        short randomRoll = (short)SeedEvoRng.Run(1344);
        return randomRoll + 256 <= WorldState.PlayerCombatRating;
    }
}
