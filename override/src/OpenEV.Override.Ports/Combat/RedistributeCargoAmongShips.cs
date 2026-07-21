using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Combat;

// FUN_1005f4a4 (EV Override-11.c 39694-39769) — rescale the player's held cargo + junk to the
// fleet's cargo capacity: ratio = the source ship's cargo Holds (or the player's EffectiveCargoMax
// when slot 0) / (player capacity + qualifying escorts' Holds); each CargoHold commodity and Junk
// PlayerQty becomes value × (1 − ratio), clamped at 0.
public static class RedistributeCargoAmongShips
{
    public static void Run(short sourceShipSlot)
    {
        var player = ShipTable.Player;

        double sourceCapacity;
        if (sourceShipSlot == 0)
            sourceCapacity = (short)ShipDerivedStats.EffectiveCargoMax();
        else
            sourceCapacity = GameData.ShipClasses[GameData.Ships[sourceShipSlot].ShipClass].Holds;

        double totalCapacity = (short)ShipDerivedStats.EffectiveCargoMax();
        for (short shipIndex = 1; shipIndex < ShipTable.Count; shipIndex++)
        {
            var s = ShipTable.Ships[shipIndex];
            if (s.IsActive == 0)
                continue;
            if (ShipDerivedStats.IsDyingOrDestroyed(s) && shipIndex != sourceShipSlot)
                continue;
            if (player.CurrentSystem != s.CurrentSystem
                || s.OwnerSlot != 0 || s.AiBehaviorType != ShipAiType.Escort || s.GrudgeMissionIndex != -1
                || GameData.ShipClasses[s.ShipClass].InherentAI >= ShipAiType.Warship)
                continue;
            totalCapacity += GameData.ShipClasses[s.ShipClass].Holds;
        }

        // Dead in the original (and correct that it's unused): it totals the player's held cargo + junk
        // and scales that by the capacity ratio, but the result is never read — the per-commodity scaling
        // below is what actually rescales the cargo. The aggregate is computed and discarded.
        double heldCargoJunkTotal = 0.0;
        for (short cargoBay = 0; cargoBay < player.CargoHold.Length; cargoBay++)
            heldCargoJunkTotal += player.CargoHold[cargoBay];
        for (short junkIndex = 0; junkIndex < JunkTable.Count; junkIndex++)
            heldCargoJunkTotal += GameData.Junk[junkIndex].PlayerQty;
        _ = (sourceCapacity / totalCapacity) * heldCargoJunkTotal;

        for (short cargoBay = 0; cargoBay < player.CargoHold.Length; cargoBay++)
        {
            player.CargoHold[cargoBay] = (short)(int)-(player.CargoHold[cargoBay] * (sourceCapacity / totalCapacity)
                - player.CargoHold[cargoBay]);
            if (player.CargoHold[cargoBay] < 0)
                player.CargoHold[cargoBay] = 0;
        }

        for (short junkIndex = 0; junkIndex < JunkTable.Count; junkIndex++)
        {
            GameData.Junk[junkIndex].PlayerQty = (short)(int)-((sourceCapacity / totalCapacity) * GameData.Junk[junkIndex].PlayerQty
                - GameData.Junk[junkIndex].PlayerQty);
            if (GameData.Junk[junkIndex].PlayerQty < 0)
                GameData.Junk[junkIndex].PlayerQty = 0;
        }

        WorldState.HudStatusPanelDirty = 1;
    }
}
