using System;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Boot;

// Port of FUN_10053ab0 (EV Override-11.c lines 34237-34382): resets the player
// ship and per-world runtime state at game entry / new pilot. `fullReset` also
// wipes credits, the per-system explored/kill tables, and the local map.
//
// Fully managed: scalar world-state in WorldState, the per-system status table in
// GalaxyMapGlobals.SystemStatusStore (old ptr slot 0x10080b54), and the star jitter/
// drift pairs in WorldState (old raw cells 0x10080ddc/0x10080de0).
public static class InitGameWorldState
{
    public static void Run(byte fullReset)
    {
        var s = ShipTable.Player;

        s.PosY = WorldState.SpawnPosDefault;
        s.PosX = WorldState.SpawnPosDefault;
        s.VelY = WorldState.SpawnVelDefault;
        s.VelX = WorldState.SpawnVelDefault;
        s.Heading = 0;
        s.ShipClass = 0;
        s.IsActive = 1;
        s.NavMode = -1;
        s.Govt = -1;
        s.AiBehaviorType = ShipAiType.Inactive;
        s.CurrentSystem = 0;

        s.Shield = (float)(int)ShipDerivedStats.EffectiveShieldMax(s);
        short fuelMax = (short)ShipDerivedStats.EffectiveFuelMax(s);
        // `(double)CONCAT44(0x43300000, x ^ 0x80000000) - <i2d-bias>` is the PPC signed-int→
        // double idiom == (double)(int)x (see Math.MathConstants — the bias copy at 0x10082150
        // carries no dedicated const since every reader collapses the same way).
        s.Fuel = (float)(int)fuelMax;

        s.TargetSlot = -1;
        s.NavTargetSpob = -1;
        s.NavMode = -1;             // faithful duplicate of the write above (decompile sets +0x2a twice)
        s.SelectedWeaponSlot = -1;
        s.OwnerSlot = -1;
        s.GrudgeMissionIndex = -1;
        s.PersIndex = -1;
        s.AiManeuverState = ShipManeuverState.None;
        s.AiState = ShipAiState.Idle;
        s.DeathTimer = WorldState.SpawnField1cDefault;
        s.PilotSkillScale = WorldState.SpawnField20Default;
        s.JumpWindupTimer = 0;
        s.PriorSystem = -1;

        // The weapon-slot arrays are stride 0x28 starting at +0x74. The original `int*`
        // indexing (`_DAT_1008a4f8 + i*10 + 0x1d`) is a POINTER index, i.e. byte
        // (i*10 + 0x1d)*4 = i*0x28 + 0x74; dropping that x4 would write byte i*10+0x1d,
        // which at i=8 lands on byte 0x6d — the ship's active flag — silently deactivating
        // the player. (The managed WeaponSlotType/Ammo arrays restore the layout.)
        var cls = GameData.ShipClasses[s.ShipClass];
        for (short i = 0; i < ShipRecord.WeaponSlotCount; i++)
        {
            s.WeaponSlotType[i] = cls.DefaultWeaponType[i];
            s.WeaponSlotAmmo[i] = cls.DefaultWeaponAmmo[i];
        }
        Array.Fill(GalaxyMapGlobals.NavHistory, (short)-1);
        Array.Fill(OwnedOutfitGrid.Store, (short)0);
        Array.Fill(s.CargoHold, (short)0);
        foreach (var junk in GameData.Junk)
        {
            junk.PlayerQty = 0;
        }

        // Reset the remaining world-state pointer cells and flags to their new-pilot defaults.
        WorldState.RespawnCounter = -1;
        SpaceportGlobals.BbsLastSpob = -1;
        WorldState.WorldCountdown = -1;
        WorldState.HyperCountdown = -1;
        WorldState.LandingTargetSpob = -1;
        WorldState.LandingApproachState = -1;
        // 0x10086ad0 / 0x10086ae0 are the SAME Mac cells the spaceport-comm dialog owns as
        // DialogScratch.SpaceportSelCellA (the bar-greeting variant seed read by
        // BuildBarDescription) and SpaceportBribeRoll; reseed/invalidate them here so the
        // in-dialog readers observe the world-init values.
        DialogScratch.SpaceportSelCellA = (short)SeedEvoRng.Run(1500);
        DialogScratch.SpaceportBribeRoll = -1;
        WorldState.AiTickFlagCb = 0;
        WorldState.AiTickFlagCa = 0;
        WorldState.UiSuppressGateB = 0;
        WorldState.UiSuppressGateA = 0;
        WorldState.TutorialHintPhase = 0x7fff;
        WorldState.HudBlinkCountdown = -1;
        WorldState.ClearShotsFlag = 1;
        WorldState.ClearCarriedSpritesFlag = 1;
        WorldState.ClearExplosionsFlag = 1;
        WorldState.ClearStreaksFlag = 1;
        WorldState.FlagF3c3 = 0;
        WorldState.FlagF3c4 = 0;
        WorldState.IsCloaked = false;
        GalaxyMapState.PreviewSystem = -1;
        WorldState.FlashChatterCountdown = 0;

        // Abort any active mission tied to a flagged government, then clear the flags.
        for (short g = 0; g < MissionStateTable.Count; g++)
        {
            if (GameData.MissionStates[g].IsActive != 0)
            {
                AbortMission.Run(g);
            }
            GameData.MissionStates[g].IsActive = 0;
        }
        // Randomize the bar/mission odds table (1..100) and reset the jitter pairs.
        for (short i = 0; i < GameData.RandomOdds.Length; i++)
        {
            short roll = (short)SeedEvoRng.Run(100);
            GameData.RandomOdds[i] = (short)(roll + 1);
        }
        for (short i = 0; i < WorldState.StarDrift.Length; i++)
        {
            WorldState.StarDrift[i] = 100;
            WorldState.StarJitter[i] = 100;
        }

        if (fullReset != 0)
        {
            s.Credits = 10000;
            for (short i = 0; i < SystTable.Count; i++)
            {
                SystTable.Store[i].Visited = 0;
                short syGovt = SystTable.Store[i].Govt;
                if (syGovt < 0 || syGovt >= GovtTable.Count)
                {
                    GalaxyMapGlobals.SetSystemStatus(i, 0);
                }
                else
                {
                    GalaxyMapGlobals.SetSystemStatus(i,
                        GameData.Governments[syGovt].InitialRecord);
                }
            }
            // Mark the current system's shown neighbours as explored on the local map.
            foreach (short link in SystTable.Store[s.CurrentSystem].HyperLink)
            {
                if (link != -1 && SystTable.Store[link].ShownFlag != 0)
                {
                    SystTable.Store[link].Visited = 1;
                }
            }
            foreach (var nebula in MapNebulaTable.Store)
            {
                nebula.Charted = 0;
            }
        }

        // Centre the galaxy map on the current system.
        WorldState.MapViewCentreX = SystTable.Store[s.CurrentSystem].XPos;
        WorldState.MapViewCentreY = SystTable.Store[s.CurrentSystem].YPos;
    }
}
