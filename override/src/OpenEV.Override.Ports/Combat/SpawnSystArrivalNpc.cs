using System;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10065530 (EV Override-11.c lines 42281-42447). Finds a free ship
// slot in [1, 36 - |activeShipCount|) and spawns an NPC there, weighted-rolled
// from the system's fleet-spawn table, arriving at the primary spob or
// scattered randomly in space.
public static class SpawnSystArrivalNpc
{
    public static int Run(short systemIndex, ushort activeShipCount)
    {
        var syst = SystTable.Store[systemIndex];

        for (short slotIndex = 1; slotIndex < ShipTable.Count - Math.Abs((short)activeShipCount); slotIndex++)
        {
            var ship = Core.Model.GameData.Ships[slotIndex];
            if (ship.IsActive != 0 || ship.HasWorldSpriteNode != 0)
            {
                continue;
            }

            // Roll a ship-class group weighted by the system's fleet-spawn table.
            short roll = (short)SeedEvoRng.Run(100);
            short chosen = -1;
            var classWeights = new short[4];
            for (short c = 0; c < 4; c++)
            {
                classWeights[c] = 0;
                for (short s = 0; s <= c; s++)
                {
                    classWeights[c] = (short)(classWeights[c] + syst.FleetSpawn[4 + s]);
                }
            }
            for (short c = 3; c >= 0; c--)
            {
                if ((short)(roll + 1) <= classWeights[c] && syst.FleetSpawn[c] >= 0)
                {
                    chosen = c;
                }
            }
            if (chosen == -1)
            {
                continue;
            }

            short dudeIndex = syst.FleetSpawn[chosen];
            var dude = Core.Model.GameData.DudeSpawns[dudeIndex];
            short classRoll = (short)PickWeightedSlot.Run(dude);
            if (classRoll < 0 || classRoll >= 4)
            {
                continue;
            }

            ship.IsActive = 1;
            ship.CurrentSystem = systemIndex;
            ship.DudeSpawnIndex = dudeIndex;
            ship.ShipClass = dude.ShipClass[classRoll];
            ship.Govt = dude.Govt;
            var cls = Core.Model.GameData.ShipClasses[ship.ShipClass];
            ship.AiBehaviorType = dude.AiType < ShipAiType.WimpyTrader ? cls.InherentAI : dude.AiType;

            if (ship.AiBehaviorType == ShipAiType.Warship && syst.StellarLink[0] != -1
                && ShipStatConstants.ZeroDouble == (double)cls.Speed)
            {
                // Arrive next to the system's primary spob, moving at the arrival speed.
                short arrivalSpob = syst.StellarLink[0];
                ship.PosX = (float)Core.Model.GameData.Spobs[arrivalSpob].XPos;
                ship.PosY = (float)Core.Model.GameData.Spobs[arrivalSpob].YPos;
                EvMath.OffsetByHeading((double)ShipStatConstants.ArrivalSpeed,
                    (int)SeedEvoRng.Run(360), ref ship.PosX, ref ship.PosY);
            }
            else
            {
                // Scatter randomly in open space.
                ship.PosX = (float)((short)SeedEvoRng.Run(1500) - 750);
                ship.PosY = (float)((short)SeedEvoRng.Run(1500) - 750);
            }

            ship.VelY = ShipStatConstants.SpawnZeroDefault;
            ship.VelX = ShipStatConstants.SpawnZeroDefault;
            ship.NavMode = -1;
            ship.JumpWindupTimer = 0;
            ship.AiState = (short)ShipAiState.Idle;               // ship is the raw ShipRecord, so cast the enum
            ship.AiManeuverState = (short)ShipManeuverState.None;  // to the short backing field
            ship.SalvageClaimed = 0;
            ship.GrudgeMissionIndex = -1;
            ship.ProvokedFlag = 0;
            ship.AiActionTimer = 0;
            ship.OwnerSlot = -1;
            ship.TargetSlot = -1;
            ship.NavTargetSpob = -1;
            ship.DeathTimer = ShipStatConstants.SpawnZeroDefault;
            ship.PilotSkillScale = (float)SkillVariationRoll.Run(ship.ShipClass);
            ship.Credits = 10000;
            ship.AiCourage = (short)(SeedEvoRng.Run(3) ^ 2);
            // Shield holds a genuine numeric value here (see ShipClassRecord.Shield), not a
            // bit-pattern reinterpret.
            ship.Shield = cls.Shield;
            ship.Fuel = (float)cls.BaseFuel;
            ship.DefendedSpobIndex = -1;
            ship.IsTractored = 0;
            ship.IsCarriedFighter = 0;
            ship.HailQuoteSpoken = 0;
            ship.HasAfterburner = (byte)(HasAfterburner.Run(ShipTable.Ships[slotIndex]) ? 1 : 0);
            ship.SpawningMissionSlot = -1;
            ship.PersIndex = -1;
            ship.DesiredAccel = ShipStatConstants.SpawnZeroDefault;
            ship.DesiredSpeed = ShipStatConstants.SpawnZeroDefault;
            ship.HasSelectedWeapon = 0;

            for (int w = 0; w < ShipRecord.WeaponSlotCount; w++)
            {
                ship.WeaponSlotType[w] = cls.DefaultWeaponType[w];
                if ((WeaponGuidanceType)Core.Model.GameData.Weapons[w].GuidanceType == WeaponGuidanceType.CarriedShip)
                {
                    ship.WeaponSlotAmmo[w] = cls.DefaultWeaponAmmo[w];
                }
                else
                {
                    ship.WeaponSlotAmmo[w] = (short)(int)(ShipStatConstants.NpcWeaponAmmoScale * cls.DefaultWeaponAmmo[w]);
                }
            }

            ShipAi.ResetAiToIdle(ShipTable.Ships[slotIndex]);
            return slotIndex;
        }

        return -1;
    }
}
