using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_10052fa4 (EV Override-11.c lines 34071-34236): the full new-pilot
// WORLD RESET — every ship slot, ship class, spob, syst, mission, fleet, nebula,
// junk, projectile, debris, beam, govt and nav-history record back to its blank
// state, the commodity base prices reseeded from STR resources, and the game clock
// restarted at the EVO start date (the host date + 250 years).
public static class ResetWorldStateForNewPilot
{
    public static void Run()
    {
        for (short i = 0; i < ShipTable.Count; i++)
        {
            var ship = GameData.Ships[i];
            ship.SlotIndex = i;
            ship.Heading = 0;
            ship.HeadingPrev = 0;
            ship.CurrentSystem = -1;
            ship.ShipClass = 0;
            ship.HasWorldSpriteNode = 0;
            ship.SalvageClaimed = 0;
            ship.IsActive = 0;
            ship.Credits = 0;
            ship.DefendedSpobIndex = -1;
            ship.HasTargetLock = 1;
            ship.TargetSlot = -1;
            ship.SelectedWeaponSlot = -1;
            ship.Shield = 0.0f;
            ship.AiBehaviorType = ShipAiType.None;
            ship.AiActionTimer = 0;
            ship.NavMode = -1;
            ship.NavTargetSpob = -1;
            ship.LastVictimSlot = -1;
            ship.DockedSpobIndex = -2;
            ship.JumpWindupTimer = 0;
            ship.Fuel = WorldState.SpawnFuelDefault;
            ship.OwnerSlot = -1;
            ship.DeathTimer = WorldState.SpawnVelDefault;
            ship.PilotSkillScale = WorldState.SpawnField20Default;
            ship.AltFireSide = 1;
            for (short j = 0; j < ShipRecord.WeaponSlotCount; j++)
            {
                ship.WeaponSlotReload[j] = WorldState.SpawnVelDefault;
                ship.WeaponSlotType[j] = 0;
                ship.WeaponSlotAmmo[j] = 0;
            }
            for (short j = 0; j < ShipRecord.CargoHoldCount; j++)
            {
                ship.CargoHold[j] = 0;
            }
        }
        for (short i = 0; i < ShipClassTable.Count; i++)
        {
            var cls = GameData.ShipClasses[i];
            cls.TechLevel = 9999;   // unavailable until the loader fills it
            cls.Shield = 1;
            for (short j = 0; j < cls.DefaultWeaponType.Length; j++)
            {
                cls.DefaultWeaponType[j] = 0;
                cls.DefaultWeaponAmmo[j] = 0;
            }
        }
        for (short i = 0; i < SpobTable.Count; i++)
        {
            var spob = GameData.Spobs[i];
            spob.Spawned = 0;
            spob.Visible = 0;
            spob.SpriteId = 0;
            spob.TechLevel = 0;
            spob.TradingEnabled = 0;
            spob.TributeAccrualTicks = 0;
            spob.System = -1;
            for (short j = 0; j < spob.SpecialTech.Length; j++)
            {
                spob.SpecialTech[j] = 0;
            }
        }
        for (short i = 0; i < SystTable.Count; i++)
        {
            var syst = SystTable.Store[i];
            syst.ShownFlag = 0;
            syst.Govt = -32767;
            syst.AsteroidCount = 0;
            syst.Interference = 0;
            syst.Visited = 0;
            syst.Govt = -1;   // re-clears Govt to -1 (faithful second write of +0x04)
            syst.YPos = 0;
            syst.XPos = 0;
            for (short j = 0; j < SystRecord.HyperLinkCount; j++)
            {
                syst.HyperLink[j] = -1;
            }
            for (short j = 0; j < syst.StellarLink.Length; j++)
            {
                syst.StellarLink[j] = -1;
            }
            for (short j = 0; j < 4; j++)
            {   // 4 fleet-spawn pairs: weights [0..3], counts [4..7]
                syst.FleetSpawn[j] = 0;
                syst.FleetSpawn[4 + j] = 25;
            }
            // The strncpy source is the empty data-seg string, so this just clears the name.
            System.Array.Clear(syst.Name, 0, syst.Name.Length);
        }
        for (short i = 0; i < PersTable.Count; i++)
        {
            var pers = GameData.Pers[i];
            pers.AvailableFlag = 0;
            pers.LinkSyst = 0;
            for (short j = 0; j < pers.WeaponType.Length; j++)
            {
                pers.WeaponType[j] = 0;
                pers.WeaponAmmo[j] = 0;
            }
        }
        for (short i = 0; i < FleetTable.Count; i++)
        {
            GameData.Fleets[i].LeadShipType = -1;
            GameData.Fleets[i].Govt = -1;
            GameData.Fleets[i].MissionBit = -1;
        }
        for (short i = 0; i < AsteroidTable.Count; i++)
        {
            GameData.Asteroids[i].Active = 0;
            GameData.Asteroids[i].Spawned = 0;
        }
        for (short i = 0; i < MapNebulaTable.Count; i++)
        {
            MapNebulaTable.Store[i].Charted = 0;
        }
        for (short i = 0; i < JunkTable.Count; i++)
        {
            GameData.Junk[i].BoughtAtSpob = -1;
            GameData.Junk[i].SoldAtSpob = -1;
        }
        for (short i = 0; i < ProjectileTable.Count; i++)
        {
            GameData.Projectiles[i].LifeRemaining = ProjectileRecord.Killed;
        }
        for (short i = 0; i < DebrisTable.Count; i++)
        {
            GameData.Debris[i].LifeRemaining = DebrisRecord.Killed;
        }
        for (short i = 0; i < BeamTable.Count; i++)
        {
            GameData.Beams[i].Life = BeamRecord.Killed;
            GameData.Beams[i].OwnerSlot = -1;
        }
        for (short i = 0; i < GalaxyMapGlobals.NavHistory.Length; i++)
        {
            GalaxyMapGlobals.NavHistory[i] = -1;
        }
        ReseedBackgroundNebulae.Run();
        // Commodity base prices: STR 0x2454+i overrides, else STR# 0xfa4 entry i+1,
        // parsed into BasePrice[0..5] (only 6 of the 10 slots are seeded).
        for (short i = 0; i < 6; i++)
        {
            string priceStr = TryLoadStr.RunString((short)(i + 0x2454))
                              ?? MacToolbox.GetIndString(0xfa4, (short)(i + 1));
            CommodityPricing.BasePrice[i] = (short)MacToolbox.StringToNum(priceStr);
        }
        for (short i = 0; i < GameData.RandomOdds.Length; i++)
        {
            short odds = (short)SeedEvoRng.Run(100);
            GameData.RandomOdds[i] = (short)(odds + 1);
        }
        for (short i = 0; i < GovtTable.Count; i++)
        {
            GameData.Governments[i].Enemy = -1;
            GameData.Governments[i].Ally = -1;
        }
        WorldState.StrictPlay = 1;
        WorldState.HudBlinkCountdown = -1;
        WorldState.LandingTargetSpob = -1;
        WorldState.LandingApproachState = -1;
        // The EVO start date is the real date 250 years on (same as InitializeNewPilotWorld).
        GameDate.SetCurrentToHostClock();
        var gameClock = GameDate.Current;
        gameClock.Year = (short)(gameClock.Year + 250);
        GameDate.Current = gameClock;
        // The decompile clears a middle-name buffer here (0x1009030c) — a dead write with
        // no reader (see PilotSaveSources); the pilot/ship names default to "No Name".
        PilotIdentity.Name = "No Name";
        PilotIdentity.ShipName = "No Name";
    }
}
