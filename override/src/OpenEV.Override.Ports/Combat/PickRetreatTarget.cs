using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_10009210 (EV Override-11.c 4910-4956). Counts the live, same-system NPCs (slots 1..35,
// excluding self) that are in AiState 3 or 4 and targeting slot 0 (the player); if any exist,
// randomly picks one as this ship's TargetSlot and switches the ship to AiState 4. If none,
// clears TargetSlot (-1).
public static class PickRetreatTarget
{
    public static void Run(ShipRec ship)
    {
        short candidateCount = 0;
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            if (shipIndex == ship.SlotIndex || shipIndex == 0)   // shipIndex == 0 can't occur (loop starts at 1); kept as in the original
                continue;
            if (IsRetreatCandidate(ship, ShipTable.Ships[shipIndex]))
                candidateCount++;
        }

        if (candidateCount < 1)
        {
            ship.TargetSlot = -1;
            return;
        }

        // Re-roll a random NPC slot until it is one that qualifies. (The pick == 0 guard can never
        // fire — Run returns [0, Count-1) so the +1 is always >= 1 — but is kept as in the original.)
        short pick;
        do
        {
            do
            {
                do
                {
                    pick = (short)(SeedEvoRng.Run(ShipTable.Count - 1) + 1);
                }
                while (pick == ship.SlotIndex);
            }
            while (pick == 0);
        }
        while (!IsRetreatCandidate(ship, ShipTable.Ships[pick]));

        ship.TargetSlot = pick;
        ship.AiState = ShipAiState.AttackShip;
    }

    // Decompile 4922-4928 — `other` qualifies: a live (not disabled, not dying) NPC in the same
    // system as `ship`, targeting slot 0, and itself in AiState 3 or 4.
    private static bool IsRetreatCandidate(ShipRec ship, ShipRec other)
    {
        return !ShipDerivedStats.IsDisabled(other)
            && !ShipDerivedStats.IsDyingOrDestroyed(other)
            && other.TargetSlot == 0
            && (other.AiState == ShipAiState.AttackShip || other.AiState == ShipAiState.DefendRetreat)
            && ship.CurrentSystem == other.CurrentSystem;
    }
}
