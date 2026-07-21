using System;
using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_1001b758 (EV Override-11.c lines 12419-12756): the current-format
// pilot-file loader — open the picked file's resource fork, validate the aux block
// (OpïL 0x81, version word 0x6b; 0x69 = old format -> -45 so OpenPilot falls back
// to the legacy importer), restore the aux/galaxy state and the main record
// (OpïL 0x80), recover the ship name from resource 0x81's resource NAME and the
// pilot name from the FSSpec's file name, then refresh the "Last Pilot" alias.
//
// The two blocks are managed PilotData byte[]s, copied from the loaded resources
// via ResourceBytes/LoadFrom.
public static class LoadPluginPilotData
{
    public static int Run(short vRefNum, int dirID, string fileName)
    {
        var fsSpecScratch = new MacToolbox.FsSpec();
        byte keepResourceFlag = 0;   // gates ReleaseResource (always 0)

        int result = MacToolbox.FSMakeFSSpec((int)vRefNum, dirID,
                                             TextScratch.Trunc(fileName, 31), fsSpecScratch);
        if ((short)result != -43 && (short)result == 0)   // -43 = fnfErr
        {
            // If ResolveAliasFile rewrote the spec to a different folder, bail. The
            // decompile's guard is the FSSpec parID snapshot (int at spec+2).
            int aliasGuardSnapshot = fsSpecScratch.ParID;
            short resolveErr = MacToolbox.ResolveAliasFile(fsSpecScratch, 1, out byte targetIsFolder, out _);
            if (resolveErr == 0 && targetIsFolder == 0 && fsSpecScratch.ParID == aliasGuardSnapshot)
            {
                PilotIdentity.FileRefNum = MacToolbox.FSpOpenResFile(fsSpecScratch, 3);
                if (PilotIdentity.FileRefNum == -1)
                {
                    result = -43;   // fnfErr
                }
                else
                {
                    int auxHandle = MacToolbox.GetResource(MacResType.PilotRecord, 0x81);
                    if (auxHandle == 0)
                    {
                        MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                        result = -43;   // fnfErr
                    }
                    else
                    {
                        MacToolbox.HNoPurge(auxHandle);
                        // De-obfuscate (XOR involution), then copy into the managed block.
                        short auxDescrambleStatus = (short)ScramblePilotHandle.Run(auxHandle, 0xabcd1234);
                        PilotData.AuxBlock.LoadFrom(MacToolbox.ResourceBytes(auxHandle) ?? Array.Empty<byte>());
                        var aux = PilotData.Aux;
                        if (auxDescrambleStatus == 0)
                        {
                            if (aux.Magic == 0x69)
                            {
                                MacToolbox.HUnlock(auxHandle);
                                MacToolbox.HPurge(auxHandle);
                                MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                                return -45;   // old format -> OpenPilot falls back to the legacy importer
                            }
                            if (aux.Magic != 0x6b)
                            {
                                MacToolbox.HUnlock(auxHandle);
                                MacToolbox.HPurge(auxHandle);
                                MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                                return -42;
                            }
                            if (aux.WorldFlag == 1)
                            {
                                WorldState.StrictPlay = 1;
                            }
                            else
                            {
                                WorldState.StrictPlay = 0;
                            }
                            for (short i = 0; i < PilotAuxRec.SpobCount; i++)
                            {
                                if (GameData.Spobs[i].TradingEnabled == 0)
                                {
                                    GameData.Spobs[i].Tribute = aux.SpobTribute(i);
                                    GameData.Spobs[i].TributeAccrualTicks = aux.SpobField18(i);
                                }
                                else
                                {
                                    GameData.Spobs[i].Tribute = 0;
                                    GameData.Spobs[i].TributeAccrualTicks = 0;
                                }
                            }
                            for (short i = 0; i < PilotAuxRec.MissionCount; i++)
                            {
                                if (GameData.Pers[i].AppearGate < ShipAiType.WimpyTrader)
                                {
                                    GameData.Pers[i].AvailableFlag = 0;
                                }
                                else if (aux.MissionAvailable(i) == 0)
                                {
                                    GameData.Pers[i].AvailableFlag = 0;
                                    GameData.Pers[i].AcceptedFlag = 0;
                                }
                                else
                                {
                                    GameData.Pers[i].AvailableFlag = 1;
                                    if (aux.MissionAccepted(i) == 0)
                                    {
                                        GameData.Pers[i].AcceptedFlag = 0;
                                    }
                                    else
                                    {
                                        GameData.Pers[i].AcceptedFlag = 1;
                                    }
                                }
                            }
                            WorldState.FirstEntryCutsceneShown = true;
                            for (short i = 0; i < PilotAuxRec.CronCount; i++)
                            {
                                GameData.Crons[i].StateCountdown = aux.CronField0c(i);
                                GameData.Crons[i].ChosenSpob = aux.CronField02(i);
                            }
                            for (short i = 0; i < PilotAuxRec.JunkCount; i++)
                            {
                                GameData.Junk[i].PlayerQty = aux.JunkPlayerQty(i);
                            }
                            for (short i = 0; i < 2; i++)
                            {
                                WorldState.StarDrift[i] = aux.GalaxyState(i);
                                WorldState.StarJitter[i] = aux.StarJitter(i);
                            }
                            MacToolbox.HPurge(auxHandle);
                            if (keepResourceFlag == 0)
                            {
                                MacToolbox.ReleaseResource(auxHandle);
                            }
                        }
                        int recordHandle = MacToolbox.GetResource(MacResType.PilotRecord, 0x80);
                        if (recordHandle == 0)
                        {
                            MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                            result = -43;   // fnfErr
                        }
                        else
                        {
                            MacToolbox.HNoPurge(recordHandle);
                            short recordDescrambleStatus = (short)ScramblePilotHandle.Run(recordHandle, 0xabcd1234);
                            PilotData.RecordBlock.LoadFrom(MacToolbox.ResourceBytes(recordHandle) ?? Array.Empty<byte>());
                            var rec = PilotData.Record;
                            if (recordDescrambleStatus == 0)
                            {
                                short dockedSpobIndex = rec.DockedSpobIndex;
                                var s0 = GameData.Ships[0];
                                s0.DockedSpobIndex = dockedSpobIndex;
                                if (dockedSpobIndex == -1)
                                {
                                    dockedSpobIndex = 0;
                                }
                                s0.CurrentSystem = GameData.Spobs[dockedSpobIndex].System;
                                // The decompile's int->double idiom is a plain cast (XPos/YPos are short, exact).
                                s0.PosX = (float)GameData.Spobs[dockedSpobIndex].XPos;
                                s0.PosY = (float)GameData.Spobs[dockedSpobIndex].YPos;
                                s0.Heading = (short)SeedEvoRng.Run(360);   // random heading 0..359
                                WorldState.MapViewCentreX = SystTable.Store[s0.CurrentSystem].XPos;
                                WorldState.MapViewCentreY = SystTable.Store[s0.CurrentSystem].YPos;
                                s0.ShipClass = rec.ShipClass;
                                for (short i = 0; i < PilotRec.ShipSlotCount; i++)
                                {
                                    s0.CargoHold[i] = rec.ShipSlot(i);
                                }
                                for (short i = 0; i < PilotRec.OwnedOutfitCount; i++)
                                {
                                    OwnedOutfitGrid.Store[i] = rec.OwnedOutfit(i);
                                }
                                // Unlike the legacy importer, the current format recomputes the
                                // shield from EffectiveShieldMax and ignores the saved +0x10.
                                s0.Shield = (float)ShipDerivedStats.EffectiveShieldMax(ShipTable.Player);
                                s0.Fuel = (float)rec.Fuel;
                                GameDate.Current = new GameDate(
                                    year: rec.DateYear,
                                    month: rec.DateMonth,
                                    day: rec.DateDay);
                                for (short i = 0; i < PilotRec.SystStateCount; i++)
                                {
                                    SystTable.Store[i].Visited = rec.SystState(i);
                                }
                                for (short i = 0; i < PilotRec.KillsBySystCount; i++)
                                {
                                    GalaxyMapGlobals.SetSystemStatus(i, rec.KillsBySyst(i));
                                }
                                for (short i = 0; i < PilotRec.WeaponTypeCount; i++)
                                {
                                    s0.WeaponSlotType[i] = rec.WeaponType(i);
                                    s0.WeaponSlotAmmo[i] = rec.WeaponAmmo(i);
                                }
                                s0.Credits = rec.Credits;
                                // The +0x26ea slot is the player day counter (the WorldState property
                                // is named PlayerCombatRating, a misnomer; the assignment is correct).
                                WorldState.PlayerCombatRating = rec.DayCounter;
                                for (short i = 0; i < PilotRec.MissionStatesCount; i++)
                                {
                                    GameData.MissionStates[i].ReadFrom(rec.Block, rec.MissionStateOffset(i));
                                    GameData.Missions[i].ReadFrom(rec.Block, rec.MissionRecordOffset(i));
                                    if (GameData.MissionStates[i].IsActive != 0)
                                    {
                                        var newDate = GameDate.AdvanceDays(GameData.Missions[i].TimeLimit);
                                        if (newDate.HasValue)
                                        {
                                            var gf = GameData.MissionStates[i];
                                            gf.DeadlineYear = newDate.Value.Year;
                                            gf.DeadlineMonth = newDate.Value.Month;
                                            gf.DeadlineDay = newDate.Value.Day;
                                        }
                                        // Govt name: default "" (the empty string), then the
                                        // STR#(NameStrId)[NameStrIndex] lookup, bounded to 31 chars.
                                        GameData.Missions[i].Name = "";
                                        if (127 < GameData.Missions[i].NameStrId &&
                                            0 < GameData.Missions[i].NameStrIndex)
                                        {
                                            GameData.Missions[i].Name = TextScratch.Trunc(MacToolbox.GetIndString(
                                                GameData.Missions[i].NameStrId,
                                                GameData.Missions[i].NameStrIndex), 31);
                                        }
                                        if ((GameData.Missions[i].Flags & MisnFlags.AuxShipsReplacedWhenDestroyed) != 0)
                                        {
                                            GameData.Missions[i].RemainingSpawnCount = GameData.Missions[i].AuxShipCount;
                                        }
                                        short rng = (short)SeedEvoRng.Run(70);
                                        GameData.Missions[i].SpawnCountdown = (short)(rng + 70);
                                        GameData.Missions[i].LiveSpawnCount = 0;
                                    }
                                }
                                for (short i = 0; i < PilotRec.ControlBitCount; i++)
                                {
                                    ControlBits.Set(i, rec.ControlBit(i));
                                }
                                for (short i = 0; i < PilotRec.SpobScannedCount; i++)
                                {
                                    GameData.Spobs[i].TradingEnabled = rec.SpobScanned(i);
                                }
                                CleanupSystNpcs.Run(1);
                                for (short i = 0; i < PilotRec.EscortClassCount; i++)
                                {
                                    if (-1 < rec.EscortClass(i))
                                    {
                                        if (rec.EscortClass(i) < 1000)
                                        {
                                            short spawnedSlot = (short)SpawnPlayerWingman.Run(rec.EscortClass(i), -1);
                                            GameData.Ships[spawnedSlot].IsCarriedFighter = 0;
                                        }
                                        else
                                        {
                                            short spawnedSlot = (short)SpawnPlayerWingman.Run((short)(rec.EscortClass(i) - 1000), -1);
                                            GameData.Ships[spawnedSlot].IsCarriedFighter = 1;
                                        }
                                    }
                                    if (-1 < rec.CarriedClass(i) && rec.CarriedClass(i) < 64)
                                    {
                                        short spawnedSlot = (short)SpawnPlayerWingman.Run(rec.CarriedClass(i), -1);
                                        if (spawnedSlot != -1)
                                        {
                                            GameData.Ships[spawnedSlot].AiBehaviorType = ShipAiType.NavalFighter;
                                            GameData.Ships[spawnedSlot].IsCarriedFighter = 0;
                                            ShipAi.ResetAiToIdle(ShipTable.Ships[spawnedSlot]);
                                        }
                                    }
                                }
                                MacToolbox.HPurge(recordHandle);
                                MacToolbox.ReleaseResource(recordHandle);
                            }
                            int prefResHandle = MacToolbox.GetResource(MacResType.PilotRecord, 0x81);
                            if (prefResHandle != 0)
                            {
                                MacToolbox.HNoPurge(prefResHandle);
                                // Ship name = resource 0x81's resource name (set by AddResource at
                                // save). Bounded to 63 here, but the decompile's FUN_10076178(.., 0x40)
                                // copies 64 — a known over-clip-by-one (the legacy importer uses 64).
                                string resName = MacToolbox.GetResInfo(prefResHandle);
                                PilotIdentity.ShipName =
                                    resName.Length > 63 ? resName.Substring(0, 63) : resName;
                                MacToolbox.HPurge(prefResHandle);
                                MacToolbox.ReleaseResource(prefResHandle);
                            }
                            // Pilot name = the loaded file's leaf name (FSSpec name field), capped 31.
                            string pilotFileName = MacToolbox.FsSpecName(fsSpecScratch);
                            PilotIdentity.Name =
                                pilotFileName.Length > 31 ? pilotFileName.Substring(0, 31) : pilotFileName;
                            MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                            // Refresh the "Last Pilot" alias pointing at this save.
                            WriteAliasResourceFile.Run(fsSpecScratch);
                            result = 0;
                        }
                    }
                }
            }
            else
            {
                result = -43;   // fnfErr
            }
        }
        return result;
    }
}
