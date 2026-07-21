using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_1005e85c (EV Override-11.c 39294-39334) — draw the player's combat-rating rank name (STR# 0x8a)
// at the current pen, picking the tier (0-10) from PlayerCombatRating's doubling thresholds.
public static class DrawCombatRatingName
{
    public static void Run()
    {
        int combatRating = WorldState.PlayerCombatRating;
        short tierIndex = 0;
        if (combatRating > 0) tierIndex = 1;
        if (combatRating >= 100) tierIndex = 2;
        if (combatRating >= 200) tierIndex = 3;
        if (combatRating >= 400) tierIndex = 4;
        if (combatRating >= 800) tierIndex = 5;
        if (combatRating >= 1600) tierIndex = 6;
        if (combatRating >= 3200) tierIndex = 7;
        if (combatRating >= 6400) tierIndex = 8;
        if (combatRating >= 12800) tierIndex = 9;
        if (combatRating >= 25600) tierIndex = 10;
        MacToolbox.DrawString(ResourceGlobals.CombatRatingName(tierIndex));
    }
}
