using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Outfit;

// FUN_1005f33c (EV Override-11.c lines 39666-39693) — the player's total cargo
// capacity: the player's own hold plus every qualifying escort's hold. An
// escort qualifies when it's active, not dying/destroyed, in the player's
// system, owned by the player (slot 0), holding formation (AiBehaviorType 6),
// not on a grudge mission, and its class's inherent AI is below 3.
public static class TotalMassWithEscorts
{
    public static int Run()
    {
        int totalMass = ShipDerivedStats.EffectiveCargoMax();
        for (short escortIndex = 1; escortIndex < ShipTable.Count; escortIndex++)
        {
            var escort = ShipTable.Ships[escortIndex];
            if (escort.IsActive != 0 &&
                !ShipDerivedStats.IsDyingOrDestroyed(escort) &&
                GameData.Player.CurrentSystem == escort.CurrentSystem &&
                escort.OwnerSlot == 0 &&
                escort.AiBehaviorType == ShipAiType.Escort &&
                escort.GrudgeMissionIndex == -1 &&
                GameData.ShipClasses[escort.ShipClass].InherentAI < ShipAiType.Warship)
            {
                totalMass += GameData.ShipClasses[escort.ShipClass].Holds;
            }
        }
        return totalMass;
    }
}
