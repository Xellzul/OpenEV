using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10007bc4 (EV Override-11.c lines 4327-4360).
public static class IsCandidateEngagingObserver
{
    public static bool Run(int observer, int candidate)
    {
        var cRec = ShipTable.FromPtr(candidate);
        var oRec = ShipTable.FromPtr(observer);

        if (cRec.IsActive != 0 && !ShipDerivedStats.IsDisabled(cRec) &&
            cRec.OwnerSlot != oRec.SlotIndex)
        {
            if (cRec.TargetSlot == oRec.SlotIndex && IsEngagingAiState(cRec.AiState))
            {
                return true;
            }
            // NOTE (original-game quirk kept, OGB-46): re-checks the CANDIDATE's own
            // SlotIndex/TargetSlot/AiState against the fleet (not the OBSERVER's, which this
            // loop never reads) — faithful to the decompile/ASM as-is; see ORIGINAL_GAME_BUGS.md.
            for (short fleetIndex = 1; fleetIndex < ShipTable.Count; fleetIndex = (short)(fleetIndex + 1))
            {
                if (cRec.SlotIndex == GameData.Ships[fleetIndex].OwnerSlot &&
                    GameData.Ships[fleetIndex].IsActive != 0 &&
                    cRec.TargetSlot == fleetIndex &&
                    fleetIndex != cRec.SlotIndex &&
                    IsEngagingAiState(cRec.AiState))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // FUN_10007bc4 4327-4360 — a candidate whose AiState is one of these six is busy on a
    // directive (inspect/refuel/escort/hyper-with/return-to/guard) that overrides simply
    // targeting the observer, so a TargetSlot match alone doesn't count as "engaging".
    private static bool IsEngagingAiState(ShipAiState state) =>
        state != ShipAiState.Inspect &&
        state != ShipAiState.Refuel &&
        state != ShipAiState.EscortParent &&
        state != ShipAiState.HyperWithParent &&
        state != ShipAiState.ReturnToParent &&
        state != ShipAiState.GuardPlayer;
}
