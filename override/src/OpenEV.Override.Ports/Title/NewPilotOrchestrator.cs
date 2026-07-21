using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.GalaxyMap;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10045b9c (EV Override-11.c lines 29060-29170).
// New Pilot orchestrator (DispatchTitleEvent button 0): prompts for the pilot
// name + strict-play toggle via NewPilotDialog, confirms overwrite through
// AlertModal_TwoButton if the file already exists, captures a christened ship
// name via AlertModal_ThreeButton, resets the world state for a fresh pilot,
// and commits it via PilotSave.
public static class NewPilotOrchestrator
{
    public static void Run()
    {
        // Toggled + written back by NewPilotDialog below; saved to WorldState.StrictPlay once the pilot commits.
        byte strictPlay = 0;

        Graphics.SetGamePortAndDevice.Run();
        MacToolbox.InvalRect(Core.Model.GlobalState.PortRect);

        // One of the three random default pilot names (STR# 128 items 1-3).
        int nameRoll = (int)Misc.SeedEvoRng.Run(3);
        string defaultName = MacToolbox.GetIndString(128, (short)(nameRoll + 1));
        // NewPilotDialog (FUN_10046054).
        byte confirmed = (byte)NewPilotDialog.Run(defaultName, 24, ref strictPlay);
        if (confirmed != 0)
        {
            // Restore the screen from the title backdrop GWorld over the port rect.
            // Both src and dst use PortRect here — matches the ASM, not a mistake.
            MacToolbox.CopyBits(
                Graphics.Model.RenderGlobals.BackdropGWorld + 2,
                Core.Model.GlobalState.ActivePortPixmap + 2,
                Core.Model.GlobalState.PortRect,
                Core.Model.GlobalState.PortRect,
                0,
                0);

            // Port-only safety cap (not in the decompile — FUN_1007615c is an unbounded
            // strcpy with no length check at all): the "The "-stripped player name, capped at
            // 253 bytes. This doesn't match the 31-char cap this same field gets elsewhere once
            // it becomes a real file/resource name (LoadPilotPluginFile.cs, SavePilotFile.cs) —
            // flagging the mismatch, not changing the value (that's a behavior change out of
            // scope for a tidy pass).
            string candidateName = Text.StripLeadingThe.Run(Pilot.Model.PilotIdentity.CapturedNameEntry);
            if (candidateName.Length > 253) candidateName = candidateName.Substring(0, 253);

            // Host-bridge fixup (not in the original): Mgr_FSMakeFSSpecByName only probes the
            // real fork-file store for names registered via RegisterManagedForkFile — any
            // unregistered name unconditionally reports fnfErr ("doesn't exist") without ever
            // running the real existence check. Without registering the candidate name here, the
            // overwrite confirm below would never fire, even for a genuine name collision, silently
            // clobbering an existing pilot file. Register it now (PilotSave registers the same
            // name again before writing).
            MacToolbox.RegisterManagedForkFile(candidateName);
            confirmed = (byte)Pilot.PilotFileExistsOnDefaultVolume.Run(candidateName);
            if (confirmed != 0)
            {
                Graphics.SetGamePortAndDevice.Run();
                DrawPilotInfo.Run(1);
                // GameToc-0x41a1 = 0x100844bf data-seg Pascal (dumped).
                confirmed = (byte)AlertModal_TwoButton.Run(
                    "A pilot file by that name already exists. Is it okay to replace it with this new pilot?");
                if (confirmed == 0)
                {
                    return;
                }
                // Same restore as above.
                MacToolbox.CopyBits(
                    Graphics.Model.RenderGlobals.BackdropGWorld + 2,
                    Core.Model.GlobalState.ActivePortPixmap + 2,
                    Core.Model.GlobalState.PortRect,
                    Core.Model.GlobalState.PortRect,
                    0,
                    0);
            }

            // New pilots always start in ship class 0 (the shuttle).
            string christen = Core.Model.StaticData.ChristenPrefix
                            + Core.Model.StaticData.ShipLongNames[0]
                            + Core.Model.StaticData.Colon;
            // One of the three random default ship names (STR# 128 items 4-6).
            int shipNameRoll = (int)Misc.SeedEvoRng.Run(3);
            string defaultShipName = MacToolbox.GetIndString(128, (short)(shipNameRoll + 4));
            confirmed = (byte)AlertModal_ThreeButton.Run(christen, defaultShipName, 20);
            if (confirmed != 0)
            {
                GalaxyMapState.TradeKeyLock = 0;
                Boot.InitGameWorldState.Run(1);
                Outfit.ResetCommodityPriceLimits.Run(1);
                Pilot.InitializeNewPilotWorld.Run();
                Resource.LoadSpobAndStellarResources.Run();
                Systems.CleanupSystNpcs.Run(1);
                MarkGalaxyMapClustersForSyst.Run(Core.Model.GameData.Player.CurrentSystem);
                Core.Model.WorldState.MapViewCentreX = Systems.Model.SystTable.Store[Core.Model.GameData.Player.CurrentSystem].XPos;
                Core.Model.WorldState.MapViewCentreY = Systems.Model.SystTable.Store[Core.Model.GameData.Player.CurrentSystem].YPos;

                Core.Model.WorldState.NoAsteroidsFlag = 1;
                Core.Model.WorldState.ClearStreaksFlag = 1;
                Core.Model.WorldState.ClearExplosionsFlag = 1;
                Core.Model.WorldState.ClearCarriedSpritesFlag = 1;
                Core.Model.WorldState.ClearShotsFlag = 1;

                // Ship name the player typed/accepted in the christen dialog above —
                // AlertModal_ThreeButton captured it into CapturedNameEntry (the earlier
                // candidateName capture already consumed the buffer's prior contents, so
                // the two don't collide). Source this from CapturedNameEntry, never the
                // ship-class name — the decompile never does.
                string shipName = Pilot.Model.PilotIdentity.CapturedNameEntry;
                if (shipName.Length > 63) shipName = shipName.Substring(0, 63);   // Str63 cap; input is already bounded to 20
                Pilot.Model.PilotIdentity.ShipName = Text.StripLeadingThe.Run(shipName);
                Pilot.Model.PilotIdentity.Name = candidateName;

                Core.Model.GameDate.SetCurrentToHostClock();
                short versionStamp = (short)Resource.ReadStoredVersionStamp.Run();
                var gameClock = Core.Model.GameDate.Current;
                gameClock.Year = (short)(gameClock.Year + versionStamp);
                Core.Model.GameDate.Current = gameClock;

                Core.Model.WorldState.PilotLoaded = true;
                Core.Model.WorldState.StrictPlay = strictPlay;

                // FAITHFUL: the original zeroes the owned-outfit grid TWICE, in two identical
                // back-to-back loops (decompile 29130-29135) — redundant in the original,
                // kept for bug-for-bug parity.
                for (short i = 0; i < Outfit.Model.OwnedOutfitGrid.Count; i++)
                {
                    Outfit.Model.OwnedOutfitGrid.Store[i] = 0;
                }
                for (short i = 0; i < Outfit.Model.OwnedOutfitGrid.Count; i++)
                {
                    Outfit.Model.OwnedOutfitGrid.Store[i] = 0;
                }

                for (short i = 0; i < Ship.Model.ShipRecord.WeaponSlotCount; i++)
                {
                    Core.Model.GameData.Player.WeaponSlotType[i] =
                        Core.Model.GameData.ShipClasses[Core.Model.GameData.Player.ShipClass].DefaultWeaponType[i];
                    Core.Model.GameData.Player.WeaponSlotAmmo[i] =
                        Core.Model.GameData.ShipClasses[Core.Model.GameData.Player.ShipClass].DefaultWeaponAmmo[i];
                }
                Outfit.RebuildOwnedOutfitsFromMarket.Run();

                short dockedSpobIndex = -1;
                for (short i = 0; i < Systems.Model.SystRecord.StellarLinkCount; i++)
                {
                    if (Systems.Model.SystTable.Store[0].StellarLink[i] != -1)
                    {
                        dockedSpobIndex = Systems.Model.SystTable.Store[0].StellarLink[i];
                        break;
                    }
                }

                Core.Model.GameData.Player.DeathTimer = -1.0f;
                Core.Model.WorldState.WorldCountdown = -1;
                Core.Model.WorldState.TutorialHintPhase = -3;

                if (dockedSpobIndex == -1)
                {
                    PilotSave.Run(0);
                }
                else
                {
                    PilotSave.Run(dockedSpobIndex);
                }
                Combat.RunFleetSpawner.Run(Core.Model.GameData.Player.CurrentSystem);
                Misc.RecomputeWorldVisibility.Run();
                Systems.Asteroids.Init();
            }
        }
        return;
    }
}
