namespace OpenEV.Override.Ports.Combat;

using System;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Misc;

// FUN_1006e344 — EV Override-11.c lines 45056-45128. Finds the first free ship slot (IsActive == 0
// and no world sprite node) in [1, 36 - |slotLimit|), initialises it as a fresh ship in systemIndex,
// and returns that slot — or -1 if none is free.
public static class AllocateShipSlot
{
    public static int Run(short systemIndex, short slotLimit)
    {
        int foundSlot = -1;
        for (short slotIndex = 1; slotIndex < ShipTable.Count - Math.Abs(slotLimit); slotIndex++)
        {
            var candidate = ShipTable.Ships[slotIndex];
            if (candidate.IsActive == 0 && candidate.HasWorldSpriteNode == 0)
            {
                foundSlot = slotIndex;
                break;
            }
        }
        if (foundSlot == -1)
            return -1;

        var ship = ShipTable.Ships[foundSlot];
        ship.IsActive = 1;
        ship.CurrentSystem = systemIndex;
        ship.DudeSpawnIndex = -1;
        ship.ShipClass = 0;
        ship.AiBehaviorType = ShipAiType.WimpyTrader;
        ship.Govt = -1;
        ship.VelY = ShipStatConstants.SpawnZeroDefault;
        ship.VelX = ShipStatConstants.SpawnZeroDefault;
        ship.NavMode = -1;
        ship.JumpWindupTimer = 0;
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
        ship.SalvageClaimed = 0;
        ship.GrudgeMissionIndex = -1;
        ship.ProvokedFlag = 0;
        ship.AiActionTimer = 0;
        ship.OwnerSlot = -1;
        ship.TargetSlot = -1;
        ship.NavTargetSpob = -1;
        ship.DeathTimer = ShipStatConstants.SpawnZeroDefault;
        ship.PilotSkillScale = (float)SkillVariationRoll.Run(ship.ShipClass);
        ship.Credits = 0;
        ship.AiCourage = (short)(SeedEvoRng.Run(3) ^ 2);
        ship.DefendedSpobIndex = -1;
        ship.IsTractored = 0;
        ship.IsCarriedFighter = 0;
        ship.HailQuoteSpoken = 0;
        ship.SpawningMissionSlot = -1;
        ship.PersIndex = -1;
        ship.DesiredAccel = ShipStatConstants.SpawnZeroDefault;
        ship.DesiredSpeed = ShipStatConstants.SpawnZeroDefault;
        ship.HasSelectedWeapon = 0;
        ship.HasAfterburner = 0;
        ship.PosX = (float)((short)SeedEvoRng.Run(1500) - 750);
        ship.PosY = (float)((short)SeedEvoRng.Run(1500) - 750);
        return foundSlot;
    }
}
