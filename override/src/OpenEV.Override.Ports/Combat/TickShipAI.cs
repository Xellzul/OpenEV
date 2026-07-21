// Port of FUN_10027830 (EV Override-11.c lines 16828-19419): the player ship's per-frame
// AI/control tick — input handling, autopilot, landing/hyperspace, shield/fuel regen, etc.
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Boot;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Pilot;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Title;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

public static class TickShipAI
{
    // `ship` is the typed handle over the player ship record (decompile's float *param_1).
    // ship.Ptr is the raw record address still needed by ports that take a bare int.
    public static void Run(ShipRec ship)
    {
        var sc = new TickScratch();

        // DEAD in shipping 1.0.2 (kept faithfully, bug-for-bug): every `CheatShowAll != 0` gate
        // in this function (this abs-shield fixup + the rearm/refuel/chart-all, call-defenders/
        // match-velocity, and boarding-alarm blocks below) is gated by WorldState.CheatShowAll =
        // Mac byte unk_E021D (via TOC cell off_80F84 / 0x10080F84). That byte is uninitialised
        // BSS with 8 read-only refs and ZERO writers in the whole binary (EV_Override.asm:550679
        // decl, :168134 TOC cell with a read-only xref), so it is PERMANENTLY 0 — the SAME "debug
        // master enable" DDC-01 documents (there named TargetDebugPanelFlag). These are shipped-
        // but-dead debug/cheat hotkeys; keep them, do NOT wire them up or flip the gate. See
        // DEV_DEBUG_CODE.md DDC-01 / DDC-15.
        if (WorldState.CheatShowAll != 0 && ((int)ship.Shield < 0))
        {
            ship.Shield = (float)-(int)ship.Shield;
        }
        if ((ship.HasTargetLock == 1 && ship.TargetSlot != -1) &&
           ((GameData.Ships[ship.TargetSlot].HasWorldSpriteNode == 0 ||
            ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[ship.TargetSlot]))))
        {
            ship.TargetSlot = -1;
            WorldState.WeaponSlotDirty = 1;
        }
        if (ship.HasWorldSpriteNode == 0)
        {
            HandleDestroyedShip(ship);
            return;
        }
        if ((int)WorldState.GameFrameTickCounter % 60 == 0)
        {
            WorldState.AiTickFlagCb = WorldState.AiTickFlagCa;
            WorldState.AiTickFlagCa = (byte)(AnyShipEngaged.Run() ? 1 : 0);
            if ((WorldState.AiTickFlagCa != 0 && WorldState.AiTickFlagCb == 0) && WorldState.WorldCountdown < 1)
            {
                SndPlay.Run(CombatSoundCells.AlarmSnd, 5, 128, 128);
                if (GamePrefs.MasterVolume < 2)   // quiet master volume → HUD blink instead
                {
                    WorldState.HudBlinkCountdown = 30;
                }
            }
        }
        ship.HeadingPrev = ship.Heading;
        if (WorldState.WorldCountdown > 0)
        {
            LaunchCountdownTick(ship);
            return;
        }
        if (PlayerRequestedQuit(ship))
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
            MacToolbox.ShowCursor();
            sc.flag = (byte)AlertModal_TwoButton.Run("Are you sure you want to quit?");
            if (sc.flag == 0)
            {
                RefreshStatusPanel.Run();
            }
            else
            {
                EvoGlobals.QuitRequested = true;
                GracefulExit.Run();
            }
            MacToolbox.HideCursor();
            RepaintGameWindow.Run();
        }
        sc.shortA = (short)Keymap.TestCachedKeymapBit(0x3f);
        WorldState.AiBehaviorFlagA = (byte)(sc.shortA != 0 ? 1 : 0);
        sc.shortA = (short)Keymap.TestCachedKeymapBit(0x4);
        WorldState.AiBehaviorFlagB = (byte)(sc.shortA != 0 ? 1 : 0);
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action8)));
        if ((sc.shortA == 0 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action12)))) == 0) ||
           (ship.JumpWindupTimer > 0))
        {
            PlayerKeyLatches.AutopilotKeyLatch = false;
        }
        else if (!PlayerKeyLatches.AutopilotKeyLatch)
        {
            PlayerKeyLatches.AutopilotKeyLatch = true;
            WorldState.SpawnPulseDirty = 1;
            WorldState.LandingTargetSpob = -1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action8)));
            if (sc.shortA != 0)
            {
                ship.NavMode = -1;
                ship.NavTargetSpob = -1;
            }
            sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action12)));
            if (sc.shortA != 0 && ship.NavMode != 3)
            {
                ship.NavMode = 3;
                ship.NavTargetSpob = -1;
                EngageAutopilotToHistoryTarget.Run();
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action13)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.HyperTargetCycleKeyLatch = false;
        }
        else if (((!PlayerKeyLatches.HyperTargetCycleKeyLatch) && ship.JumpWindupTimer < 1) &&
                (ship.NavMode == 3))
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
            PlayerKeyLatches.HyperTargetCycleKeyLatch = true;
            WorldState.SpawnPulseDirty = 1;
            sc.shortA = 0;
            for (sc.shortB = 0; sc.shortB < SystRecord.HyperLinkCount; sc.shortB = (short)(sc.shortB + 1))
            {
                sc.eligibleHyperTargetFlags[sc.shortB] = 0;
                if (SystTable.Store[ship.CurrentSystem].HyperLink[sc.shortB] != -1 &&
                   (SystTable.Store[SystTable.Store[ship.CurrentSystem].HyperLink[sc.shortB]].ShownFlag != 0))
                {
                    sc.eligibleHyperTargetFlags[sc.shortB] = 1;
                    sc.shortA = (short)(sc.shortA + 1);
                }
            }
            if (sc.shortA < 1)
            {
                ship.NavTargetSpob = -1;
            }
            else
            {
                do
                {
                    ship.NavTargetSpob = (short)(ship.NavTargetSpob + 1);
                    if (ship.NavTargetSpob > SystRecord.HyperLinkCount - 1)
                    {
                        ship.NavTargetSpob = 0;
                    }
                    if (ship.NavTargetSpob < 0)
                    {
                        ship.NavTargetSpob = SystRecord.HyperLinkCount - 1;
                    }
                } while (sc.eligibleHyperTargetFlags[ship.NavTargetSpob] == 0);
            }
        }
        if (ship.JumpWindupTimer < 1)
        {
            sc.shortA = -1;
            sc.shortB = (short)(Keymap.TestCachedKeymapBit(0x1a));
            if (sc.shortB != 0)
            {
                sc.shortA = 0;
            }
            sc.shortB = (short)(Keymap.TestCachedKeymapBit(0x1b));
            if (sc.shortB != 0)
            {
                sc.shortA = 1;
            }
            sc.shortB = (short)(Keymap.TestCachedKeymapBit(0x1c));
            if (sc.shortB != 0)
            {
                sc.shortA = 2;
            }
            sc.shortB = (short)(Keymap.TestCachedKeymapBit(0x1d));
            if (sc.shortB != 0)
            {
                sc.shortA = 3;
            }
            for (sc.shortB = 0; sc.shortB < 4; sc.shortB = (short)(sc.shortB + 1))
            {
                sc.shortC = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot((KeyAction)(sc.shortB + 29))));  // slots 29..32
                if (sc.shortC != 0)
                {
                    sc.shortA = sc.shortB;
                }
            }
            sc.shortB = (short)(Keymap.TestCachedKeymapBit(0x68));
            if (sc.shortB != 0 ||
               (((short)(Keymap.TestCachedKeymapBit(0x3f))) != 0 && ((short)(Keymap.TestCachedKeymapBit(0x1f))) != 0))
            {
                sc.shortA = 5;
            }
            if (sc.shortA == -1)
            {
                PlayerKeyLatches.PlanetSelectKeyLatch = false;
            }
            else if (!PlayerKeyLatches.PlanetSelectKeyLatch)
            {
                PlayerKeyLatches.PlanetSelectKeyLatch = true;
                if (sc.shortA == 5)
                {
                    sc.shortA = (short)(FindNearestLandableStellar.Run(ShipTable.Player));
                }
                else
                {
                    sc.shortA = SystTable.SpobLink(ship.CurrentSystem, sc.shortA);
                }
                if (sc.shortA != -1 &&
                   (ship.CurrentSystem == GameData.Spobs[sc.shortA].System))
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
                    ship.NavTargetSpob = sc.shortA;
                    WorldState.SpawnPulseDirty = 1;
                    ship.NavMode = 2;
                    WorldState.LandingTargetSpob = -1;
                }
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action10)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.WeaponCycleKeyLatch = false;
        }
        else if (!PlayerKeyLatches.WeaponCycleKeyLatch)
        {
            PlayerKeyLatches.WeaponCycleKeyLatch = true;
            WorldState.WeaponSlotDirty = 1;
            if (ship.HasTargetLock == 1)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x30));
                if (sc.shortA == 0)
                {
                    sc.shortA = (short)(FindNextShipSlot.Run((int)ship.TargetSlot, ship.CurrentSystem));
                }
                else
                {
                    sc.shortA = (short)(FindPrevShipSlot.Run((int)ship.TargetSlot, ship.CurrentSystem));
                }
                if (sc.shortA == ship.TargetSlot || sc.shortA == ship.SlotIndex)
                {
                    ship.TargetSlot = -1;
                }
                else
                {
                    ship.TargetSlot = sc.shortA;
                }
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.MapKeyLatch = false;
        }
        else if (((ship.JumpWindupTimer < 1 && WorldState.UiSuppressGateA == 0) &&
                 (WorldState.UiSuppressGateB == 0)) && WorldState.GameFrameTickCounter > -1)
        {
            RepaintGameWindow.Run();
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
            MacToolbox.ShowCursor();
            RunGalaxyMapDialog.Run();
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            MacToolbox.HideCursor();
            SetGamePortAndDevice.Run();
            RefreshStatusPanel.Run();
            DispatchPendingChatter.Run(0);
        }
        else if (!PlayerKeyLatches.MapKeyLatch)
        {
            PlayerKeyLatches.MapKeyLatch = true;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Land)));
        if ((sc.shortA == 0 || ship.DeathTimer > 0.0f) ||
           ((0 < WorldState.WorldCountdown || ship.JumpWindupTimer > 0)))
        {
            PlayerKeyLatches.LandKeyLatch = false;
        }
        else
        {
            if ((ship.NavMode != 2 || ship.NavTargetSpob == -1) &&
               (sc.shortA = (short)(FindNearestLandableStellar.Run(ship))) != -1)
            {
                WorldState.SpawnPulseDirty = 1;
                ship.NavMode = 2;
                ship.NavTargetSpob = sc.shortA;
            }
            if (((!PlayerKeyLatches.LandKeyLatch) && ship.NavMode == 2) &&
               (ship.NavTargetSpob != -1))
            {
                PlayerKeyLatches.LandKeyLatch = true;
                if ((((SpobFlags)GameData.Spobs[ship.NavTargetSpob].Flags & SpobFlags.Landable) == 0 ||
                    (ship.CurrentSystem !=
                     GameData.Spobs[ship.NavTargetSpob].System)) ||
                   (GameData.Spobs[ship.NavTargetSpob].Visible == 0))
                {
                    string unableMsg = "Your ship is unable to ";
                    if (((SpobFlags)GameData.Spobs[ship.NavTargetSpob].Flags & SpobFlags.Station) == 0)
                    {
                        unableMsg += "land on ";
                    }
                    else
                    {
                        unableMsg += "dock at ";
                    }
                    unableMsg += Trunc(GameData.Spobs[ship.NavTargetSpob].Name, 31);
                    unableMsg += ". The ";
                    if (((SpobFlags)GameData.Spobs[ship.NavTargetSpob].Flags & SpobFlags.Station) == 0)
                    {
                        unableMsg += "planet’s environment is too hostile.";
                    }
                    else
                    {
                        unableMsg += "station’s hull integrity is too unstable.";
                    }
                    EnqueueChatterEvent.Run(unableMsg, 250, 0, 12, UiColors.ChatterText, 0, 0);
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                }
                else if (!WorldState.IsCloaked)
                {
                    sc.flag = (byte)(ShipDerivedStats.IsDyingOrDestroyed(ship) ? 1 : 0);
                    if (sc.flag == 0)
                    {
                        sc.spobStationFlag = (byte)((SpobFlags)GameData.Spobs[ship.NavTargetSpob].Flags & SpobFlags.Station);
                        sc.spobInhabitedFlag = (byte)(((SpobFlags)GameData.Spobs[ship.NavTargetSpob].Flags & SpobFlags.Uninhabited) ==
                                    0 ? 1 : 0);
                        if (sc.spobInhabitedFlag == 0)
                        {
                            WorldState.LandingApproachState = 750;
                            ship.AiActionTimer = 0;
                            WorldState.LandingTargetSpob = ship.NavTargetSpob;
                            EnqueueChatterEvent.Run("No response.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                        }
                        if (WorldState.LandingTargetSpob == ship.NavTargetSpob)
                        {
                            sc.dScratch24 = (double)EvMath.FloatAbs((double)ship.VelX);
                            sc.local_3cc = (int)(float)sc.dScratch24;
                            sc.dScratch24 = (double)EvMath.FloatAbs((double)ship.VelY);
                            sc.local_3c8 = (int)(float)sc.dScratch24;
                            if (((WorldState.LandingApproachState < 750 || ship.AiActionTimer > 0)
                                || (0.75 < (double)(float)sc.local_3cc)) ||
                               (0.75 < (double)(float)sc.local_3c8))
                            {
                                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                                sc.loopIndex = (int)(ship.PosX - (float)(int)GameData.Spobs[ship.NavTargetSpob].XPos);
                                sc.ushortScratch17 = (ushort)sc.loopIndex;
                                sc.ushortB = (ushort)((short)sc.ushortScratch17 >> 0xf);
                                sc.loopIndex = (int)(ship.PosY - (float)(int)GameData.Spobs[ship.NavTargetSpob].YPos);
                                sc.ushortScratch12 = (ushort)sc.loopIndex;
                                sc.ushortA = (ushort)((short)sc.ushortScratch12 >> 0xf);
                                sc.local_3de = (short)((sc.ushortA ^ sc.ushortScratch12) - sc.ushortA);
                                if (PlanetSpriteRecordTable.Store[GameData.Spobs[ship.NavTargetSpob].SpriteId] == 0)
                                {
                                    sc.dockingProximityThreshold = 75;
                                }
                                else
                                {
                                    sc.shortA = (short)(MacRectHeight.Run(PlanetSpriteRecordTable.Store[GameData.Spobs[ship.NavTargetSpob].SpriteId]));
                                    sc.loopIndex = (int)(1.75 * (double)(int)sc.shortA);
                                    sc.dockingProximityThreshold = (short)sc.loopIndex;
                                }
                                if (((short)((sc.ushortB ^ sc.ushortScratch17) - sc.ushortB) < sc.dockingProximityThreshold) && sc.local_3de < sc.dockingProximityThreshold)
                                {
                                    if ((0.75 < (double)(float)sc.local_3cc) ||
                                       (0.75 < (double)(float)sc.local_3c8))
                                    {
                                        if (sc.spobStationFlag == 0)
                                        {
                                            EnqueueChatterEvent.Run("You’re moving too quickly to land on this planet.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                        }
                                        else
                                        {
                                            EnqueueChatterEvent.Run("You’re moving too quickly to dock at this station.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                        }
                                    }
                                }
                                else if (sc.spobStationFlag == 0)
                                {
                                    EnqueueChatterEvent.Run("You’re too far away to land on this planet.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                }
                                else
                                {
                                    EnqueueChatterEvent.Run("You’re too far away to dock at this station.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                }
                            }
                            else
                            {
                                sc.loopIndex = (int)(ship.PosX - (float)(int)GameData.Spobs[ship.NavTargetSpob].XPos);
                                sc.ushortScratch17 = (ushort)sc.loopIndex;
                                sc.ushortB = (ushort)((short)sc.ushortScratch17 >> 0xf);
                                sc.loopIndex = (int)(ship.PosY - (float)(int)GameData.Spobs[ship.NavTargetSpob].YPos);
                                sc.ushortScratch12 = (ushort)sc.loopIndex;
                                sc.ushortA = (ushort)((short)sc.ushortScratch12 >> 0xf);
                                sc.local_3de = (short)((sc.ushortA ^ sc.ushortScratch12) - sc.ushortA);
                                if (PlanetSpriteRecordTable.Store[GameData.Spobs[ship.NavTargetSpob].SpriteId] == 0)
                                {
                                    sc.dockingProximityThreshold = 75;
                                }
                                else
                                {
                                    sc.shortA = (short)(MacRectHeight.Run(PlanetSpriteRecordTable.Store[GameData.Spobs[ship.NavTargetSpob].SpriteId]));
                                    sc.loopIndex = (int)(1.75 * (double)(int)sc.shortA);
                                    sc.dockingProximityThreshold = (short)sc.loopIndex;
                                }
                                if (((short)((sc.ushortB ^ sc.ushortScratch17) - sc.ushortB) < sc.dockingProximityThreshold) && sc.local_3de < sc.dockingProximityThreshold)
                                {
                                    if (sc.spobInhabitedFlag == 0)
                                    {
                                        SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                                    }
                                    else
                                    {
                                        TickPassiveOutfitTopup.Run();
                                    }
                                    WorldState.FlashChatterCountdown = 0;
                                    SpriteNodes.At(EscortSpawnRecord.Handle).SpritePtr = 0;
                                    WorldState.HudBlinkCountdown = 0;
                                    TickFlashEffectCountdown.Run();
                                    RepaintGameWindow.Run();
                                    TickHudRedrawScheduler.Run();
                                    StopAmbientSoundChannel.Run();
                                    MacToolbox.ShowCursor();
                                    WorldState.IsCursorHiddenByGame = false;
                                    SetGamePortAndDevice.Run();
                                    WorldState.HudStatusPanelDirty = 1;
                                    TickHudRedrawScheduler.Run();
                                    SetGamePortAndDevice.Run();
                                    RunSpaceportDialog.Run(ship.NavTargetSpob);
                                    BankRobberyNewsEvent.Run();
                                    SetGamePortAndDevice.Run();
                                    RefreshStatusPanel.Run();
                                    ship.VelY = 0f;
                                    ship.VelX = 0f;
                                    if (ship.NavTargetSpob == -1)
                                    {
                                        ship.PosY = 0f;
                                        ship.PosX = 0f;
                                    }
                                    else
                                    {
                                        ship.PosX = (float)(int)GameData.Spobs[ship.NavTargetSpob].XPos;
                                        ship.PosY = (float)(int)GameData.Spobs[ship.NavTargetSpob].YPos;
                                    }
                                    sc.floatScratch = (float)ShipDerivedStats.EffectiveShieldMax(ship);
                                    ship.Shield = sc.floatScratch;
                                    ship.TargetSlot = -1;
                                    WorldState.RadarRedrawDirty = 1;
                                    WorldState.WeaponSlotDirty = 1;
                                    WorldState.HudWeaponPanelDirty = 1;
                                    WorldState.SpawnPulseDirty = 1;
                                    WorldState.PlayerShieldBarDirty = 1;
                                    WorldState.HudStatusPanelDirty = 1;
                                    TickWorldDailyEvents.Run();
                                    TickStarJitter.Run();
                                    RefuelAndRepairEscorts.Run(ship);
                                    if (SystTable.Store[ship.CurrentSystem].Visited < 2)
                                    {
                                        SystTable.Store[ship.CurrentSystem].Visited = 2;
                                    }
                                    MarkGalaxyMapClustersForSyst.Run(ship.CurrentSystem);
                                    for (sc.shortA = 0; sc.shortA < MissionStateTable.Count; sc.shortA = (short)(sc.shortA + 1))
                                    {
                                        if (GameData.MissionStates[sc.shortA].IsActive != 0)
                                        {
                                            if ((GameData.Missions[sc.shortA].Flags & MisnFlags.AuxShipsReplacedWhenDestroyed) != 0)
                                            {
                                                GameData.Missions[sc.shortA].RemainingSpawnCount = GameData.Missions[sc.shortA].AuxShipCount;
                                            }
                                            sc.shortB = (short)(SeedEvoRng.Run(70));
                                            GameData.Missions[sc.shortA].SpawnCountdown = (short)(sc.shortB + 70);
                                            GameData.Missions[sc.shortA].LiveSpawnCount = 0;
                                        }
                                    }
                                    MacToolbox.HideCursor();
                                    WorldState.IsCursorHiddenByGame = true;
                                    if (WorldState.TutorialHintPhase < 3)
                                    {
                                        WorldState.TutorialHintPhase = -1;
                                    }
                                    else
                                    {
                                        sc.shortA = (short)(SeedEvoRng.Run(5));
                                        string launchMsg = "";
                                        if (sc.shortA == 0)
                                        {
                                            launchMsg = "Launching from ";
                                        }
                                        if (sc.shortA == 1)
                                        {
                                            launchMsg = "Blasting off from ";
                                        }
                                        if (sc.shortA == 2)
                                        {
                                            launchMsg = "Taking off from ";
                                        }
                                        if (sc.shortA == 3)
                                        {
                                            launchMsg = "Leaving ";
                                        }
                                        if (sc.shortA == 4)
                                        {
                                            launchMsg = "Departing ";
                                        }
                                        launchMsg += Trunc(GameData.Spobs[WorldState.LandingTargetSpob].Name, 63);
                                        launchMsg += " on ";
                                        launchMsg += FormatDateLongFull.Format(GameDate.Current.Year, GameDate.Current.Month,
                                                                                    GameDate.Current.Day);
                                        launchMsg += ".";
                                        EnqueueChatterEvent.Run(launchMsg, 240, 0, 12, UiColors.ChatterText, 0, 0);
                                    }
                                    CleanupSystNpcs.Run(0);
                                    PilotSave.Run((int)ship.NavTargetSpob);
                                    WorldState.LandingTargetSpob = -1;
                                    WorldState.LandingApproachState = 0;
                                    sc.ushortScratch13 = (short)(SeedEvoRng.Run(360));
                                    ship.Heading = sc.ushortScratch13;
                                    ship.TargetSlot = -1;
                                    sc.shortA = (short)(SeedEvoRng.Run(30));
                                    WorldState.RespawnCounter = (short)(sc.shortA + 30);
                                    RunFleetSpawner.Run((int)ship.CurrentSystem);
                                    RecomputeWorldVisibility.Run();
                                    ship.NavTargetSpob = -1;
                                    EngageAutopilotToHistoryTarget.Run();
                                    TickPersNagHook.Run();
                                    WorldState.ClearShotsFlag = 1;
                                    WorldState.ClearCarriedSpritesFlag = 1;
                                    WorldState.ClearExplosionsFlag = 1;
                                    WorldState.ClearStreaksFlag = 1;
                                    WorldState.NoAsteroidsFlag = 1;
                                    WorldState.GameFrameTickCounter = -15;
                                }
                                else
                                {
                                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                                    if (sc.spobStationFlag == 0)
                                    {
                                        EnqueueChatterEvent.Run("You’re too far away to land on this planet.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                    }
                                    else
                                    {
                                        EnqueueChatterEvent.Run("You’re too far away to dock at this station.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                                    }
                                }
                            }
                        }
                        else
                        {
                            WorldState.LandingTargetSpob = ship.NavTargetSpob;
                            sc.loopIndex = (int)(ship.PosX - (float)(int)GameData.Spobs[WorldState.LandingTargetSpob].XPos);
                            sc.ushortScratch17 = (ushort)sc.loopIndex;
                            sc.ushortB = (ushort)((short)sc.ushortScratch17 >> 0xf);
                            sc.loopIndex = (int)(ship.PosY - (float)(int)GameData.Spobs[WorldState.LandingTargetSpob].YPos);
                            sc.ushortScratch12 = (ushort)sc.loopIndex;
                            sc.ushortA = (ushort)((short)sc.ushortScratch12 >> 0xf);
                            sc.local_3de = (short)((sc.ushortA ^ sc.ushortScratch12) - sc.ushortA);
                            sc.found = GameData.Spobs[WorldState.LandingTargetSpob].MinCoolness <= GalaxyMapGlobals.SystemStatus(ship.CurrentSystem);
                            sc.skipFlag = GameData.Spobs[WorldState.LandingTargetSpob].TradingEnabled != 0;
                            sc.found2 = 749 < WorldState.LandingApproachState || (sc.skipFlag || sc.found);
                            if (749 >= WorldState.LandingApproachState && (!sc.skipFlag && !sc.found))
                            {
                                for (sc.shortA = 0; sc.shortA < MissionStateTable.Count; sc.shortA = (short)(sc.shortA + 1))
                                {
                                    if (GameData.MissionStates[sc.shortA].IsActive != 0)
                                    {
                                        if (GameData.Ships[0].NavTargetSpob == GameData.Missions[sc.shortA].TargetSpob)
                                        {
                                            sc.found2 = true;
                                        }
                                        if (GameData.Ships[0].NavTargetSpob == GameData.Missions[sc.shortA].ReturnSpob)
                                        {
                                            sc.found2 = true;
                                        }
                                    }
                                }
                            }
                            if (sc.found2)
                            {
                                if (((short)((sc.ushortB ^ sc.ushortScratch17) - sc.ushortB) < 250) && sc.local_3de < 250)
                                {
                                    WorldState.LandingApproachState = 749;
                                }
                                else
                                {
                                    WorldState.LandingApproachState = 0;
                                    SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                                    string requestMsg;
                                    if (((SpobFlags)GameData.Spobs[WorldState.LandingTargetSpob].Flags & SpobFlags.Station) == 0)
                                    {
                                        sc.shortA = (short)(SeedEvoRng.Run(3));
                                        if (sc.shortA == 0)
                                        {
                                            requestMsg = Trunc(GameData.Spobs[GameData.Ships[0].NavTargetSpob].Name, 31)
                                                       + " traffic control reads you";
                                        }
                                        else
                                        {
                                            requestMsg = "Landing request received";
                                        }
                                    }
                                    else
                                    {
                                        sc.shortA = (short)(SeedEvoRng.Run(3));
                                        if (sc.shortA == 0)
                                        {
                                            requestMsg = Trunc(GameData.Spobs[GameData.Ships[0].NavTargetSpob].Name, 31)
                                                       + " dockmaster reads you";
                                        }
                                        else
                                        {
                                            requestMsg = "Docking request received";
                                        }
                                    }
                                    sc.shortA = (short)(SeedEvoRng.Run(2));
                                    if (sc.shortA == 0)
                                    {
                                        requestMsg += ", " + Trunc(PilotIdentity.ShipName, 63);
                                    }
                                    requestMsg += ". Begin initial approach.";
                                    EnqueueChatterEvent.Run(requestMsg, 250, 0, 12, UiColors.ChatterText, 0, 0);
                                }
                            }
                            else
                            {
                                SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                                if (((SpobFlags)GameData.Spobs[WorldState.LandingTargetSpob].Flags & SpobFlags.Station) == 0)
                                {
                                    EnqueueChatterEvent.Run("Landing request denied.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                                }
                                else
                                {
                                    EnqueueChatterEvent.Run("Docking request denied.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                                }
                                WorldState.LandingTargetSpob = -1;
                            }
                        }
                    }
                }
                else
                {
                    EnqueueChatterEvent.Run("Disengage cloaking device first.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                }
            }
        }
        if (WorldState.LandingTargetSpob != -1)
        {
            ContinueLandingApproach(ship, sc);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action16)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.BoardKeyLatch = false;
        }
        else if (((!PlayerKeyLatches.BoardKeyLatch) && ship.TargetSlot != -1) && (!WorldState.IsCloaked))
        {
            HandleBoardDisabledTarget(ship, sc);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action11)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.TargetNearestKeyLatch = false;
        }
        else if (!PlayerKeyLatches.TargetNearestKeyLatch)
        {
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
            if (sc.shortA == 0)
            {
                sc.shortA = (short)(FindNearestEngageable.Run());
            }
            else
            {
                sc.shortA = (short)(FindNearestActiveShip.Run());
            }
            PlayerKeyLatches.TargetNearestKeyLatch = true;
            if ((sc.shortA == -1 || ship.HasTargetLock != 1) ||
               (sc.shortA == ship.TargetSlot))
            {
                if (sc.shortA == -1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                }
            }
            else
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                WorldState.WeaponSlotDirty = 1;
                ship.HasTargetLock = 1;
                ship.TargetSlot = sc.shortA;
            }
        }
        if ((ship.JumpWindupTimer < 1 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.FirePrimary)))) != 0)
           && ((ship.AiActionTimer < 1 && (!WorldState.IsCloaked))))
        {
            for (sc.loopIndex = 0; (sc.shortA = (short)sc.loopIndex) < ShipRecord.WeaponSlotCount; sc.loopIndex += 1)
            {
                // Weapons flagged secondary-trigger-only don't fire on the primary-fire key.
                if (ship.WeaponSlotType[sc.shortA] > 0 &&
                   (((WeaponFlags)GameData.Weapons[sc.shortA].Flags & WeaponFlags.FiresOnSecondaryTrigger) == 0))
                {
                    WeaponSlotTick.Run(sc.loopIndex);
                }
            }
        }
        if (((ship.JumpWindupTimer < 1 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action3)))) != 0)
            && ship.SelectedWeaponSlot != -1) &&
           ((ship.AiActionTimer < 1 && (!WorldState.IsCloaked))))
        {
            WeaponSlotTick.Run((int)ship.SelectedWeaponSlot);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action0)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.SecondaryTriggerKeyLatch = false;
        }
        else if (!PlayerKeyLatches.SecondaryTriggerKeyLatch)
        {
            PlayerKeyLatches.SecondaryTriggerKeyLatch = true;
            WorldState.HudWeaponPanelDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
            sc.shortA = 0;
            for (sc.shortB = 0; sc.shortB < ShipRecord.WeaponSlotCount; sc.shortB = (short)(sc.shortB + 1))
            {
                if (ship.WeaponSlotType[sc.shortB] > 0 &&
                   (((WeaponFlags)GameData.Weapons[sc.shortB].Flags & WeaponFlags.FiresOnSecondaryTrigger) != 0))
                {
                    sc.shortA = (short)(sc.shortA + 1);
                }
            }
            if (sc.shortA > 0)
            {
                sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
                if (sc.shortA == 0)
                {
                    sc.local_3de = 1;
                }
                else
                {
                    sc.local_3de = -1;
                }
                sc.found = false;
                while (!sc.found)
                {
                    ship.SelectedWeaponSlot = (short)(ship.SelectedWeaponSlot + sc.local_3de);
                    while (ship.SelectedWeaponSlot > ShipRecord.WeaponSlotCount - 1)
                    {
                        ship.SelectedWeaponSlot = (short)(ship.SelectedWeaponSlot - ShipRecord.WeaponSlotCount);
                    }
                    while (ship.SelectedWeaponSlot < 0)
                    {
                        ship.SelectedWeaponSlot = (short)(ship.SelectedWeaponSlot + ShipRecord.WeaponSlotCount);
                    }
                    if (ship.SelectedWeaponSlot < 0 || ship.SelectedWeaponSlot > ShipRecord.WeaponSlotCount - 1)
                    {
                        sc.found = false;
                    }
                    else if (ship.WeaponSlotType[ship.SelectedWeaponSlot] > 0 &&
                            (((WeaponFlags)GameData.Weapons[ship.SelectedWeaponSlot].Flags & WeaponFlags.FiresOnSecondaryTrigger)
                             != 0))
                    {
                        sc.found = true;
                    }
                }
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action1)));
        if (sc.shortA != 0 && ship.SelectedWeaponSlot != -1)
        {
            ship.SelectedWeaponSlot = -1;
            WorldState.HudWeaponPanelDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
        }
        for (sc.shortA = 0; sc.shortA < ShipRecord.WeaponSlotCount; sc.shortA = (short)(sc.shortA + 1))
        {
            if (ship.WeaponSlotReload[sc.shortA] <= 0.0f)
            {
                ship.WeaponSlotReload[sc.shortA] = 0.0f;
            }
            else
            {
                ship.WeaponSlotReload[sc.shortA] -= 1.0f;
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action7)));
        if (sc.shortA != 0 && ship.JumpWindupTimer < 1)
        {
            if (ship.TargetSlot == -1)
            {
                if (ship.NavTargetSpob != -1)
                {
                    float spobX = (float)GameData.Spobs[ship.NavTargetSpob].XPos;
                    float spobY = (float)GameData.Spobs[ship.NavTargetSpob].YPos;
                    ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, spobX, spobY);
                }
            }
            else if (ship.NavTargetSpob == -1)
            {
                var target = ShipTable.Ships[ship.TargetSlot];
                ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            }
            else
            {
                sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
                if (sc.shortA == 0)
                {
                    var target = ShipTable.Ships[ship.TargetSlot];
                    ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
                }
                else
                {
                    float spobX = (float)GameData.Spobs[ship.NavTargetSpob].XPos;
                    float spobY = (float)GameData.Spobs[ship.NavTargetSpob].YPos;
                    ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, spobX, spobY);
                }
            }
        }
        if (ship.NavMode == 3)
        {
            WorldState.LandingTargetSpob = -1;
        }
        sc.uintScratch = (uint)(short)(int)ship.VelX;
        sc.uintScratch6 = (uint)((int)sc.uintScratch >> 0x1f);
        bool velXSettled = (int)((sc.uintScratch6 ^ sc.uintScratch) - sc.uintScratch6) < 2;  // |trunc(velX)| < 2
        sc.uintScratch = (uint)(short)(int)ship.VelY;
        sc.uintScratch6 = (uint)((int)sc.uintScratch >> 0x1f);
        bool velYSettled = (int)((sc.uintScratch6 ^ sc.uintScratch) - sc.uintScratch6) < 2;  // |trunc(velY)| < 2
        if (ship.NavMode == 3 && ship.NavTargetSpob != -1 && ship.JumpWindupTimer > 0 && velXSettled && velYSettled)
        {
            sc.local_3bc = (int)(float)(int)SystTable.Store[ship.CurrentSystem].XPos;
            sc.local_3b8 = (int)(float)(int)SystTable.Store[ship.CurrentSystem].YPos;
            sc.shortA = SystTable.Store[ship.CurrentSystem].HyperLink[ship.NavTargetSpob];
            sc.local_3c4 = (float)(int)SystTable.Store[sc.shortA].XPos;
            sc.local_3c0 = (float)(int)SystTable.Store[sc.shortA].YPos;
            sc.ushortScratch13 = (short)(EvMath.HeadingBetween(sc.local_3bc, sc.local_3b8, sc.local_3c4, sc.local_3c0));
            ship.HeadingPrev = (short)sc.ushortScratch13;
        }
        sc.windupSettledFlag = 0;
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action14)));
        if (sc.shortA == 0 && ship.JumpWindupTimer < 1)
        {
            PlayerKeyLatches.JumpKeyLatch = false;
        }
        else
        {
            if (ship.NavMode != 3 ||
               (((ship.NavTargetSpob == -1 || ship.JumpWindupTimer == -999) ||
                (ship.Fuel < 100.0f))))
            {
                if (!PlayerKeyLatches.JumpKeyLatch)
                {
                    if (WorldState.TutorialHintPhase < 3)
                    {
                        if (ship.NavMode != 3 || ship.NavTargetSpob == -1)
                        {
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                            EnqueueChatterEvent.Run("You have to select a destination before you can start a hyperspace jump.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                        }
                    }
                    else if ((ship.NavMode == 3 && ship.NavTargetSpob != -1) &&
                            ((ship.JumpWindupTimer != -999 && ship.Fuel < 100.0f)))
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                        EnqueueChatterEvent.Run("Insufficent fuel for hyperspace jump.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                }
            }
            else
            {
                sc.clearOfStellarsFlag = 1;
                for (sc.shortA = 0; sc.shortA < SystRecord.StellarLinkCount; sc.shortA = (short)(sc.shortA + 1))
                {
                    if (SystTable.SpobLink(ship.CurrentSystem, sc.shortA) != -1)
                    {
                        sc.uintScratch = (uint)(ShipDerivedStats.EffectiveHyperRangeSquared(ship));
                        sc.dScratch24 = (double)(int)sc.uintScratch;
                        sc.dScratch25 = (double)EvMath.FloatAbs(EvMath.DistanceSquared(0.0f, 0.0f, ship.PosX, ship.PosY));
                        if (sc.dScratch25 <= (double)(float)sc.dScratch24)
                        {
                            sc.clearOfStellarsFlag = 0;
                        }
                    }
                }
                if (sc.clearOfStellarsFlag == 0 && ship.JumpWindupTimer < 1)
                {
                    if (!PlayerKeyLatches.JumpKeyLatch)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                        EnqueueChatterEvent.Run("Can’t initiate hyperspace jump - not yet far enough away from system center.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                    else
                    {
                        ship.JumpWindupTimer = 0;
                    }
                }
                else
                {
                    sc.uintScratch = (uint)(short)(int)ship.VelX;
                    sc.uintScratch6 = (uint)((int)sc.uintScratch >> 0x1f);
                    velXSettled = (int)((sc.uintScratch6 ^ sc.uintScratch) - sc.uintScratch6) < 2;  // |trunc(velX)| < 2
                    sc.uintScratch6 = (uint)(short)(int)ship.VelY;
                    sc.uintScratch = (uint)((int)sc.uintScratch6 >> 0x1f);
                    velYSettled = (int)((sc.uintScratch ^ sc.uintScratch6) - sc.uintScratch) < 2;  // |trunc(velY)| < 2
                    if (velXSettled && velYSettled)
                    {
                        sc.windupSettledFlag = 1;
                        if (ship.JumpWindupTimer < 2)
                        {
                            ship.JumpWindupTimer = 2;
                            ship.AiTickStamp = (int)MacToolbox.TickCount();
                            PlayerKeyLatches.JumpSettleFlag = false;
                            if (WorldState.IsCloaked)
                            {
                                DisengageCloaking.Run();
                            }
                            WorldState.SpawnPulseDirty = 1;
                            WorldState.TutorialHintPhase = (short)0x7fff;
                            sc.shortA = (short)(CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd));
                            if (sc.shortA == 0)
                            {
                                ComputeScrollRatio.Run((double)GameData.ShipClasses[ship.ShipClass].SpriteScale);
                                EnqueueSoundVoice.Run(SoundMixer.BoardingChimeRequest);
                                WorldState.AutopilotFlag = 0;
                            }
                        }
                    }
                    else
                    {
                        ship.AiTickStamp = (int)MacToolbox.TickCount();
                        PlayerKeyLatches.JumpSettleFlag = false;
                        if (WorldState.IsCloaked)
                        {
                            DisengageCloaking.Run();
                        }
                        ship.JumpWindupTimer = 1;
                        sc.local_3bc = 0;
                        sc.local_3c4 = (float)(100.0 * (double)ship.VelX);
                        sc.local_3c0 = (float)(100.0 * (double)ship.VelY);
                        sc.ushortScratch13 = (short)(EvMath.HeadingBetween(sc.local_3c4, sc.local_3c0, sc.local_3bc, sc.local_3bc));
                        ship.HeadingPrev = (short)sc.ushortScratch13;
                        sc.shortA = (short)(ShipDerivedStats.EffectiveManeuver(ship));
                        sc.uintScratch = (uint)((int)ship.HeadingPrev - (int)ship.Heading >> 0x1f);
                        if ((int)((sc.uintScratch ^ (int)ship.HeadingPrev - (int)ship.Heading) - sc.uintScratch)
                            < sc.shortA + 1)
                        {
                            // EffectiveAccel is the magnitude arg here, not a 3-arg position-only absorber.
                            {
                                float vx = ship.VelX, vy = ship.VelY;
                                EvMath.OffsetByHeading(ShipDerivedStats.EffectiveAccel(ship),
                                  (int)ship.Heading, ref vx, ref vy);
                                ship.VelX = vx; ship.VelY = vy;
                            }
                        }
                    }
                }
            }
            PlayerKeyLatches.JumpKeyLatch = true;
        }
        if (sc.windupSettledFlag == 0 || (PlayerKeyLatches.JumpSettleFlag))
        {
            UpdateWindupState(ship);
        }
        else
        {
            PropagateFleeToEscorts.Run(ship);
            ship.JumpWindupTimer = (short)(ship.JumpWindupTimer + 1);
            ship.VelX *= 0.98f;
            ship.VelY *= 0.98f;
            if (ship.JumpWindupTimer > 30 && ((short)(CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd))) == 0)
            {
                WorldState.AutopilotFlag = 1;
            }
            if (ship.JumpWindupTimer < 30 || WorldState.AutopilotFlag == 0)
            {
                if (ship.JumpWindupTimer > 30)
                {
                    sc.loopIndex = (int)(MacToolbox.TickCount());
                    if (466.0f /
                        GameData.ShipClasses[ship.ShipClass].SpriteScale <
                        (float)(double)(uint)(sc.loopIndex - ship.AiTickStamp))
                    {
                        sc.loopIndex = (int)(MacToolbox.TickCount());
                        if ((500.0f /
                             GameData.ShipClasses[ship.ShipClass].SpriteScale <
                             (float)(double)(uint)(sc.loopIndex - ship.AiTickStamp)) &&
                           (WorldState.AutopilotFlag == 0))
                        {
                            WorldState.AutopilotFlag = 1;
                            SndPlay.Run(SoundResourceCells.UiChimeSnd, 50, 128, 128);
                        }
                        sc.shortA = (short)(CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd));
                        if (sc.shortA == 0)
                        {
                            WorldState.AutopilotFlag = 1;
                        }
                    }
                }
            }
            else
            {
                ship.PosY = 0f;
                ship.PosX = 0f;
                sc.local_3bc = (int)(float)(int)SystTable.Store[
                                            SystTable.Store[ship.CurrentSystem].HyperLink[ship.NavTargetSpob]].XPos;
                sc.local_3b8 = (int)(float)(int)SystTable.Store[
                                            SystTable.Store[ship.CurrentSystem].HyperLink[ship.NavTargetSpob]].YPos;
                sc.local_3c4 = (float)(int)SystTable.Store[ship.CurrentSystem].XPos;
                sc.local_3c0 = (float)(int)SystTable.Store[ship.CurrentSystem].YPos;
                sc.shortA = (short)(EvMath.HeadingBetween(sc.local_3c4, sc.local_3c0, sc.local_3bc, sc.local_3b8));
                {
                    float px = ship.PosX, py = ship.PosY;
                    EvMath.OffsetByHeading((double)1350.0f, (sc.shortA + 180) % 360, ref px, ref py);
                    ship.PosX = px; ship.PosY = py;
                }
                ship.VelY = 0f;
                ship.VelX = 0f;
                // EffectiveSpeed is the magnitude arg here, not a 3-arg position-only absorber —
                // dropping it silently zeroes the post-arrival velocity nudge.
                {
                    float vx = ship.VelX, vy = ship.VelY;
                    EvMath.OffsetByHeading(ShipDerivedStats.EffectiveSpeed(ship),
                      (int)ship.Heading, ref vx, ref vy);
                    ship.VelX = vx; ship.VelY = vy;
                }
                sc.destSystemIndex = SystTable.Store[ship.CurrentSystem].HyperLink[ship.NavTargetSpob];
                ship.PriorSystem = ship.CurrentSystem;
                ship.CurrentSystem = sc.destSystemIndex;
                MarkGalaxyMapClustersForSyst.Run(ship.CurrentSystem);
                ship.NavMode = -1;
                ship.NavTargetSpob = -1;
                ship.TargetSlot = -1;
                ship.Fuel -= 100.0f;
                // Hyper-jump screen flash: a rect inset 144px from the right edge of the game
                // window, inverted twice (decompile: Rect {top, left, bottom, top + (right-left) - 144}).
                short flashRight = (short)(GlobalState.PortTop +
                    (GlobalState.PortRight - GlobalState.PortLeft) - 144);
                MacToolbox.InvertRect(GlobalState.PortTop, GlobalState.PortLeft,
                                      GlobalState.PortBottom, flashRight);
                MacToolbox.InvertRect(GlobalState.PortTop, GlobalState.PortLeft,
                                      GlobalState.PortBottom, flashRight);
                ReseedBackgroundNebulae.Run();
                WorldState.SpawnPulseDirty = 1;
                WorldState.WeaponSlotDirty = 1;
                WorldState.HudStatusPanelDirty = 1;
                WorldState.HudWeaponPanelDirty = 1;
                WorldState.ShieldEnergyBarDirty = 1;
                WorldState.LandingTargetSpob = -1;
                WorldState.LandingApproachState = -1;
                SpaceportGlobals.BbsLastSpob = -1;
                DialogScratch.SpaceportBribeRoll = -1; // invalidate bribe roll on jump
                sc.ushortScratch13 = (short)(SeedEvoRng.Run(1500));
                DialogScratch.SpaceportSelCellA = sc.ushortScratch13; // reseed bar-greeting variant
                WorldState.FlagF3c3 = 0;
                WorldState.FlagF3c4 = 0;
                WorldState.TutorialHintPhase = (short)0x7fff;
                sc.shortA = (short)(SeedEvoRng.Run(30));
                WorldState.RespawnCounter = (short)(sc.shortA + 30);
                SpriteNodes.At(EscortSpawnRecord.Handle).SpritePtr = 0;
                WorldState.HudBlinkCountdown = 0;
                WorldState.MapViewCentreX = SystTable.Store[ship.CurrentSystem].XPos;
                WorldState.MapViewCentreY = SystTable.Store[ship.CurrentSystem].YPos;
                PayEscortWages.Run(1);
                sc.shortA = (short)(EffectiveHyperJumpDays.Run(ship));
                for (sc.shortB = 0; sc.shortB < sc.shortA; sc.shortB = (short)(sc.shortB + 1))
                {
                    TickWorldDailyEvents.Run();
                }
                TickStarJitter.Run();
                ReseedStarJitter.Run();
                for (sc.shortA = 0; sc.shortA < MissionStateTable.Count; sc.shortA = (short)(sc.shortA + 1))
                {
                    if (GameData.MissionStates[sc.shortA].IsActive != 0)
                    {
                        var mission = GameData.Missions[sc.shortA];
                        if (0 < mission.SpawnCount &&
                            (ship.CurrentSystem == mission.DestSystem || mission.DestSystem == -6) &&
                            8 < mission.ShipBehavior)
                        {
                            sc.shortB = (short)(SeedEvoRng.Run(100));
                            mission.MissionShipSpawnCountdown = (short)(sc.shortB + 100);
                            mission.MissionShipsSpawnedCount = 0;
                        }
                        if ((mission.Flags & MisnFlags.AuxShipsReplacedWhenDestroyed) != 0)
                        {
                            mission.RemainingSpawnCount = mission.AuxShipCount;
                        }
                        sc.shortB = (short)(SeedEvoRng.Run(70));
                        mission.SpawnCountdown = (short)(sc.shortB + 70);
                        mission.LiveSpawnCount = 0;
                    }
                }
                ship.JumpWindupTimer = -999;  // hyperspace-arrival sentinel, checked elsewhere as JumpWindupTimer == -999
                RunFleetSpawner.Run((int)ship.CurrentSystem);
                TickPersNagHook.Run();
                RecomputeWorldVisibility.Run();
                ship.JumpWindupTimer = 0;
                if (SystTable.Store[sc.destSystemIndex].Message == -1)
                {
                    sc.shortA = (short)(SeedEvoRng.Run(3));
                    string arriveMsg = "";
                    if (sc.shortA == 0)
                    {
                        arriveMsg = "Entering the ";
                    }
                    if (sc.shortA == 1)
                    {
                        arriveMsg = "Jumping into the ";
                    }
                    if (sc.shortA == 2)
                    {
                        arriveMsg = "Arriving in the ";
                    }
                    arriveMsg += MacToolbox.PascalToString(SystTable.Store[sc.destSystemIndex].Name);
                    arriveMsg += " system on ";
                    arriveMsg += FormatDateLongFull.Format(GameDate.Current.Year, GameDate.Current.Month,
                                                                GameDate.Current.Day);
                    arriveMsg += ".";
                    sc.shortA = 0;
                    for (sc.shortB = 0; sc.shortB < SystRecord.StellarLinkCount; sc.shortB = (short)(sc.shortB + 1))
                    {
                        if (SystTable.SpobLink(ship.CurrentSystem, sc.shortB) != -1)
                        {
                            sc.shortA = (short)(sc.shortA + 1);
                        }
                    }
                    if (sc.shortA == 0)
                    {
                        arriveMsg += " No stellar objects present.";
                    }
                    EnqueueChatterEvent.Run(arriveMsg, 240, 0, 12, UiColors.ChatterText, 0, 0);
                }
                else
                {
                    SpeakSystDiscovery.Run((int)SystTable.Store[sc.destSystemIndex].Message);
                }
                ValidateNavHistoryChain.Run();
                EngageAutopilotToHistoryTarget.Run();
                WorldState.ClearShotsFlag = 1;
                WorldState.ClearCarriedSpritesFlag = 1;
                WorldState.ClearExplosionsFlag = 1;
                WorldState.ClearStreaksFlag = 1;
                WorldState.NoAsteroidsFlag = 1;
                TickSpriteSystem.Run();
                Asteroids.Init();
                WorldState.AutopilotFlag = 0;
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action25)));
        if ((sc.shortA != 0 && ship.JumpWindupTimer < 1) &&
           ((0.05 <= ((double)EvMath.FloatAbs((double)ship.VelX))
            || 0.05 <= ((double)EvMath.FloatAbs((double)ship.VelY)))))
        {
            sc.local_3bc = 0;
            sc.local_3c4 = (float)(100.0 * (double)ship.VelX);
            sc.local_3c0 = (float)(100.0 * (double)ship.VelY);
            sc.shortA = (short)(EvMath.HeadingBetween(sc.local_3bc, sc.local_3bc, sc.local_3c4, sc.local_3c0));
            ship.HeadingPrev = (short)((sc.shortA + 180) % 360);
        }
        sc.local_3e4 = (short)(ShipDerivedStats.EffectiveManeuver(ship));
        if (ship.HeadingPrev == ship.Heading)
        {
            if (ship.JumpWindupTimer < 1)
            {
                sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.TurnLeft)));
                if (sc.shortA != 0)
                {
                    ship.Heading = (short)(ship.Heading - sc.local_3e4);
                }
                sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.TurnRight)));
                if (sc.shortA != 0)
                {
                    ship.Heading = (short)(ship.Heading + sc.local_3e4);
                }
            }
        }
        else
        {
            sc.ushortA = (ushort)((int)ship.HeadingPrev - (int)ship.Heading >> 0x1f);
            if (sc.local_3e4 <=
                (short)((sc.ushortA ^ (ushort)((int)ship.HeadingPrev - (int)ship.Heading)) -
                       sc.ushortA))
            {
                for (sc.floatScratch = (float)((int)ship.HeadingPrev - (int)ship.Heading);
                    360.0f <= sc.floatScratch; sc.floatScratch -= 360.0f)
                {
                }
                for (; sc.floatScratch < 0.0f; sc.floatScratch += 360.0f)
                {
                }
                if (sc.floatScratch <= 180.0f)
                {
                    sc.loopIndex = (int)((float)(int)ship.Heading + (float)(int)sc.local_3e4);
                    ship.Heading = (short)sc.loopIndex;
                }
                else
                {
                    sc.loopIndex = (int)((float)(int)ship.Heading - (float)(int)sc.local_3e4);
                    ship.Heading = (short)sc.loopIndex;
                }
            }
        }
        while (ship.Heading > 359)
        {
            ship.Heading = (short)(ship.Heading + -360);
        }
        while (ship.Heading < 0)
        {
            ship.Heading = (short)(ship.Heading + 360);
        }
        if ((ship.AiActionTimer < 1 &&
            ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action24)))) != 0) && ship.JumpWindupTimer < 1)
        {
            // EffectiveSpeed/EffectiveAccel return fractional doubles (e.g. accel ~0.087) — truncating
            // them to an integer type before calling AccelerateAlongHeading zeroes the thrust.
            sc.speedArg = ShipDerivedStats.EffectiveSpeed(ship);
            sc.accelArg = ShipDerivedStats.EffectiveAccel(ship);
            EvMath.AccelerateAlongHeading(sc.accelArg, sc.speedArg, (int)ship.Heading, ship);
        }
        sc.uintScratch = ShipDerivedStats.EffectiveShieldMax(ship);
        sc.dScratch24 = (double)(int)sc.uintScratch;
        sc.dScratch25 = (double)(float)sc.dScratch24;
        // Player shield recharge: +1% of max per interval while below max, clamped to max. The
        // decompile's literal Shield term is NEGATED, but
        // the ASM (FUN_10027830__TickShipAI.asm ~5586-5651) has no negate at any of the three
        // raw int reads of ship.Shield in this block — that's a decompiler mis-rendering, not real
        // source logic. The POSITIVE int-valued Shield read here is the ASM-faithful translation;
        // do not "fix" this back toward the decompile literal (it would reintroduce an insta-explode
        // on full-shield ships). Same root cause as UpdateShipAiFrame's recharge.
        if ((((double)(float)(int)ship.Shield < sc.dScratch25) &&
            !(ShipDerivedStats.IsDyingOrDestroyed(ship))) &&
           ((int)WorldState.GameFrameTickCounter ==
           ((int)WorldState.GameFrameTickCounter / (int)(sc.local_3de = (short)(ShipDerivedStats.EffectiveShieldRecharge(ship)))) * (int)sc.local_3de))
        {
            sc.floatScratch = (float)(int)(0.01 * sc.dScratch25 + (double)(int)ship.Shield);
            ship.Shield = sc.floatScratch;
            if (sc.dScratch25 < (double)(float)(int)ship.Shield)
            {
                sc.floatScratch = (float)(int)sc.dScratch24;
                ship.Shield = sc.floatScratch;
            }
            WorldState.PlayerShieldBarDirty = 1;
        }
        if (ship.JumpWindupTimer > 1)
        {
            sc.local_3bc = (int)(float)(int)SystTable.Store[ship.CurrentSystem].XPos;
            sc.local_3b8 = (int)(float)(int)SystTable.Store[ship.CurrentSystem].YPos;
            sc.shortA = SystTable.Store[ship.CurrentSystem].HyperLink[ship.NavTargetSpob];
            sc.local_3c4 = (float)(int)SystTable.Store[sc.shortA].XPos;
            sc.local_3c0 = (float)(int)SystTable.Store[sc.shortA].YPos;
            sc.shortA = (short)(EvMath.HeadingBetween(sc.local_3bc, sc.local_3b8, sc.local_3c4, sc.local_3c0));
            sc.shortB = (short)(ShipDerivedStats.EffectiveManeuver(ship));
            sc.uintScratch = (uint)((int)ship.Heading - (int)sc.shortA >> 0x1f);
            if ((int)((sc.uintScratch ^ (int)ship.Heading - (int)sc.shortA) - sc.uintScratch) <= (int)sc.shortB)
            {
                sc.loopIndex = (int)(MacToolbox.TickCount());
                sc.dScratch24 = (double)(float)((double)(GameData.ShipClasses[ship.ShipClass].SpriteScale *
                                                 (float)(double)(uint)(sc.loopIndex - ship.AiTickStamp)) /
                                         4.6 -
                                        35.0 /
                                        (double)GameData.ShipClasses[ship.ShipClass].SpriteScale);
                if (sc.dScratch24 > 0.0)
                {
                    {
                        float px = ship.PosX, py = ship.PosY;
                        EvMath.OffsetByHeading(sc.dScratch24, (int)ship.Heading, ref px, ref py);
                        ship.PosX = px; ship.PosY = py;
                    }
                }
            }
        }
        if (WorldState.PlayerSpeedCapX < ship.VelX)
        {
            ship.VelX = WorldState.PlayerSpeedCapX;
        }
        if (ship.VelX < -WorldState.PlayerSpeedCapX)
        {
            ship.VelX = -WorldState.PlayerSpeedCapX;
        }
        if (WorldState.PlayerSpeedCapY < ship.VelY)
        {
            ship.VelY = WorldState.PlayerSpeedCapY;
        }
        if (ship.VelY < -WorldState.PlayerSpeedCapY)
        {
            ship.VelY = -WorldState.PlayerSpeedCapY;
        }
        ship.PosX += (float)((double)ship.VelX * WorldState.TimeScale);
        ship.PosY += (float)((double)ship.VelY * WorldState.TimeScale);
        sc.dScratch24 = (double)EvMath.FloatAbs((double)ship.PosX);
        _ = (int)(float)sc.dScratch24;  // |PosX| computed but unused — the bounds check below tests |PosY| twice (EVO bug, preserved)
        sc.dScratch24 = (double)EvMath.FloatAbs((double)ship.PosY);
        sc.local_3c8 = (int)(float)sc.dScratch24;
        if ((20000.0f < (float)sc.local_3c8) || (20000.0f < (float)sc.local_3c8))
        {
            sc.local_3cc = 0;
            sc.local_3c8 = 0;
            sc.dScratch25 = (double)ShipDerivedStats.EffectiveSpeed(ship);
            sc.dScratch24 = 0.1;
            sc.dScratch26 = (double)ShipDerivedStats.EffectiveAccel(ship);
            sc.dScratch27 = 1.1;
            sc.longA = EvMath.HeadingBetween(ship.PosX, ship.PosY, sc.local_3cc, sc.local_3c8);
            // headingIndex must be the WHOLE HeadingBetween result truncated to int, not bits 32-63
            // (a known decompiler register-pair misrender class; see also ApplyShipDamage,
            // RespawnEscortAdjacentToPlayer, UpdateProjectilePositions).
            EvMath.AccelerateAlongHeading((double)(float)(sc.dScratch27 * sc.dScratch26), (double)(float)(sc.dScratch24 * sc.dScratch25),
                         (int)sc.longA, ship);
        }
        if (ship.PosX > 20256.0f)
        {
            ship.PosX = 20256.0f;
        }
        if (ship.PosX < -20256.0f)
        {
            ship.PosX = -20256.0f;
        }
        if (ship.PosY > 20256.0f)
        {
            ship.PosY = 20256.0f;
        }
        if (ship.PosY < -20256.0f)
        {
            ship.PosY = -20256.0f;
        }
        if (ship.NavMode == 3 && ship.NavTargetSpob != -1)
        {
            sc.clearOfStellarsFlag = 1;
            for (sc.shortA = 0; sc.shortA < SystRecord.StellarLinkCount; sc.shortA = (short)(sc.shortA + 1))
            {
                if (SystTable.SpobLink(ship.CurrentSystem, sc.shortA) != -1)
                {
                    sc.uintScratch = (uint)(ShipDerivedStats.EffectiveHyperRangeSquared(ship));
                    sc.dScratch24 = (double)(int)sc.uintScratch;
                    sc.dScratch25 = (double)EvMath.FloatAbs(EvMath.DistanceSquared(0.0f, 0.0f, ship.PosX, ship.PosY));
                    if (sc.dScratch25 <= (double)(float)sc.dScratch24)
                    {
                        sc.clearOfStellarsFlag = 0;
                    }
                }
            }
            if (sc.clearOfStellarsFlag == 0)
            {
                PlayerKeyLatches.HyperRangeReachedLatch = false;
            }
            else
            {
                if ((!PlayerKeyLatches.HyperRangeReachedLatch) && ship.JumpWindupTimer < 1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    WorldState.SpawnPulseDirty = 1;
                }
                PlayerKeyLatches.HyperRangeReachedLatch = true;
            }
        }
        if (ship.AiActionTimer > 0)
        {
            ship.AiActionTimer = (short)(ship.AiActionTimer + -1);
        }
        if (ship.DeathTimer > 0.0f)
        {
            ship.DeathTimer -= 1.0f;
            if (WorldState.IsCloaked)
            {
                DisengageCloaking.Run();
            }
        }
        for (sc.shortA = 0; sc.shortA < OutfitTable.Count; sc.shortA = (short)(sc.shortA + 1))
        {
            for (sc.shortB = 0; sc.shortB < OutfitRecord.ModBankCount; sc.shortB = (short)(sc.shortB + 1))
            {
                var outfitRec = OutfitTable.Store[sc.shortA];
                if (((short)outfitRec.ModType[sc.shortB] == 18) && OwnedOutfitGrid.Store[sc.shortA] > 0)
                {
                    // sign-magnitude abs of the fuel-mod period, then a 1-in-N RNG roll
                    sc.uintScratch6 = (uint)outfitRec.ModValue[sc.shortB];
                    sc.uintScratch = (uint)((int)sc.uintScratch6 >> 0x1f);
                    sc.shortC = (short)(SeedEvoRng.Run((short)((sc.uintScratch ^ (int)sc.uintScratch6) - sc.uintScratch)));
                    if (sc.shortC == 0 && ship.JumpWindupTimer < 1)
                    {
                        if (outfitRec.ModValue[sc.shortB] < 0)
                        {
                            ship.Fuel -= 1.0f;
                        }
                        else
                        {
                            ship.Fuel += 1.0f;
                        }
                        WorldState.ShieldEnergyBarDirty = 1;
                    }
                }
            }
        }
        var playerCls = GameData.ShipClasses[GameData.Player.ShipClass];
        if (playerCls.FuelRegen > 0 && ((playerCls.Flags & ShipFlags.UseFuelRegen) != 0))
        {
            sc.uintScratch6 = (uint)playerCls.FuelRegen;
            sc.uintScratch = (uint)((int)sc.uintScratch6 >> 0x1f);
            sc.shortA = (short)(SeedEvoRng.Run((short)((sc.uintScratch ^ (int)sc.uintScratch6) - sc.uintScratch)));
            if (sc.shortA == 0)
            {
                ship.Fuel += 1.0f;
                WorldState.ShieldEnergyBarDirty = 1;
            }
        }
        sc.fuelMax = (short)(ShipDerivedStats.EffectiveFuelMax(ship));
        if ((float)(int)sc.fuelMax < ship.Fuel)
        {
            ship.Fuel = (float)(int)sc.fuelMax;
        }
        if (ship.Fuel < 0.0f)
        {
            ship.Fuel = 0.0f;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action6)));
        if (sc.shortA != 0 && WorldState.FlashChatterCountdown > 0)
        {
            WorldState.FlashChatterCountdown = 0;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action38)));
        // DEAD debug/cheat hotkey (rearm + refuel + chart-all): CheatShowAll == unk_E021D,
        // permanently 0 — see the note at the top of Run and DEV_DEBUG_CODE.md DDC-15.
        if ((sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x79))) != 0) && WorldState.CheatShowAll != 0)
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            var reloadCls = GameData.ShipClasses[ship.ShipClass];
            for (sc.shortA = 0; sc.shortA < ShipRecord.WeaponSlotCount; sc.shortA = (short)(sc.shortA + 1))
            {
                if (reloadCls.DefaultWeaponAmmo[sc.shortA] != -1)
                {
                    ship.WeaponSlotAmmo[sc.shortA] = reloadCls.DefaultWeaponAmmo[sc.shortA];
                }
            }
            for (sc.shortA = 0; sc.shortA < MapNebulaTable.Count; sc.shortA = (short)(sc.shortA + 1))
            {
                MapNebulaTable.Store[sc.shortA].Charted = 1;
            }
            ship.CreditsEasterEggShown = 1;
            FloodVisitedSysts.Run(GameData.Player.CurrentSystem, 1024);
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
            if (sc.shortA == 0)
            {
                sc.shortA = (short)(ShipDerivedStats.EffectiveFuelMax(ship));
                ship.Fuel = (float)(int)sc.shortA;
            }
            else
            {
                ship.Fuel = 0.0f;
            }
            sc.floatScratch = (float)ShipDerivedStats.EffectiveShieldMax(ship);
            ship.Shield = sc.floatScratch;
            ship.DeathTimer = -1.0f;
            WorldState.ShieldEnergyBarDirty = 1;
            WorldState.PlayerShieldBarDirty = 1;
            WorldState.HudWeaponPanelDirty = 1;
        }
        // DEAD debug/cheat hotkeys (call defenders + match target velocity): CheatShowAll ==
        // unk_E021D, permanently 0 — see the note at the top of Run and DDC-15.
        if (WorldState.CheatShowAll != 0)
        {
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x43));
            if ((sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x6f))) != 0) &&
               ((ship.TargetSlot != -1 &&
                (GameData.Ships[ship.TargetSlot].IsActive != 0))))
            {
                ShipAi.CallForDefendersAndEngagePlayer(ShipTable.Ships[ship.TargetSlot]);
            }
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x59));
            if (sc.shortA != 0 && ship.TargetSlot != -1)
            {
                ship.VelX = GameData.Ships[ship.TargetSlot].VelX;
                ship.VelY = GameData.Ships[ship.TargetSlot].VelY;
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action17)));
        if ((sc.shortA == 0 && (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action18))) == 0) &&
           (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action19))) == 0)
        {
            PlayerKeyLatches.EscortCommandKeyLatch = false;
        }
        else if ((!PlayerKeyLatches.EscortCommandKeyLatch) && ship.JumpWindupTimer == 0)
        {
            PlayerKeyLatches.EscortCommandKeyLatch = true;
            sc.escortCommand = -1;
            sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action18)));
            if (sc.shortA != 0)
            {
                sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
                if (sc.shortA == 0)
                {
                    sc.escortCommand = 1;
                }
                else
                {
                    sc.escortCommand = 0;
                }
            }
            sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action17)));
            if (sc.shortA != 0)
            {
                sc.escortCommand = 2;
            }
            sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action19)));
            if (sc.shortA != 0)
            {
                sc.escortCommand = 3;
            }
            sc.shortA = 0;
            for (sc.shortB = 1; sc.shortB < ShipTable.Count; sc.shortB = (short)(sc.shortB + 1))
            {
                if ((ship.SlotIndex == GameData.Ships[sc.shortB].OwnerSlot &&
                    (GameData.Ships[sc.shortB].IsActive != 0)) &&
                   ((-900 < GameData.Ships[sc.shortB].JumpWindupTimer &&
                    (GameData.Ships[sc.shortB].JumpWindupTimer < 1))))
                {
                    if ((sc.escortCommand == 0 &&
                        !ShipAi.IsStateLeaving(ShipTable.Ships[sc.shortB])) &&
                       (GameData.Ships[sc.shortB].AiBehaviorType == ShipAiType.NavalFighter))
                    {
                        ShipAi.SetStateLeavingFollowSelf(ShipTable.Ships[sc.shortB]);
                        sc.shortA = (short)(sc.shortA + 1);
                    }
                    if (sc.escortCommand == 1 &&
                       !ShipAi.IsStateHyperWindup(ShipTable.Ships[sc.shortB]))
                    {
                        ShipAi.SetStateHyperWindupAndPropagate(ShipTable.Ships[sc.shortB]);
                        if (GameData.Ships[sc.shortB].AiBehaviorType == ShipAiType.Escort)
                        {
                            sc.shortC = (short)(SeedEvoRng.Run(150));
                            GameData.Ships[sc.shortB].AiActionTimer = (short)(sc.shortC + 100);
                        }
                        sc.shortA = (short)(sc.shortA + 1);
                    }
                    if ((sc.escortCommand == 2 &&
                        !ShipAi.IsStateCombat(ShipTable.Ships[sc.shortB])) &&
                       ((GameData.Ships[sc.shortB].AiBehaviorType == ShipAiType.NavalFighter &&
                        (((ship.TargetSlot != -1 &&
                          (ship.SlotIndex != ship.TargetSlot)) &&
                        (GameData.Ships[ship.TargetSlot].OwnerSlot != 0))))))
                    {
                        ShipAi.SetStateRetaliateAgainstGovt(ShipTable.Ships[sc.shortB],
                                     ShipTable.Ships[ship.TargetSlot]);
                        sc.shortA = (short)(sc.shortA + 1);
                    }
                    if (sc.escortCommand == 3 &&
                       !ShipAi.IsStateLandingApproach(ShipTable.Ships[sc.shortB]))
                    {
                        ShipAi.SetStateLanding(ShipTable.Ships[sc.shortB]);
                        sc.shortA = (short)(sc.shortA + 1);
                    }
                }
            }
            if (sc.shortA > 0)
            {
                if (sc.escortCommand == 0)
                {
                    EnqueueChatterEvent.Run("Acknowledged - fighters returning to mothership.", 120, 0, 12, UiColors.ChatterText, 0, 0);
                    SetActiveChatterSpeaker.Run(0);
                }
                if (sc.escortCommand == 1)
                {
                    EnqueueChatterEvent.Run("Acknowledged - escorts returning to formation.", 120, 0, 12, UiColors.ChatterText, 0, 0);
                    SetActiveChatterSpeaker.Run(0);
                }
                if (sc.escortCommand == 2)
                {
                    // The original's NumToString(count, buf) call is dead here — the chatter text
                    // uses a literal and the buffer is never read, so it's omitted.
                    EnqueueChatterEvent.Run("Affirmative - fighters engaging new target.", 120, 0, 12, UiColors.ChatterText, 0, 0);
                    SetActiveChatterSpeaker.Run(1);
                }
                if (sc.escortCommand == 3)
                {
                    EnqueueChatterEvent.Run("Affirmative - escorts holding position.", 120, 0, 12, UiColors.ChatterText, 0, 0);
                    SetActiveChatterSpeaker.Run(3);
                }
            }
        }
        // DEAD debug/cheat gate (boarding-alarm audio): CheatShowAll == unk_E021D, permanently 0
        // — see the note at the top of Run and DDC-15.
        if (WorldState.CheatShowAll != 0)
        {
            TickBoardingAlarmAudio(ship, sc);
        }
        if (ship.Credits < 0)
        {
            ship.Credits = 1610612735;   // credits cap (overflow wraps to the cap, not 0 — faithful)
            WorldState.HudStatusPanelDirty = 1;
        }
        if (ship.Credits > 1610612735)
        {
            ship.Credits = 1610612735;   // credits cap
            WorldState.HudStatusPanelDirty = 1;
        }
        for (sc.shortA = 0; sc.shortA < ShipRecord.CargoHoldCount; sc.shortA = (short)(sc.shortA + 1))
        {
            if (ship.CargoHold[sc.shortA] < 0)
            {
                ship.CargoHold[sc.shortA] = 0;
                WorldState.HudStatusPanelDirty = 1;
            }
        }
        for (sc.shortA = 0; sc.shortA < JunkTable.Count; sc.shortA = (short)(sc.shortA + 1))
        {
            if (GameData.Junk[sc.shortA].PlayerQty < 0)
            {
                GameData.Junk[sc.shortA].PlayerQty = 0;
                WorldState.HudStatusPanelDirty = 1;
            }
        }
        if (ship.DeathTimer > 1.0f && ((short)(CountMatchingSoundVoices.Run(SoundResourceCells.DeathCountdownSnd))) == 0)
        {
            SndPlay.Run(SoundResourceCells.DeathCountdownSnd, 15, 128, 128);
        }
        if (ship.DeathTimer > 1.0f)
        {
            sc.found = false;
            if ((((double)ship.DeathTimer <=
                  0.5 * (double)(int)GameData.ShipClasses[ship.ShipClass].DeathDelay) ||
                (ship.DeathTimer <= 30.0f)) && ShipDerivedStats.HasAutoEject(ship))
            {
                sc.found = true;
            }
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x3f));
            if (((sc.shortA != 0 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action20)))) != 0) || (sc.found)) &&
               ((ShipDerivedStats.HasEscapePod(ship) &&
                (sc.shortA = (short)(AllocateShipSlot.Run(ship.CurrentSystem, 0))) != -1)))
            {
                for (sc.shortB = 1; sc.shortB < ShipTable.Count; sc.shortB = (short)(sc.shortB + 1))
                {
                    if (GameData.Ships[sc.shortB].IsActive != 0 &&
                       (GameData.Ships[sc.shortB].TargetSlot == 0))
                    {
                        GameData.Ships[sc.shortB].TargetSlot = -1;
                    }
                }
                GameData.Ships[sc.shortA].AiBehaviorType = ShipAiType.Inactive;
                GameData.Ships[sc.shortA].PosX = ship.PosX;
                GameData.Ships[sc.shortA].PosY = ship.PosY;
                GameData.Ships[sc.shortA].VelX = ship.VelX;
                GameData.Ships[sc.shortA].VelY = ship.VelY;
                GameData.Ships[sc.shortA].Heading = ship.Heading;
                GameData.Ships[sc.shortA].ShipClass = ship.ShipClass;
                GameData.Ships[sc.shortA].DeathTimer = ship.DeathTimer;
                GameData.Ships[sc.shortA].Shield = ship.Shield;
                ship.ShipClass = ShipRecord.EmptyShipClass;
                ship.DeathTimer = 0.0f;
                ship.TargetSlot = -1;
                ship.NavTargetSpob = -1;
                // Shield must be assigned as a numeric int-valued copy of the class's base shield,
                // not a bit-reinterpret — matches SpawnFleet and the other spawners.
                ship.Shield = GameData.ShipClasses[ship.ShipClass].Shield;
                WorldState.HudWeaponPanelDirty = 1;
                WorldState.HudStatusPanelDirty = 1;
                WorldState.WeaponSlotDirty = 1;
                WorldState.SpawnPulseDirty = 1;
                WorldState.ShieldEnergyBarDirty = 1;
                for (sc.shortA = 0; sc.shortA < ShipRecord.WeaponSlotCount; sc.shortA = (short)(sc.shortA + 1))
                {
                    ship.WeaponSlotType[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponType[sc.shortA];
                    ship.WeaponSlotAmmo[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponAmmo[sc.shortA];
                }
                for (sc.shortA = 0; sc.shortA < OwnedOutfitGrid.Count; sc.shortA = (short)(sc.shortA + 1))
                {
                    OwnedOutfitGrid.Store[sc.shortA] = 0;
                }
                SndPlay.Run(CombatSoundCells.ScanSweepSnd, 50, 128, 128);
                sc.shortA = (short)(CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd));
                if (sc.shortA != 0)
                {
                    FlushMixQueueEntries.Run(SoundResourceCells.BoardingChimeSnd);
                }
                ship.JumpWindupTimer = -1;
                WorldState.WorldCountdown = 420;
            }
        }
        sc.afterburnerEngagedFlag = 0;
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action27)));
        if (((sc.shortA != 0 && ShipDerivedStats.HasAfterburner(ship)) &&
            (ship.JumpWindupTimer < 1)) &&
           ((0.0f < ship.Fuel && ship.AiActionTimer < 1)))
        {
            sc.afterburnerEngagedFlag = 1;
        }
        if (sc.afterburnerEngagedFlag == 0)
        {
            sc.dScratch24 = (double)ShipDerivedStats.EffectiveSpeed(ship);
            sc.dScratch25 = (double)ShipDerivedStats.EffectiveAccel(ship);
            if (sc.dScratch24 < (double)WorldState.PlayerSpeedCapX)
            {
                WorldState.PlayerSpeedCapX = (float)(WorldState.PlayerSpeedCapX - (float)(0.4 * sc.dScratch25));
            }
            if (sc.dScratch24 < (double)WorldState.PlayerSpeedCapY)
            {
                WorldState.PlayerSpeedCapY = (float)(WorldState.PlayerSpeedCapY - (float)(0.4 * sc.dScratch25));
            }
            if ((double)WorldState.PlayerSpeedCapX < sc.dScratch24)
            {
                WorldState.PlayerSpeedCapX = (float)sc.dScratch24;
            }
            if ((double)WorldState.PlayerSpeedCapY < sc.dScratch24)
            {
                WorldState.PlayerSpeedCapY = (float)sc.dScratch24;
            }
        }
        else
        {
            sc.dScratch24 = (double)ShipDerivedStats.EffectiveSpeed(ship);
            WorldState.PlayerSpeedCapX = (float)(1.8 * sc.dScratch24);
            sc.dScratch24 = (double)ShipDerivedStats.EffectiveSpeed(ship);
            WorldState.PlayerSpeedCapY = (float)(1.8 * sc.dScratch24);
            sc.dScratch24 = (double)ShipDerivedStats.EffectiveAccel(ship);
            {
                float vx = ship.VelX, vy = ship.VelY;
                EvMath.OffsetByHeading((double)(float)(2.75 * sc.dScratch24),
                           (int)ship.Heading, ref vx, ref vy);
                ship.VelX = vx; ship.VelY = vy;
            }
            sc.uintScratch = (uint)WorldState.GameFrameTickCounter;
            if (sc.uintScratch == (((int)sc.uintScratch >> 2) + (uint)(((int)sc.uintScratch < 0 && (sc.uintScratch & 3) != 0) ? 1 : 0)) * 4)
            {
                ship.Fuel -= 5.0f;
                WorldState.ShieldEnergyBarDirty = 1;
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x3f));
        if (sc.shortA != 0 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action15)))) != 0)
        {
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
            if (sc.shortA == 0)
            {
                RunFramePostMortem.Run(1);
            }
            else
            {
                RunFramePostMortem.Run(0);
            }
            WorldState.HudStatusPanelDirty = 1;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x3f));
        if (((sc.shortA != 0 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action21)))) != 0) &&
            (WorldState.HyperCountdown < 0)) &&
           ((!(ShipDerivedStats.IsDisabled(ship)) &&
            !(ShipDerivedStats.IsDyingOrDestroyed(ship)))))
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 15, 128, 128);
            EnqueueChatterEvent.Run("Self-destruct sequence initiated.", 40, 0, 12, UiColors.ChatterText, 0, 0);
            WorldState.HyperCountdown = 360;
        }
        if (WorldState.HyperCountdown > 0)
        {
            WorldState.HyperCountdown = (short)(WorldState.HyperCountdown + -1);
            if (((int)WorldState.HyperCountdown % 30 == 0) && WorldState.HyperCountdown < 301)
            {
                string destructMsg = "Self-destruct in " + ((int)WorldState.HyperCountdown / 30) + " second";
                if (WorldState.HyperCountdown > 30)
                {
                    destructMsg += "s";
                }
                destructMsg += ".";
                SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 15, 128, 128);
                EnqueueChatterEvent.Run(destructMsg, 40, 0, 12, UiColors.ChatterText, 0, 0);
            }
        }
        if (WorldState.HyperCountdown == 0)
        {
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x3f));
            if (sc.shortA == 0 || ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action21)))) == 0)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 15, 128, 128);
                EnqueueChatterEvent.Run("Self-destruct sequence cancelled.", 40, 0, 12, UiColors.ChatterText, 0, 0);
            }
            else
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 15, 128, 128);
                EnqueueChatterEvent.Run("Have a nice day.", 40, 0, 12, UiColors.ChatterText, 0, 0);
                // Sink the shield/armor cell past the death line so the ship self-destructs.
                // The ASM (sub_27830 "li r9,-0x7D00; stw r9,0x68(r30)") is an INTEGER store of
                // -32000; Ghidra renders it "param_1[0x1a] = -NAN" only because the cell is typed
                // float* and -32000 (0xFFFF8300) reads as a negative NaN. The port keeps the cell
                // as an integer VALUE read via (int)Shield, so float.NaN would (int)-convert to 0
                // and leave the ship alive. Same self-destruct/disabled-armor marker as SpawnFromShip.
                ship.Shield = -32000f;
            }
            WorldState.HyperCountdown = -1;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action28)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.CaptureDialogKeyLatch = false;
        }
        else if (!PlayerKeyLatches.CaptureDialogKeyLatch)
        {
            PlayerKeyLatches.CaptureDialogKeyLatch = true;
            if (((ship.JumpWindupTimer < 1 && WorldState.WorldCountdown < 1) &&
                (WorldState.GameFrameTickCounter > -1)) && !(ShipDerivedStats.IsDyingOrDestroyed(ship)))
            {
                MacToolbox.ShowCursor();
                RunPlayerInfoDialog.Run();
                MacToolbox.HideCursor();
                RefreshStatusPanel.Run();
                DispatchPendingChatter.Run(0);
            }
            else
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 5, 128, 128);
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action4)));
        if (sc.shortA == 0)
        {
            PlayerKeyLatches.HailKeyLatch = false;
        }
        else if (!PlayerKeyLatches.HailKeyLatch)
        {
            PlayerKeyLatches.HailKeyLatch = true;
            PlayerHailAction.Run();
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action43)));
        if ((sc.shortA == 0 || ship.JumpWindupTimer > 0) ||
           (((0 < WorldState.WorldCountdown ||
             ((ShipDerivedStats.IsDyingOrDestroyed(ship) || ship.DeathTimer > 0.0f)))
            || WorldState.GameFrameTickCounter < 0)))
        {
            PlayerKeyLatches.OutfitterKeyLatch = false;
        }
        else if (!PlayerKeyLatches.OutfitterKeyLatch)
        {
            PlayerKeyLatches.OutfitterKeyLatch = true;
            sc.shortA = 0;
            for (sc.shortB = 0; sc.shortB < MissionStateTable.Count; sc.shortB = (short)(sc.shortB + 1))
            {
                if (GameData.MissionStates[sc.shortB].IsActive != 0)
                {
                    sc.shortA = (short)(sc.shortA + 1);
                }
            }
            if (sc.shortA < 1)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                EnqueueChatterEvent.Run("You have no active missions.", 240, 0, 12, UiColors.ChatterText, 0, 0);
            }
            else
            {
                MacToolbox.ShowCursor();
                RunMissionInfoDialog.Run();
                MacToolbox.HideCursor();
                RefreshStatusPanel.Run();
                DispatchPendingChatter.Run(0);
            }
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action44)));
        if ((sc.shortA == 0 || ship.DeathTimer > 0.0f) || ship.JumpWindupTimer > 0)
        {
            PlayerKeyLatches.CloakToggleKeyLatch = false;
        }
        else if (!PlayerKeyLatches.CloakToggleKeyLatch)
        {
            PlayerKeyLatches.CloakToggleKeyLatch = true;
            sc.flag = ShipDerivedStats.HasCloakingDevice(ship) ? (byte)1 : (byte)0;
            if (sc.flag == 0)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
            }
            else if (!WorldState.IsCloaked)
            {
                if (ship.Fuel <= 0.0f)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                }
                else
                {
                    EngageCloaking.Run();
                }
            }
            else
            {
                DisengageCloaking.Run();
            }
        }
        if (WorldState.IsCloaked)
        {
            if (ship.Fuel > 0.0f)
            {
                sc.uintScratch = (uint)WorldState.GameFrameTickCounter;
                if (sc.uintScratch == (((int)sc.uintScratch >> 3) + (uint)(((int)sc.uintScratch < 0 && (sc.uintScratch & 7) != 0) ? 1 : 0)) * 8)
                {
                    ship.Fuel -= 1.0f;
                    WorldState.ShieldEnergyBarDirty = 1;
                }
            }
            else
            {
                DisengageCloaking.Run();
            }
        }
        if ((WorldState.GameFrameTickCounter == 0 || WorldState.GameFrameTickCounter == 250) ||
           ((WorldState.GameFrameTickCounter == 500 || WorldState.GameFrameTickCounter == 750)))
        {
            for (sc.shortA = 0; sc.shortA < JunkTable.Count; sc.shortA = (short)(sc.shortA + 1))
            {
                if ((((JunkFlags)GameData.Junk[sc.shortA].Flags & JunkFlags.Tribbles) != 0) &&
                   (GameData.Junk[sc.shortA].PlayerQty > 0))
                {
                    sc.shortB = (short)(TotalMassWithEscorts.Run());
                    sc.shortC = (short)(ShipDerivedStats.TotalMassCarried(ship));
                    if (0 < (short)(sc.shortB - sc.shortC))
                    {
                        GameData.Junk[sc.shortA].PlayerQty = (short)(GameData.Junk[sc.shortA].PlayerQty + 1);
                        WorldState.HudStatusPanelDirty = 1;
                    }
                }
            }
        }
        if (WorldState.TutorialHintPhase < 2 && ((int)WorldState.GameFrameTickCounter % 60 == 0))
        {
            sc.dScratch24 = (double)EvMath.FloatAbs(EvMath.DistanceSquared(0.0f, 0.0f, ship.PosX, ship.PosY));
            if (WorldState.TutorialHintPhase == -3)
            {
                if (SystTable.SpobLink(GameData.Player.CurrentSystem, 0) != -1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    short systSpob = SystTable.SpobLink(GameData.Player.CurrentSystem, 0);
                    string welcomeMsg;
                    if (((SpobFlags)GameData.Spobs[systSpob].Flags & SpobFlags.Station) == 0)
                    {
                        welcomeMsg = "Welcome to EV Override - it would be a good idea to start by landing on ";
                    }
                    else
                    {
                        welcomeMsg = "Welcome to EV Override - it would be a good idea to start by docking at ";
                    }
                    welcomeMsg += Trunc(GameData.Spobs[systSpob].Name, 31);
                    welcomeMsg += " and checking out the prices. Hit ‘";
                    welcomeMsg += MacToolbox.GetIndString(0x81, (short)(Keymap.Slot(KeyAction.Land) + 1));
                    welcomeMsg += "’ to request landing clearance, then hit it again to land.";
                    EnqueueChatterEvent.Run(welcomeMsg, 512, 0, 12, UiColors.ChatterText, 0, 0);
                }
                WorldState.TutorialHintPhase = -2;
            }
            if (WorldState.TutorialHintPhase == -1)
            {
                if (SystTable.SpobLink(GameData.Player.CurrentSystem, 0) != -1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    string hyperMsg = "Now you should probably hyperspace to another system. Hit ‘"
                        + MacToolbox.GetIndString(0x81, (short)(Keymap.Slot(KeyAction.Action9) + 1))
                        + "’ to access the map, select a nearby system, then move outward and hit ‘"
                        + MacToolbox.GetIndString(0x81, (short)(Keymap.Slot(KeyAction.Action14) + 1))
                        + "’ to begin your jump.";
                    EnqueueChatterEvent.Run(hyperMsg, 512, 0, 12, UiColors.ChatterText, 0, 0);
                }
                WorldState.TutorialHintPhase = 0;
            }
            if (sc.dScratch24 <= 2000000.0 || WorldState.TutorialHintPhase > 0)
            {
                if (sc.dScratch24 > 5000000.0 && WorldState.TutorialHintPhase == 1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    EnqueueChatterEvent.Run("There’s nothing to find out here - if you want to get to another system, you’ll have to access the map, set a hyperdrive destination, and start your jump.", 512, 0, 12, UiColors.ChatterText, 0, 0);
                    WorldState.TutorialHintPhase = 2;
                }
            }
            else
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                EnqueueChatterEvent.Run("You’re well beyond safe hyperspace range - you can initiate hyperjump at any time.", 512, 0, 12, UiColors.ChatterText, 0, 0);
                WorldState.TutorialHintPhase = 1;
            }
        }
        if (WorldState.GameFrameTickCounter == 500 || WorldState.GameFrameTickCounter == 1000)
        {
            sc.local_3e4 = (short)(TotalMassWithEscorts.Run());
            sc.shortA = (short)(ShipDerivedStats.TotalMassCarried(ship));
            sc.dScratch24 = (double)(int)sc.shortA;
            if ((double)(float)(int)sc.local_3e4 < (double)(float)sc.dScratch24)
            {
                for (sc.shortA = 0; sc.shortA < ShipRecord.CargoHoldCount; sc.shortA = (short)(sc.shortA + 1))
                {
                    ship.CargoHold[sc.shortA] =
                         (short)(int)((double)(int)ship.CargoHold[sc.shortA] *
                                     ((double)(int)sc.local_3e4 / (double)(float)sc.dScratch24));
                }
                WorldState.HudStatusPanelDirty = 1;
            }
        }
        if (ship.Credits < 0)
        {
            ship.Credits = 0;
            WorldState.HudStatusPanelDirty = 1;
        }
        if (ship.Fuel < 0.0f)
        {
            ship.Fuel = 0.0f;
            WorldState.ShieldEnergyBarDirty = 1;
        }
        if (SystTable.Store[ship.CurrentSystem].Visited < 1)
        {
            SystTable.Store[ship.CurrentSystem].Visited = 1;
        }
        return;
    }

    // ---- extracted phases of the player tick (each peeled from Run for readability) ----

    // Pascal/C-string truncation semantics of the old fixed-size staging copies
    // (WritePascalString(buf, s, max) / strncpy): keep at most `max` chars.
    private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    // Ship is gone (HasWorldSpriteNode == 0): clear targets, drain the wreck timer, and — if the player
    // pressed the New-Pilot key — reinitialize the world. Run returns after calling this.
    // The quit gate (opens the "Are you sure you want to quit?" alert): not winding up a jump or
    // counting down, and one of two keymap-bit + AI-flag combinations is held. The reads are
    // side-effect-free, so factoring the condition out preserves short-circuit order.
    private static bool PlayerRequestedQuit(ShipRec ship)
    {
        return ship.JumpWindupTimer < 1 && WorldState.WorldCountdown < 1 &&
            ((!ShipDerivedStats.IsDyingOrDestroyed(ship) && (short)Keymap.TestCachedKeymapBit(0x4) != 0 &&
              WorldState.AiBehaviorFlagB == 0 && WorldState.AiBehaviorFlagA != 0) ||
             ((short)Keymap.TestCachedKeymapBit(0x3f) != 0 && (short)Keymap.TestCachedKeymapBit(0x4) != 0 &&
              WorldState.AiBehaviorFlagA == 0 && WorldState.AiBehaviorFlagB == 0));
    }

    private static void HandleDestroyedShip(ShipRec ship)
    {
        ship.TargetSlot = -1;
        ship.NavTargetSpob = -1;
        if (ship.DeathTimer <= -240.0f)
        {
            if (ship.ShipClass != ShipRecord.EmptyShipClass && WorldState.StrictPlay != 0)
            {
                DeletePilotFileIfExists.Run(PilotIdentity.Name);
            }
            EvoGlobals.PlayerDead = true; // the original wrote an INT 1 into the death BYTE (width quirk, folded into the managed bool)
        }
        else
        {
            ship.DeathTimer -= 1.0f;
        }
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action35)) == 0)
            return;
        InitGameWorldState.Run(1);
        ship.TargetSlot = -1;
        ship.SelectedWeaponSlot = -1;
        RefreshStatusPanel.Run();
        DispatchPendingChatter.Run(0);
    }

    // Launch / landing countdown is running (WorldCountdown > 0): coast the ship, decrement the
    // counter, and when it reaches 0 play the arrival sequence (fades, world re-init, autosave).
    // Run returns after calling this (entering the block always returned in the decompile).
    private static void LaunchCountdownTick(ShipRec ship)
    {
        // EffectiveSpeed/EffectiveAccel return fractional doubles (e.g. accel ~0.087) — truncating
        // them to an integer type before calling AccelerateAlongHeading zeroes the thrust.
        double speedArg = ShipDerivedStats.EffectiveSpeed(ship);
        double accelArg = ShipDerivedStats.EffectiveAccel(ship);
        EvMath.AccelerateAlongHeading(accelArg, speedArg, (int)ship.Heading, ship);
        ship.PosX += (float)((double)ship.VelX * WorldState.TimeScale);
        ship.PosY += (float)((double)ship.VelY * WorldState.TimeScale);
        WorldState.WorldCountdown = (short)(WorldState.WorldCountdown + -1);
        if (WorldState.WorldCountdown != 0)
        {
            return;
        }
        Palette.FadeIn(16, Palette.ScreenFadeCTab);   // fades to black (the source CTab cell is never written); revealed by FadeOut(8) below
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        Palette.FadeOut(8);
        MacToolbox.ShowCursor();
        AlertText.Message = LoadDescriptionText.Load(1900);
        DoSceneTransition.Run(0, 0);
        MacToolbox.HideCursor();
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        Palette.FadeIn(8, Palette.ScreenFadeCTab); // fades to black (the source CTab cell is never written); revealed by FadeOut(64) below
        int loopIndex;
        short shortA;
        for (loopIndex = 0; (shortA = (short)loopIndex) < MissionStateTable.Count; loopIndex += 1)
        {
            if (GameData.MissionStates[shortA].IsActive != 0)
            {
                AbortMission.Run((short)(loopIndex));
                GameData.MissionStates[shortA].IsActive = 0;
            }
        }
        InitGameWorldState.Run(0);
        CleanupSystNpcs.Run(1);
        RunFleetSpawner.Run((int)ship.CurrentSystem);
        for (shortA = 0; shortA < 30; shortA = (short)(shortA + 1))
        {
            TickWorldDailyEvents.Run();
        }
        RecomputeWorldVisibility.Run();
        // Re-christen: the class name + a space + 4 random digits.
        string rechristened = GameData.ShipClasses[0].Name + " ";
        for (shortA = 0; shortA < 4; shortA = (short)(shortA + 1))
        {
            short shortB = (short)(SeedEvoRng.Run(9));
            rechristened += (shortB + 1).ToString();
        }
        PilotIdentity.ShipName = rechristened;
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        RefreshStatusPanel.Run();
        DispatchPendingChatter.Run(0);
        TickSpriteSystem.Run();
        UpdateWindowRegionLayout.Run(true);
        RepaintGameWindow.Run();
        TwoStepRepaintGameWindow.Run();
        WorldState.PlayerShieldBarDirty = 1;
        WorldState.ShieldEnergyBarDirty = 1;
        WorldState.HudWeaponPanelDirty = 1;
        WorldState.SpawnPulseDirty = 1;
        WorldState.WeaponSlotDirty = 1;
        for (shortA = 0; shortA < SystTable.Count; shortA = (short)(shortA + 1))
        {
            if (SystTable.Store[shortA].Govt < 0)
            {
                GalaxyMapGlobals.SetSystemStatus(shortA, 0);
            }
            else
            {
                GalaxyMapGlobals.SetSystemStatus(shortA,
                     GameData.Governments[SystTable.Store[shortA].Govt].InitialRecord);
            }
        }
        if (WorldState.StrictPlay != 0)
        {
            bool found = false;
            for (shortA = 0; shortA < SystRecord.StellarLinkCount; shortA = (short)(shortA + 1))
            {
                if (SystTable.SpobLink(ship.CurrentSystem, shortA) != -1)
                {
                    PilotSave.Run((int)SystTable.SpobLink(ship.CurrentSystem, shortA));
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                PilotSave.Run(0);
            }
        }
        Palette.FadeOut(64);
    }

    // Per-tick scratch state (was the Run() declaration block). One instance per Run() call, so the
    // extracted phase helpers can share it via a single `sc` parameter instead of ~60 ref args.
    private sealed class TickScratch
    {
        public bool skipFlag, found, found2;
        public ushort ushortA, ushortB, ushortScratch12, ushortScratch17;
        public uint uintScratch, uintScratch6;
        public int loopIndex;
        public byte flag;
        public short shortA, shortB, shortC, ushortScratch13;
        public float floatScratch;
        public long longA;
        public double dScratch24, dScratch25, dScratch26, dScratch27, accelArg, speedArg;
        public byte[] eligibleHyperTargetFlags = new byte[17];
        public byte cargoNotRetrievedFlag, spobInhabitedFlag, afterburnerEngagedFlag, spobStationFlag, clearOfStellarsFlag, windupSettledFlag;
        public short escortCommand, local_3e4, destSystemIndex, dockingProximityThreshold, local_3de, targetMissionIndex, fuelMax;
        public int local_3cc, local_3c8, local_3bc, local_3b8;
        public float local_3c4, local_3c0;
    }

    private static void TickBoardingAlarmAudio(ShipRec ship, TickScratch sc)
    {
        sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x49));
        if (sc.shortA != 0 && ((short)(CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd))) != 0)
        {
            FlushMixQueueEntries.Run(SoundResourceCells.BoardingChimeSnd);
            TriggerBoardingAlarmOnce.Run(SoundCompletionKind.VoiceCompleted);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action40)));
        if (sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x65))) != 0)
        {
            PlayerKeyLatches.FreeMemoryBytes = MacToolbox.MaxMem(); // grow out-param never read
            string statsMsg = PlayerKeyLatches.FreeMemoryBytes + " bytes free, gSpeedMult = "
                + (int)(100.0 * WorldState.TimeScale) + "%, "
                + (int)WorldState.InstallDays + " days played";
            EnqueueChatterEvent.Run(statsMsg, 60, 0, 12, UiColors.ChatterText, 0, 0);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action33)));
        if (sc.shortA == 0 && ((short)(Keymap.TestCachedKeymapBit(0x69))) == 0)
        {
            PlayerKeyLatches.TargetSpecialKeyLatch = false;
        }
        else if (!PlayerKeyLatches.TargetSpecialKeyLatch)
        {
            PlayerKeyLatches.TargetSpecialKeyLatch = true;
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
            if (sc.shortA == 0)
            {
                sc.ushortScratch13 = (short)(RollNpcArrival.Run((int)ship.CurrentSystem));
                ship.TargetSlot = (short)sc.ushortScratch13;
            }
            else
            {
                SpawnPers.Run((int)GameData.Player.CurrentSystem, 1, 44);
            }
            WorldState.WeaponSlotDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action34)));
        if ((sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x6a))) != 0) &&
           (ship.TargetSlot != -1))
        {
            GameData.Ships[ship.TargetSlot].IsActive = 0;
            WorldState.WeaponSlotDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action36)));
        if ((sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x6c))) != 0) &&
           ((ship.TargetSlot != -1 &&
            (-(int)GameData.ShipClasses[GameData.Ships[ship.TargetSlot].ShipClass].BaseArmor <
             (int)GameData.Ships[ship.TargetSlot].Shield))))
        {
            GameData.Ships[ship.TargetSlot].Shield = (float)(-(int)GameData.ShipClasses[GameData.Ships[ship.TargetSlot].ShipClass].BaseArmor);
            WorldState.WeaponSlotDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action37)));
        if ((sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x6d))) != 0) &&
           ((ship.TargetSlot != -1 &&
            (-(int)GameData.ShipClasses[GameData.Ships[ship.TargetSlot].ShipClass].BaseArmor <=
             (int)GameData.Ships[ship.TargetSlot].Shield))))
        {
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x30));
            if (sc.shortA != 0)
            {
                FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem,
                              GameData.Ships[ship.TargetSlot].Govt, 3,
                              GameData.Ships[ship.TargetSlot].GrudgeMissionIndex);
                if ((0 < WorldState.PlayerCombatRating +
                          (int)GameData.ShipClasses[ship.ShipClass].Crew) &&
                   (WorldState.PlayerCombatRating +
                    (int)GameData.ShipClasses[ship.ShipClass].Crew < 32000))
                {
                    // (Faithful quirk: the range check uses the PLAYER's crew, the
                    // increment uses the TARGET's crew.)
                    WorldState.PlayerCombatRating +=
                         (int)GameData.ShipClasses[GameData.Ships[ship.TargetSlot].ShipClass].Crew;
                }
            }
            GameData.Ships[ship.TargetSlot].Shield = (float)(-(GameData.ShipClasses[GameData.Ships[ship.TargetSlot].ShipClass].BaseArmor + 1));
            WorldState.WeaponSlotDirty = 1;
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action41)));
        if (sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x10))) != 0)
        {
            // Keymap.Slot() is already EVO-keycode-space — the `int` overload (no further ^8)
            // matches TestCachedKeymapBit's cast two lines up; the MacKeycode overload would
            // double-XOR it and this loop would read "released" on its first spin.
            do
            {
                sc.shortA = (short)(Keymap.TestLiveKeymapBit((int)Keymap.Slot(KeyAction.Action41)));
            } while (sc.shortA != 0);
            ship.ShipClass = (short)(ship.ShipClass + 1);
            if (ship.ShipClass > ShipClassTable.Count - 1)
            {
                ship.ShipClass = 0;
            }
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            sc.floatScratch = (float)ShipDerivedStats.EffectiveShieldMax(ship);
            ship.Shield = sc.floatScratch;
            ship.Fuel = (float)(int)GameData.ShipClasses[ship.ShipClass].BaseFuel;
            WorldState.HudStatusPanelDirty = 1;
            WorldState.HudWeaponPanelDirty = 1;
            WorldState.PlayerShieldBarDirty = 1;
            WorldState.ShieldEnergyBarDirty = 1;
            for (sc.shortA = 0; sc.shortA < OwnedOutfitGrid.Count; sc.shortA = (short)(sc.shortA + 1))
            {
                OwnedOutfitGrid.Store[sc.shortA] = 0;
            }
            for (sc.shortA = 0; sc.shortA < ShipRecord.WeaponSlotCount; sc.shortA = (short)(sc.shortA + 1))
            {
                ship.WeaponSlotType[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponType[sc.shortA];
                ship.WeaponSlotAmmo[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponAmmo[sc.shortA];
            }
            ship.SelectedWeaponSlot = -1;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action42)));
        if (sc.shortA != 0 || ((short)(Keymap.TestCachedKeymapBit(0x13))) != 0)
        {
            // Same keycode-space note as the Action41 block above.
            do
            {
                sc.shortA = (short)(Keymap.TestLiveKeymapBit((int)Keymap.Slot(KeyAction.Action42)));
            } while (sc.shortA != 0);
            ship.ShipClass = (short)(ship.ShipClass + -1);
            if (ship.ShipClass < 0)
            {
                ship.ShipClass = ShipClassTable.Count - 1;
            }
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            sc.floatScratch = (float)ShipDerivedStats.EffectiveShieldMax(ship);
            ship.Shield = sc.floatScratch;
            ship.Fuel = (float)(int)GameData.ShipClasses[ship.ShipClass].BaseFuel;
            WorldState.HudStatusPanelDirty = 1;
            WorldState.HudWeaponPanelDirty = 1;
            WorldState.PlayerShieldBarDirty = 1;
            WorldState.ShieldEnergyBarDirty = 1;
            for (sc.shortA = 0; sc.shortA < OwnedOutfitGrid.Count; sc.shortA = (short)(sc.shortA + 1))
            {
                OwnedOutfitGrid.Store[sc.shortA] = 0;
            }
            for (sc.shortA = 0; sc.shortA < ShipRecord.WeaponSlotCount; sc.shortA = (short)(sc.shortA + 1))
            {
                ship.WeaponSlotType[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponType[sc.shortA];
                ship.WeaponSlotAmmo[sc.shortA] = GameData.ShipClasses[ship.ShipClass].DefaultWeaponAmmo[sc.shortA];
            }
            ship.SelectedWeaponSlot = -1;
        }
        sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x30));
        if (sc.shortA != 0 && ((short)(Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action39)))) != 0)
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            sc.shortA = (short)(Keymap.TestCachedKeymapBit(0x32));
            if (sc.shortA == 0)
            {
                ship.Credits += 10000;
            }
            else if (ship.Credits < 1000)
            {
                ship.Credits = 0;
            }
            else
            {
                ship.Credits -= 1000;
            }
            WorldState.HudStatusPanelDirty = 1;
        }
    }

    private static void UpdateWindupState(ShipRec ship)
    {
        if (ship.JumpWindupTimer == -999)
        {
            ship.JumpWindupTimer = 0;
        }
    }

    private static void ContinueLandingApproach(ShipRec ship, TickScratch sc)
    {
        if (ship.CurrentSystem == GameData.Spobs[WorldState.LandingTargetSpob].System)
        {
            if (WorldState.LandingApproachState > -1)
            {
                sc.spobInhabitedFlag = ((SpobFlags)GameData.Spobs[WorldState.LandingTargetSpob].Flags & SpobFlags.Uninhabited) == 0 ? (byte)1 : (byte)0;
                sc.found = false;
                if (GameData.Spobs[WorldState.LandingTargetSpob].Govt == -1)
                {
                    sc.found = true;
                }
                else if (GameData.Spobs[WorldState.LandingTargetSpob].Govt != -1 && (GameData.Spobs[WorldState.LandingTargetSpob].MinCoolness <= GalaxyMapGlobals.SystemStatus(ship.CurrentSystem)))
                {
                    sc.found = true;
                }
                if (GameData.Spobs[WorldState.LandingTargetSpob].TradingEnabled != 0)
                {
                    sc.found = true;
                }
                if (WorldState.LandingApproachState > 749)
                {
                    sc.found = true;
                }
                if (!sc.found)
                {
                    for (sc.shortA = 0; sc.shortA < MissionStateTable.Count; sc.shortA = (short)(sc.shortA + 1))
                    {
                        if (GameData.MissionStates[sc.shortA].IsActive != 0)
                        {
                            if (GameData.Ships[0].NavTargetSpob == GameData.Missions[sc.shortA].TargetSpob)
                            {
                                sc.found = true;
                            }
                            if (GameData.Ships[0].NavTargetSpob == GameData.Missions[sc.shortA].ReturnSpob)
                            {
                                sc.found = true;
                            }
                        }
                    }
                }
                if (sc.spobInhabitedFlag != 0 && (sc.found))
                {
                    if (WorldState.LandingApproachState > 748)
                    {
                        WorldState.LandingApproachState = (short)(WorldState.LandingApproachState + 1);
                    }
                    sc.loopIndex = (int)(ship.PosX - (float)(int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].XPos);
                    sc.ushortScratch12 = (ushort)sc.loopIndex;
                    sc.ushortA = (ushort)((short)sc.ushortScratch12 >> 0xf);
                    sc.loopIndex = (int)(ship.PosY - (float)(int)GameData.Spobs[GameData.Ships[0].NavTargetSpob].YPos);
                    sc.ushortScratch17 = (ushort)sc.loopIndex;
                    sc.ushortB = (ushort)((short)sc.ushortScratch17 >> 0xf);
                    sc.local_3de = (short)((sc.ushortB ^ sc.ushortScratch17) - sc.ushortB);
                    if ((((short)((sc.ushortA ^ sc.ushortScratch12) - sc.ushortA) < 250) && sc.local_3de < 250) && WorldState.LandingApproachState < 750)
                    {
                        WorldState.LandingApproachState = 750;
                    }
                    if (WorldState.LandingApproachState == 750)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                        string clearedMsg;
                        if (((SpobFlags)GameData.Spobs[GameData.Ships[0].NavTargetSpob].Flags & SpobFlags.Station) == 0)
                        {
                            sc.shortA = (short)(SeedEvoRng.Run(3));
                            if (sc.shortA == 0)
                            {
                                clearedMsg = "Cleared to land, " + Trunc(PilotIdentity.ShipName, 63) + ". ";
                            }
                            else if (sc.shortA == 1)
                            {
                                clearedMsg = Trunc(PilotIdentity.ShipName, 63) + ", you’re cleared to land. ";
                            }
                            else
                            {
                                clearedMsg = "You are cleared to land. ";
                            }
                        }
                        else
                        {
                            sc.shortA = (short)(SeedEvoRng.Run(3));
                            if (sc.shortA == 0)
                            {
                                clearedMsg = "Cleared to dock, " + Trunc(PilotIdentity.ShipName, 63) + ". ";
                            }
                            else if (sc.shortA == 1)
                            {
                                clearedMsg = Trunc(PilotIdentity.ShipName, 63) + ", you’re cleared to dock. ";
                            }
                            else
                            {
                                clearedMsg = "You are cleared to dock. ";
                            }
                        }
                        sc.shortA = (short)(SeedEvoRng.Run(2));
                        if (sc.shortA == 0)
                        {
                            clearedMsg += "Commence final approach.";
                        }
                        else
                        {
                            clearedMsg += "Welcome to " + Trunc(GameData.Spobs[GameData.Ships[0].NavTargetSpob].Name, 31) + ". ";
                        }
                        EnqueueChatterEvent.Run(clearedMsg, 250, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                    if (WorldState.LandingApproachState > 2047)
                    {
                        WorldState.LandingApproachState = -1;
                        WorldState.LandingTargetSpob = -1;
                    }
                }
                else if (sc.found)
                {
                    if (WorldState.LandingApproachState > 748)
                    {
                        WorldState.LandingApproachState = (short)(WorldState.LandingApproachState + 1);
                    }
                    sc.loopIndex = (int)(ship.PosX - (float)(int)GameData.Spobs[WorldState.LandingTargetSpob].XPos);
                    sc.ushortScratch12 = (ushort)sc.loopIndex;
                    sc.ushortB = (ushort)((short)sc.ushortScratch12 >> 0xf);
                    sc.loopIndex = (int)(ship.PosY - (float)(int)GameData.Spobs[WorldState.LandingTargetSpob].YPos);
                    sc.ushortScratch17 = (ushort)sc.loopIndex;
                    sc.ushortA = (ushort)((short)sc.ushortScratch17 >> 0xf);
                    sc.local_3de = (short)((sc.ushortA ^ sc.ushortScratch17) - sc.ushortA);
                    if ((((short)((sc.ushortB ^ sc.ushortScratch12) - sc.ushortB) < 250) && sc.local_3de < 250) && WorldState.LandingApproachState < 750)
                    {
                        WorldState.LandingApproachState = 750;
                    }
                    if (WorldState.LandingApproachState == 750 && (PlayerKeyLatches.LandKeyLatch))
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
                        EnqueueChatterEvent.Run("No response.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                    if (WorldState.LandingApproachState > 2047)
                    {
                        WorldState.LandingApproachState = -1;
                        WorldState.LandingTargetSpob = -1;
                    }
                }
            }
        }
        else
        {
            WorldState.LandingTargetSpob = -1;
            WorldState.LandingApproachState = -1;
        }
    }

    // The locked target can be boarded: an unclaimed, disabled, in-system, non-stellar
    // (PersIndex != KamikazePersIndex sentinel) NPC, and the player ship itself is still alive.
    private static bool TargetIsBoardable(ShipRec ship)
    {
        var target = ShipTable.Ships[ship.TargetSlot];
        return target.SalvageClaimed == 0 && ShipDerivedStats.IsDisabled(target) &&
               target.IsActive != 0 && ship.CurrentSystem == target.CurrentSystem &&
               target.PersIndex != ShipRecord.KamikazePersIndex && !ShipDerivedStats.IsDyingOrDestroyed(ship);
    }

    private static void HandleBoardDisabledTarget(ShipRec ship, TickScratch sc)
    {
        PlayerKeyLatches.BoardKeyLatch = true;
        sc.cargoNotRetrievedFlag = 0;
        if (TargetIsBoardable(ship))
        {
            sc.dScratch24 = (double)EvMath.FloatAbs((double)(GameData.Ships[ship.TargetSlot].VelX - ship.VelX));
            if (sc.dScratch24 > 0.5 || (double)EvMath.FloatAbs((double)(GameData.Ships[ship.TargetSlot].VelY - ship.VelY)) > 0.5)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                EnqueueChatterEvent.Run("You’re moving too fast to board this ship.", 360, 0, 12, UiColors.ChatterText, 0, 0);
            }
            else
            {
                sc.shortA = (short)(MacRectWidth.Run(WeaponGraphicsTable.Store[GameData.Ships[ship.TargetSlot].ShipClass * 36 + (int)GameData.Ships[ship.TargetSlot].Heading / 10]));
                sc.dScratch25 = (double)(int)sc.shortA;
                sc.dScratch24 = 0.5;
                sc.dScratch26 = (double)EvMath.FloatAbs((double)(GameData.Ships[ship.TargetSlot].PosX - ship.PosX));
                if (sc.dScratch26 <= (double)(float)(sc.dScratch24 * sc.dScratch25))
                {
                    sc.shortA = (short)(MacRectHeight.Run(WeaponGraphicsTable.Store[GameData.Ships[ship.TargetSlot].ShipClass * 36 + (int)GameData.Ships[ship.TargetSlot].Heading / 10]));
                    sc.dScratch24 = (double)(int)sc.shortA;
                    sc.dScratch26 = 0.5;
                    sc.dScratch25 = (double)EvMath.FloatAbs((double)(GameData.Ships[ship.TargetSlot].PosY - ship.PosY));
                    if (sc.dScratch25 <= (double)(float)(sc.dScratch26 * sc.dScratch24))
                    {
                        sc.uintScratch = (uint)((int)ship.Heading - (int)GameData.Ships[ship.TargetSlot].Heading);
                        sc.uintScratch6 = (uint)((int)sc.uintScratch >> 0x1f);
                        bool headingAligned = (int)((sc.uintScratch6 ^ sc.uintScratch) - sc.uintScratch6) < 31;
                        if (!headingAligned)
                        {
                            // Minuend must be ship.Heading, not the target's (decompile line 17830) —
                            // swapping it makes this ~180-degree-opposite alignment fallback never fire.
                            sc.uintScratch6 = (uint)((int)ship.Heading -
                                     (GameData.Ships[ship.TargetSlot].Heading + 180) % 360);
                            sc.uintScratch = (uint)((int)sc.uintScratch6 >> 0x1f);
                            headingAligned = (int)((sc.uintScratch ^ sc.uintScratch6) - sc.uintScratch) < 31;
                        }
                        if (headingAligned)
                        {
                            if (GameData.Ships[ship.TargetSlot].GrudgeMissionIndex == -1)
                            {
                                sc.flag = 1;
                            }
                            else
                            {
                                sc.targetMissionIndex = GameData.Ships[ship.TargetSlot].GrudgeMissionIndex;
                                if (GameData.Missions[sc.targetMissionIndex].PickupMode == MissionCargoPickupMode.WhenBoardingSpecialShip &&
                                   (GameData.MissionStates[sc.targetMissionIndex].IsActive != 0))
                                {
                                    MacToolbox.ShowCursor();
                                    sc.flag = (byte)(ValidateMissionCargoSpace.Run(GameData.Missions[sc.targetMissionIndex].CargoStringIndex,
                                                          GameData.Missions[sc.targetMissionIndex].CargoMass) ? 1 : 0);
                                    MacToolbox.HideCursor();
                                    if (sc.flag == 0)
                                    {
                                        sc.cargoNotRetrievedFlag = 1;
                                    }
                                    else
                                    {
                                        if (GameData.Ships[ship.TargetSlot].GrudgeMissionIndex != -1 &&
                                            GameData.MissionStates[GameData.Ships[ship.TargetSlot].GrudgeMissionIndex].IsActive != 0)
                                        {
                                            var grudgeMission = GameData.Missions[GameData.Ships[ship.TargetSlot].GrudgeMissionIndex];
                                            grudgeMission.BoardedShipCount = (short)(grudgeMission.BoardedShipCount + 1);
                                        }
                                        GameData.Ships[ship.TargetSlot].SalvageClaimed = 1;
                                        GameData.Missions[sc.targetMissionIndex].CargoPickedUp = 1;
                                        WorldState.HudStatusPanelDirty = 1;
                                        string retrievedMsg = "You retreived the "
                                            + ResourceGlobals.NamesStr0fa1[GameData.Missions[sc.targetMissionIndex].CargoStringIndex]
                                            + " from this ship.";
                                        EnqueueChatterEvent.Run(retrievedMsg, 250, 0, 12, UiColors.ChatterText, 0, 0);
                                        SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 8, 128, 128);
                                    }
                                }
                                else
                                {
                                    sc.flag = 1;
                                }
                            }
                            if (sc.flag == 0)
                            {
                                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                                EnqueueChatterEvent.Run("You can’t board this ship.", 360, 0, 12, UiColors.ChatterText, 0, 0);
                            }
                            else
                            {
                                if (sc.cargoNotRetrievedFlag == 0)
                                {
                                    GameData.Ships[ship.TargetSlot].SalvageClaimed = 1;
                                }
                                ship.VelX = GameData.Ships[ship.TargetSlot].VelX;
                                ship.VelY = GameData.Ships[ship.TargetSlot].VelY;
                                sc.local_3de = 0;
                                if (GameData.Ships[ship.TargetSlot].GrudgeMissionIndex == -1)
                                {
                                    sc.local_3de = 1;
                                }
                                else if (GameData.MissionStates[GameData.Ships[ship.TargetSlot].GrudgeMissionIndex].IsActive != 0 &&
                                        (GameData.MissionStates[GameData.Ships[ship.TargetSlot].GrudgeMissionIndex].Failed != 0))
                                {
                                    sc.local_3de = 1;
                                }
                                if (sc.local_3de == 1 &&
                                    GameData.Ships[ship.TargetSlot].PersIndex != ShipRecord.KamikazePersIndex &&
                                    GameData.Ships[ship.TargetSlot].PersIndex != ShipRecord.EngagePlayerPersIndex)
                                {
                                    FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem,
                                                 GameData.Ships[ship.TargetSlot].Govt, 2,
                                                 GameData.Ships[ship.TargetSlot].GrudgeMissionIndex);
                                    if (GameData.Ships[ship.TargetSlot].PersIndex == -1)
                                    {
                                        sc.flag = (byte)(CanSpawnAnotherSubMunition.Run(GameData.Ships[ship.TargetSlot].ShipClass,
                                                              (short)-1, (short)-1) ? 1 : 0);
                                        if (sc.flag == 0)
                                        {
                                            SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                            ShowBoardingDialog.Run();
                                        }
                                        else
                                        {
                                            EnqueueChatterEvent.Run("You added this ship to your fighter bay.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                                            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 8, 128, 128);
                                            GameData.Ships[ship.TargetSlot].AiBehaviorType = ShipAiType.NavalFighter;
                                            GameData.Ships[ship.TargetSlot].OwnerSlot = 0;
                                            GameData.Ships[ship.TargetSlot].Shield = 0f;
                                            GameData.Ships[ship.TargetSlot].NavTargetSpob = -1;
                                            ShipAi.SetStateLeavingFollowSelf(ShipTable.Ships[ship.TargetSlot]);
                                        }
                                    }
                                    else if (GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].LinkMission == -1)
                                    {
                                        sc.flag = (byte)(CanSpawnAnotherSubMunition.Run(GameData.Ships[ship.TargetSlot].ShipClass,
                                                              (short)-1, (short)-1) ? 1 : 0);
                                        if (sc.flag == 0)
                                        {
                                            SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                            ShowBoardingDialog.Run();
                                        }
                                        else
                                        {
                                            EnqueueChatterEvent.Run("You added this ship to your fighter bay.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                                            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 8, 128, 128);
                                            GameData.Ships[ship.TargetSlot].AiBehaviorType = ShipAiType.NavalFighter;
                                            GameData.Ships[ship.TargetSlot].OwnerSlot = 0;
                                            GameData.Ships[ship.TargetSlot].Shield = 0f;
                                            GameData.Ships[ship.TargetSlot].NavTargetSpob = -1;
                                            ShipAi.SetStateLeavingFollowSelf(ShipTable.Ships[ship.TargetSlot]);
                                        }
                                    }
                                    // Bit 0x200 is not yet in the PersFlags registry (OpenEV.Platform.EvoData/Resources/Flags/PersFlags.cs) —
                                    // out of this file's scope to add; left as a raw mask pending that follow-up.
                                    else if (((uint)GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].Flags & 0x200) == 0)
                                    {
                                        sc.flag = (byte)(CanSpawnAnotherSubMunition.Run(GameData.Ships[ship.TargetSlot].ShipClass,
                                                              (short)-1, (short)-1) ? 1 : 0);
                                        if (sc.flag == 0)
                                        {
                                            SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                            ShowBoardingDialog.Run();
                                        }
                                        else
                                        {
                                            EnqueueChatterEvent.Run("You added this ship to your fighter bay.", 250, 0, 12, UiColors.ChatterText, 0, 0);
                                            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 8, 128, 128);
                                            GameData.Ships[ship.TargetSlot].AiBehaviorType = ShipAiType.NavalFighter;
                                            GameData.Ships[ship.TargetSlot].OwnerSlot = 0;
                                            GameData.Ships[ship.TargetSlot].Shield = 0f;
                                            GameData.Ships[ship.TargetSlot].NavTargetSpob = -1;
                                            ShipAi.SetStateLeavingFollowSelf(ShipTable.Ships[ship.TargetSlot]);
                                        }
                                    }
                                    else
                                    {
                                        RenderGlobals.DrawGateFlag = 1;
                                        WorldState.CurrentTargetShipId = ship.TargetSlot;
                                        sc.flag = (byte)(IsBarPersEligible.Run((short)GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].LinkMission) ? 1 : 0);
                                        if (sc.flag == 0)
                                        {
                                            SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                            ShowBoardingDialog.Run();
                                        }
                                        else
                                        {
                                            if (WorldState.IsCursorHiddenByGame)
                                            {
                                                MacToolbox.ShowCursor();
                                            }
                                            SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                            RunSingleMissionDialog.Run((int)GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].LinkMission);
                                            if (WorldState.IsCursorHiddenByGame)
                                            {
                                                MacToolbox.HideCursor();
                                            }
                                            SetGamePortAndDevice.Run();
                                            MacToolbox.ForeColor(QuickDrawColor.Black);
                                            MacToolbox.PaintRect(new[] {
                                                GlobalState.PortTop, GlobalState.PortLeft,
                                                GlobalState.PortBottom,
                                                (short)(GlobalState.PortRight - 144) });
                                            DispatchPendingChatter.Run(0);
                                            if (((PersFlags)GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].Flags & PersFlags.DeactivateAfterMission) != 0)
                                            {
                                                GameData.Pers[GameData.Ships[ship.TargetSlot].PersIndex].AvailableFlag = 0;
                                            }
                                        }
                                        RenderGlobals.DrawGateFlag = 0;
                                        WorldState.CurrentTargetShipId = -1;
                                    }
                                    CallForGovtDefenders.Run(ShipTable.Ships[ship.TargetSlot].Ptr, ship.Ptr);
                                }
                                else if (GameData.Ships[ship.TargetSlot].GrudgeMissionIndex == -1)
                                {
                                    SndPlay.Run(SoundResourceCells.BoardingDialogChimeSnd, 8, 128, 128);
                                    ShowBoardingDialog.Run();
                                }
                            }
                        }
                        return;  // boarding handled (or refused) above — skip the "not close enough" message below
                    }
                }
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                EnqueueChatterEvent.Run("You’re not close enough to board this ship.", 360, 0, 12, UiColors.ChatterText, 0, 0);
            }
        }
        else
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
            EnqueueChatterEvent.Run("You can’t board this ship.", 360, 0, 12, UiColors.ChatterText, 0, 0);
        }
    }
}
