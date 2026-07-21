using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

public static class PlayerHailAction
{
    public static void Run()
    {
        // Port of FUN_1002ef00 (EV Override-11.c lines 19425-19614).
        // (The decompile's ppuVar5 TOC-register reloads after the glue calls are dropped;
        // its only real consumer was the portRect read below, kept via GlobalState.)

        if (!WorldState.IsCloaked &&
            !ShipDerivedStats.IsDisabled(ShipTable.Ships[0]) &&
            !ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[0]) &&
            GameData.Ships[0].JumpWindupTimer < 1)
        {
            if (GameData.Ships[0].TargetSlot == -1 || Keymap.TestCachedKeymapBit(0x32) != 0)
            {
                if (GameData.Ships[0].NavMode == 2 && GameData.Ships[0].NavTargetSpob != -1)
                {
                    if (GameData.Spobs[GameData.Ships[0].NavTargetSpob].Visible == 0 ||
                        ((SpobFlags)GameData.Spobs[GameData.Ships[0].NavTargetSpob].Flags & SpobFlags.Landable) == 0 ||
                        ((SpobFlags)GameData.Spobs[GameData.Ships[0].NavTargetSpob].Flags & SpobFlags.Uninhabited) != 0)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 5, 128, 128);
                        EnqueueChatterEvent.Run("No response.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                    else
                    {
                        // FUN_10010f70 never reads its incoming r3 (clobbered in its own
                        // prologue before use, then it independently re-fetches NavTargetSpob
                        // from the global itself) — the caller's argument is real but dead at
                        // the callee, so calling with no args here is faithful.
                        ShowSpobHailDialog.Run();
                    }
                }
                else
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 5, 128, 128);
                }
            }
            else if (GameData.Ships[ShipTable.Ships[0].TargetSlot].JumpWindupTimer < 1)
            {
                bool canBoard = true;
                if (GameData.Ships[ShipTable.Ships[0].TargetSlot].Govt != -1 &&
                    (GameData.Governments[GameData.Ships[ShipTable.Ships[0].TargetSlot].Govt].Flags &
                     GovtFlags.NoHail) != 0)
                {
                    canBoard = false;
                }
                if (GameData.Ships[ShipTable.Ships[0].TargetSlot].ShipClass == ShipRecord.EmptyShipClass)
                {
                    canBoard = false;
                }
                if (ShipDerivedStats.IsDisabled(ShipTable.Ships[GameData.Ships[0].TargetSlot]))
                {
                    canBoard = false;
                }
                if (GameData.Ships[ShipTable.Ships[0].TargetSlot].PersIndex == ShipRecord.KamikazePersIndex)
                {
                    canBoard = false;
                }
                if (canBoard)
                {
                    if (GameData.Ships[ShipTable.Ships[0].TargetSlot].PersIndex == -1)
                    {
                        SpaceportPersonDialog.Run(GameData.Ships[0].TargetSlot);
                    }
                    else if (GameData.Pers[GameData.Ships[ShipTable.Ships[0].TargetSlot].PersIndex].LinkMission == -1)
                    {
                        SpaceportPersonDialog.Run(GameData.Ships[0].TargetSlot);
                    }
                    // Bit 0x200 has no PersFlags member yet (enum jumps 0x0100 -> 0x0400);
                    // left as a raw mask, matching TickShipAI's equivalent site.
                    else if ((GameData.Pers[GameData.Ships[ShipTable.Ships[0].TargetSlot].PersIndex].Flags & 0x200) == 0)
                    {
                        // Safe to cache: PersIndex (and this pers record) don't change again
                        // until TargetSlot itself is reassigned below, after all uses here.
                        var pers = GameData.Pers[GameData.Ships[ShipTable.Ships[0].TargetSlot].PersIndex];
                        RenderGlobals.DrawGateFlag = 1;
                        WorldState.CurrentTargetShipId = GameData.Ships[0].TargetSlot;
                        var barPersEligible = IsBarPersEligible.Run(pers.LinkMission);
                        if (!barPersEligible)
                        {
                            SpaceportPersonDialog.Run(GameData.Ships[0].TargetSlot);
                        }
                        else
                        {
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                            if (WorldState.IsCursorHiddenByGame)
                            {
                                MacToolbox.ShowCursor();
                            }
                            var missionAccepted = RunSingleMissionDialog.Run(pers.LinkMission);
                            if (WorldState.IsCursorHiddenByGame)
                            {
                                // Decompile checks a WRONG-TOC ppuVar5 alias here; same
                                // cursor-hidden flag as the ShowCursor check above.
                                MacToolbox.HideCursor();
                            }
                            SetGamePortAndDevice.Run();
                            MacToolbox.ForeColor(QuickDrawColor.Black);
                            MacToolbox.PaintRect(new[] {
                                GlobalState.PortTop, GlobalState.PortLeft,
                                GlobalState.PortBottom,
                                (short)(GlobalState.PortRight - 144) });
                            DispatchPendingChatter.Run(0);
                            if (missionAccepted != 0)
                            {
                                if (((PersFlags)pers.Flags & PersFlags.DeactivateAfterMission) != 0)
                                {
                                    pers.AvailableFlag = 0;
                                }
                                if (((PersFlags)pers.Flags & PersFlags.LeaveAfterMissionAccept) != 0)
                                {
                                    ShipAi.SetStateInert(ShipTable.Ships[GameData.Ships[0].TargetSlot]);
                                }
                                // Find the active mission slot whose def matches this pers's
                                // LinkMission (-1 if none).
                                int escortSlot = -1;
                                for (int loopIndex = 0; loopIndex < MissionStateTable.Count; loopIndex++)
                                {
                                    if (GameData.MissionStates[loopIndex].IsActive != 0 &&
                                        GameData.Missions[loopIndex].MissionDefIndex == pers.LinkMission)
                                    {
                                        escortSlot = loopIndex;
                                        break;
                                    }
                                }
                                short shortResult = (short)escortSlot;
                                if (-1 < shortResult &&
                                     ((PersFlags)pers.Flags & PersFlags.ReplaceShipOnMissionAccept) != 0 &&
                                     GameData.Missions[shortResult].SpawnCount == 1 &&
                                     (shortResult = (short)SpawnMissionNpc.Run(GameData.Missions[shortResult].ShipToBoardOrScan,
                                                        GameData.Ships[0].CurrentSystem, (short)escortSlot)) != -1
                                     )
                                {
                                    GameData.Ships[shortResult].PosX = GameData.Ships[GameData.Ships[0].TargetSlot].PosX;
                                    GameData.Ships[shortResult].PosY = GameData.Ships[GameData.Ships[0].TargetSlot].PosY;
                                    GameData.Ships[shortResult].VelX = GameData.Ships[GameData.Ships[0].TargetSlot].VelX;
                                    GameData.Ships[shortResult].VelY = GameData.Ships[GameData.Ships[0].TargetSlot].VelY;
                                    GameData.Ships[shortResult].Heading = GameData.Ships[GameData.Ships[0].TargetSlot].Heading;
                                    ShipAi.SetStateHyperWindupAndPropagate(ShipTable.Ships[shortResult]);
                                    GameData.Ships[GameData.Ships[0].TargetSlot].IsActive = 0;
                                    GameData.Ships[0].TargetSlot = shortResult;
                                    WorldState.WeaponSlotDirty = 1;
                                }
                            }
                        }
                        RenderGlobals.DrawGateFlag = 0;
                        WorldState.CurrentTargetShipId = GameData.Ships[0].TargetSlot;
                    }
                    else
                    {
                        SpaceportPersonDialog.Run(GameData.Ships[0].TargetSlot);
                    }
                    RefreshStatusPanel.Run();
                    DispatchPendingChatter.Run(0);
                }
                else
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 5, 128, 128);
                    EnqueueChatterEvent.Run("No response.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                }
            }
            else
            {
                if (!ShipDerivedStats.IsDisabled(ShipTable.Ships[GameData.Ships[0].TargetSlot]))
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    EnqueueChatterEvent.Run("Unable to send hail - target ship is entering hyperspace.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                }
                else
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 5, 128, 128);
                    EnqueueChatterEvent.Run("No response.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                }
            }
        }
        else
        {
            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
        }
    }
}
