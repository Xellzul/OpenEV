using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_1001a868 (EV Override-11.c lines 12068-12316): the pilot-file
// writer — serialize the main pilot record (OpïL 0x80, "Pilot Data") and the
// aux/galaxy block (OpïL 0x81, named after the player ship) into the pilot's
// resource fork, then write the "Last Pilot" alias file.
//
// The two blocks are managed PilotData byte[]s; AddResource gets them via
// NewHandleFromBytes, which aliases the block bytes — so the field writes through
// rec/aux below are what AddResource emits.
public static class SavePilotFile
{
    public static int Run(int dockedSpobIndex)
    {
        var fsSpec = new MacToolbox.FsSpec();
        int result = dockedSpobIndex;   // guard-set (bit 0xd) skips the body below, returns the untouched param

        if (!BugBits.IsStoredSet(BugBit.SavePilotFileGuard))   // 'ëbug' bit 0xd — save guard
        {
            short savedCurResFile = (short)MacToolbox.CurResFile();
            MacToolbox.FSMakeFSSpec((int)PrefsFolderLocation.VRefNum,
                                    PrefsFolderLocation.DirID, PilotIdentity.Name, fsSpec);
            MacToolbox.FSpCreateResFile(fsSpec, MacFileType.EvoCreator, (int)MacResType.PilotRecord, 0);
            PilotIdentity.FileRefNum = (short)MacToolbox.FSpOpenResFile(fsSpec, 3);
            if (PilotIdentity.FileRefNum == -1)
            {
                result = -43;   // fnfErr — the resource-file open returned ref -1
            }
            else
            {
                var rec = PilotData.Record;
                var aux = PilotData.Aux;
                int recordHandle = MacToolbox.NewHandleFromBytes(PilotData.RecordBlock.Data);
                int auxHandle = MacToolbox.NewHandleFromBytes(PilotData.AuxBlock.Data);
                if (recordHandle != 0 && auxHandle != 0)
                {
                    MacToolbox.HLock(recordHandle);
                    MacToolbox.HLock(auxHandle);
                    MacToolbox.HNoPurge(recordHandle);
                    MacToolbox.HNoPurge(auxHandle);
                    rec.DockedSpobIndex = (short)dockedSpobIndex;
                    rec.ShipClass = GameData.Player.ShipClass;
                    for (short i = 0; i < PilotRec.ShipSlotCount; i++)
                    {
                        rec.SetShipSlot(i, GameData.Player.CargoHold[i]);
                    }
                    // The original keeps the +0x68 shield cell int-valued (a real new-pilot
                    // save = 180, the shuttle shield max), so it saves (short)(int), NOT the
                    // float's bit pattern. The current-format loader recomputes shield from
                    // EffectiveShieldMax and ignores this; the legacy importer restores it.
                    rec.Shield = (short)(int)GameData.Player.Shield;
                    rec.Fuel = (short)(int)GameData.Player.Fuel;
                    rec.DateMonth = GameDate.Current.Month;
                    rec.DateDay = GameDate.Current.Day;
                    rec.DateYear = GameDate.Current.Year;
                    for (short i = 0; i < PilotRec.SystStateCount; i++)
                    {
                        rec.SetSystState(i, (short)SystTable.Store[i].Visited);
                    }
                    for (short i = 0; i < PilotRec.OwnedOutfitCount; i++)
                    {
                        rec.SetOwnedOutfit(i, OwnedOutfitGrid.Store[i]);
                    }
                    for (short i = 0; i < PilotRec.KillsBySystCount; i++)
                    {
                        rec.SetKillsBySyst(i, GalaxyMapGlobals.SystemStatus(i));
                    }
                    for (short i = 0; i < PilotRec.WeaponTypeCount; i++)
                    {
                        rec.SetWeaponType(i, GameData.Player.WeaponSlotType[i]);
                        rec.SetWeaponAmmo(i, GameData.Player.WeaponSlotAmmo[i]);
                    }
                    rec.Credits = GameData.Player.Credits;
                    for (short i = 0; i < PilotRec.MissionStatesCount; i++)
                    {
                        GameData.MissionStates[i].WriteTo(rec.Block, rec.MissionStateOffset(i));
                        GameData.Missions[i].WriteTo(rec.Block, rec.MissionRecordOffset(i));
                    }
                    for (short i = 0; i < PilotRec.ControlBitCount; i++)
                    {
                        rec.SetControlBit(i, ControlBits.Get(i));
                    }
                    for (short i = 0; i < PilotRec.SpobScannedCount; i++)
                    {
                        rec.SetSpobScanned(i, GameData.Spobs[i].TradingEnabled);
                    }
                    for (short i = 0; i < PilotRec.EscortClassCount; i++)
                    {
                        rec.SetEscortClass(i, -1);
                        rec.SetCarriedClass(i, -1);
                    }
                    for (short i = 0; i < PilotRec.CarriedClassCount; i++)
                    {
                        var ship = GameData.Ships[i];
                        if (ship.IsActive == 0 || ship.OwnerSlot != 0 || ship.AiBehaviorType != ShipAiType.NavalFighter)
                        {
                            rec.SetCarriedClass(i, -1);
                        }
                        else
                        {
                            rec.SetCarriedClass(i, ship.ShipClass);
                        }
                    }
                    for (short i = 0; i < PilotRec.EscortClassCount; i++)
                    {
                        var ship = GameData.Ships[i];
                        if (ship.IsActive == 0 || ship.OwnerSlot != 0 ||
                            ship.AiBehaviorType != ShipAiType.Escort || ship.GrudgeMissionIndex != -1)
                        {
                            rec.SetEscortClass(i, -1);
                        }
                        else
                        {
                            rec.SetEscortClass(i, ship.ShipClass);
                            if (ship.IsCarriedFighter != 0)
                            {
                                rec.SetEscortClass(i, (short)(rec.EscortClass(i) + 1000));
                            }
                        }
                    }
                    rec.DayCounter = WorldState.PlayerCombatRating;
                    aux.Magic = 0x6b;
                    if (WorldState.StrictPlay == 0)
                    {
                        aux.WorldFlag = 0;
                    }
                    else
                    {
                        aux.WorldFlag = 1;
                    }
                    for (short i = 0; i < PilotAuxRec.SpobCount; i++)
                    {
                        if (GameData.Spobs[i].Visible == 0 || GameData.Spobs[i].TradingEnabled == 0)
                        {
                            aux.SetSpobTribute(i, GameData.Spobs[i].Tribute);
                            aux.SetSpobField18(i, 0);
                        }
                        else
                        {
                            aux.SetSpobTribute(i, GameData.Spobs[i].Tribute);
                            aux.SetSpobField18(i, GameData.Spobs[i].TributeAccrualTicks);
                        }
                    }
                    for (short i = 0; i < PilotAuxRec.MissionCount; i++)
                    {
                        if (GameData.Pers[i].AvailableFlag == 0 || GameData.Pers[i].AppearGate < ShipAiType.WimpyTrader)
                        {
                            aux.SetMissionAvailable(i, 0);
                            aux.SetMissionAccepted(i, 0);
                        }
                        else
                        {
                            aux.SetMissionAvailable(i, 1);
                            if (GameData.Pers[i].AcceptedFlag == 0)
                            {
                                aux.SetMissionAccepted(i, 0);
                            }
                            else
                            {
                                aux.SetMissionAccepted(i, 1);
                            }
                        }
                    }
                    aux.Marker1ff4 = 1;
                    for (short i = 0; i < PilotAuxRec.CronCount; i++)
                    {
                        aux.SetCronField0c(i, GameData.Crons[i].StateCountdown);
                        aux.SetCronField02(i, GameData.Crons[i].ChosenSpob);
                    }
                    for (short i = 0; i < PilotAuxRec.JunkCount; i++)
                    {
                        aux.SetJunkPlayerQty(i, GameData.Junk[i].PlayerQty);
                    }
                    for (short i = 0; i < 2; i++)
                    {
                        aux.SetGalaxyState(i, WorldState.StarDrift[i]);
                        aux.SetStarJitter(i, WorldState.StarJitter[i]);
                    }
                    MacToolbox.UseResFile((int)PilotIdentity.FileRefNum);
                    ScramblePilotHandle.Run(recordHandle, 0xabcd1234);
                    ScramblePilotHandle.Run(auxHandle, 0xabcd1234);
                    // Resource names: the record saves as "Pilot Data" and the aux block as the
                    // player ship name (capped 31, the bounded copy the decompile performs).
                    MacToolbox.AddResource(recordHandle, (int)MacResType.PilotRecord, 0x80, "Pilot Data");
                    MacToolbox.AddResource(auxHandle, (int)MacResType.PilotRecord, 0x81,
                        PilotIdentity.ShipName.Length > 31 ? PilotIdentity.ShipName.Substring(0, 31) : PilotIdentity.ShipName);
                    MacToolbox.UseResFile((int)savedCurResFile);
                    MacToolbox.UpdateResFile((int)PilotIdentity.FileRefNum);
                    MacToolbox.HUnlock(recordHandle);
                    MacToolbox.HUnlock(auxHandle);
                    MacToolbox.HPurge(recordHandle);
                    MacToolbox.HPurge(auxHandle);
                    MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                }
                // Write/refresh the "Last Pilot" alias file pointing at this save.
                WriteAliasResourceFile.Run(fsSpec);
                MacToolbox.UseResFile((int)savedCurResFile);
                MacToolbox.FlushVol(0, (int)PrefsFolderLocation.VRefNum);
                result = 0;
            }
        }
        return result;
    }
}
