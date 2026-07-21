using System;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_1001c5fc (EV Override-11.c lines 12757-13069): the LEGACY (pre-0x6b
// format, version word 0x69) pilot importer, reached via OpenPilot's -45 fallback.
// The legacy file layout differs from the current one (256-mission band, 64-outfit
// and 36-weapon bands, 192 control bits, shifted offsets) — the LegacyAux/LegacyRec
// facades at the bottom name every offset. Resource bytes are de-obfuscated in
// place, then copied into local PilotBlocks (the loaded handle is the registry
// byte[], so reading it directly is fine).
public static class LoadPilotPluginFile
{
    public static int Run(short vRefNum, int dirID, string fileName)
    {
        var fsSpec = new MacToolbox.FsSpec();
        int result = MacToolbox.FSMakeFSSpec((int)vRefNum, dirID, fileName, fsSpec);
        if ((short)result != -43 && (short)result == 0)   // -43 = fnfErr
        {
            // If ResolveAliasFile rewrote the spec to a different folder, bail (the
            // decompile's guard is the FSSpec parID snapshot, int at spec+2).
            int aliasGuardSnapshot = fsSpec.ParID;
            short resolveErr = MacToolbox.ResolveAliasFile(fsSpec, 1, out byte targetIsFolder, out _);
            if (resolveErr == 0 && targetIsFolder == 0 && fsSpec.ParID == aliasGuardSnapshot)
            {
                PilotIdentity.FileRefNum = (short)MacToolbox.FSpOpenResFile(fsSpec, 3);
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
                        short auxDescrambleStatus = (short)ScramblePilotHandle.Run(auxHandle, 0xabcd1234);
                        var aux = LegacyAux.From(auxHandle);
                        if (auxDescrambleStatus == 0)
                        {
                            if (aux.WorldFlag == 1)
                            {
                                WorldState.StrictPlay = 1;
                            }
                            else
                            {
                                WorldState.StrictPlay = 0;
                            }
                            for (short i = 0; i < SpobTable.Count; i++)
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
                            for (short i = 0; i < PersTable.Count; i++)
                            {
                                GameData.Pers[i].AvailableFlag = 0;
                                GameData.Pers[i].AcceptedFlag = 0;
                            }
                            // The legacy format only carries 256 mission flags (current carries 512).
                            for (short i = 0; i < 256; i++)
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
                            for (short i = 0; i < CronTable.Count; i++)
                            {
                                GameData.Crons[i].StateCountdown = aux.CronField0c(i);
                                GameData.Crons[i].ChosenSpob = aux.CronField02(i);
                            }
                            for (short i = 0; i < JunkTable.Count; i++)
                            {
                                GameData.Junk[i].PlayerQty = aux.JunkPlayerQty(i);
                            }
                            for (short i = 0; i < 2; i++)
                            {
                                WorldState.StarDrift[i] = aux.StarDrift(i);
                                WorldState.StarJitter[i] = aux.StarJitter(i);
                            }
                            MacToolbox.HPurge(auxHandle);
                            MacToolbox.ReleaseResource(auxHandle);
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
                            var rec = LegacyRec.From(recordHandle);
                            if (recordDescrambleStatus == 0)
                            {
                                short dockedSpobIndex = rec.DockedSpobIndex;
                                GameData.Ships[0].DockedSpobIndex = dockedSpobIndex;
                                if (dockedSpobIndex == -1)
                                {
                                    dockedSpobIndex = 0;
                                }
                                GameData.Ships[0].CurrentSystem = GameData.Spobs[dockedSpobIndex].System;
                                GameData.Ships[0].PosX = (float)GameData.Spobs[dockedSpobIndex].XPos;
                                GameData.Ships[0].PosY = (float)GameData.Spobs[dockedSpobIndex].YPos;
                                GameData.Ships[0].Heading = (short)SeedEvoRng.Run(360);   // random heading 0..359
                                WorldState.MapViewCentreX = SystTable.Store[GameData.Ships[0].CurrentSystem].XPos;
                                WorldState.MapViewCentreY = SystTable.Store[GameData.Ships[0].CurrentSystem].YPos;
                                GameData.Ships[0].ShipClass = rec.ShipClass;
                                for (short i = 0; i < PilotRec.ShipSlotCount; i++)
                                {
                                    GameData.Ships[0].CargoHold[i] = rec.ShipSlot(i);
                                }
                                // Unlike the current-format loader, the legacy importer restores the
                                // saved shield short directly.
                                GameData.Ships[0].Shield = (float)(int)rec.Shield;
                                GameData.Ships[0].Fuel = (float)rec.Fuel;
                                // Restore the game clock (save +0x14 month, +0x16 day, +0x18 year).
                                GameDate.Current = new GameDate(
                                    year: rec.DateYear,
                                    month: rec.DateMonth,
                                    day: rec.DateDay);
                                for (short i = 0; i < PilotRec.SystStateCount; i++)
                                {
                                    SystTable.Store[i].Visited = rec.SystState(i);
                                }
                                for (short i = 0; i < OwnedOutfitGrid.Count; i++)
                                {
                                    OwnedOutfitGrid.Store[i] = 0;
                                }
                                // The legacy format only carries 64 outfit slots (current carries 128).
                                for (short i = 0; i < 64; i++)
                                {
                                    OwnedOutfitGrid.Store[i] = rec.OwnedOutfit(i);
                                }
                                for (short i = 0; i < PilotRec.KillsBySystCount; i++)
                                {
                                    GalaxyMapGlobals.SetSystemStatus(i, rec.KillsBySyst(i));
                                }
                                for (short i = 0; i < ShipRecord.WeaponSlotCount; i++)
                                {
                                    GameData.Ships[0].WeaponSlotType[i] = 0;
                                    GameData.Ships[0].WeaponSlotAmmo[i] = 0;
                                }
                                // The legacy format only carries 36 weapon slots (current carries 64).
                                for (short i = 0; i < 36; i++)
                                {
                                    GameData.Ships[0].WeaponSlotType[i] = rec.WeaponType(i);
                                    GameData.Ships[0].WeaponSlotAmmo[i] = rec.WeaponAmmo(i);
                                }
                                GameData.Ships[0].Credits = rec.Credits;
                                WorldState.PlayerCombatRating = rec.CombatRating;
                                for (short i = 0; i < PilotRec.MissionStatesCount; i++)
                                {
                                    GameData.MissionStates[i].ReadFrom(rec.Block, rec.MissionStateOffset(i));
                                    GameData.Missions[i].ReadFrom(rec.Block, rec.MissionRecordOffset(i));
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
                                }
                                for (short i = 0; i < ControlBits.Count; i++)
                                {
                                    ControlBits.Set(i, 0);
                                }
                                // The legacy format only carries 192 control bits (current carries 512).
                                for (short i = 0; i < 192; i++)
                                {
                                    ControlBits.Set(i, rec.ControlBit(i));
                                }
                                for (short i = 0; i < SpobTable.Count; i++)
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
                            int resInfoHandle = MacToolbox.GetResource(MacResType.PilotRecord, 0x81);
                            if (resInfoHandle != 0)
                            {
                                MacToolbox.HNoPurge(resInfoHandle);
                                // Ship name = resource 0x81's resource name (set by AddResource at
                                // save), bounded to 64 (the legacy importer's cap; the current loader
                                // over-clips by one at 63).
                                PilotIdentity.ShipName = TextScratch.Trunc(MacToolbox.GetResInfo(resInfoHandle), 64);
                                MacToolbox.HPurge(resInfoHandle);
                                MacToolbox.ReleaseResource(resInfoHandle);
                            }
                            // Pilot name = the loaded file's name (the decompile copies the FSSpec
                            // name field; the managed fileName string here), capped 31.
                            PilotIdentity.Name = TextScratch.Trunc(fileName, 31);
                            MacToolbox.CloseResFile((int)PilotIdentity.FileRefNum);
                            // Refresh the "Last Pilot" alias pointing at this save.
                            WriteAliasResourceFile.Run(fsSpec);
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

    // Copy a (de-obfuscated) resource's bytes into a local PilotBlock so the facades
    // below can read it by named offset.
    private static PilotBlock BlockFrom(int resourceHandle)
    {
        byte[] bytes = MacToolbox.ResourceBytes(resourceHandle) ?? Array.Empty<byte>();
        var block = new PilotBlock(bytes.Length < 2 ? 2 : bytes.Length);
        block.LoadFrom(bytes);
        return block;
    }

    // LEGACY (0x69-format) AUX block layout (OpïL 0x81): a 256-mission band and
    // shifted tails relative to the current PilotAuxRec.
    private readonly struct LegacyAux
    {
        public readonly PilotBlock Block;
        private LegacyAux(PilotBlock block) { Block = block; }
        public static LegacyAux From(int resourceHandle) => new LegacyAux(BlockFrom(resourceHandle));

        /// +0x02 short — world flag (StrictPlay mirror).
        public short WorldFlag => Block.ShortAt(0x02);
        /// short[1500] @ +0x04  [spob Tribute].
        public short SpobTribute(int i) => Block.ShortAt(0x04 + i * 2);
        /// short[1500] @ +0x103c  [spob +0x18].
        public short SpobField18(int i) => Block.ShortAt(0x103c + i * 2);
        /// short[256] @ +0xbbc  [mission available flag].
        public short MissionAvailable(int i) => Block.ShortAt(0xbbc + i * 2);
        /// short[256] @ +0xdbc  [mission accepted flag].
        public short MissionAccepted(int i) => Block.ShortAt(0xdbc + i * 2);
        /// short[128] @ +0x1bf6  [cron +0x0c].
        public short CronField0c(int i) => Block.ShortAt(0x1bf6 + i * 2);
        /// short[128] @ +0x1cf6  [cron +0x02].
        public short CronField02(int i) => Block.ShortAt(0x1cf6 + i * 2);
        /// short[128] @ +0x1df6  [junk PlayerQty].
        public short JunkPlayerQty(int i) => Block.ShortAt(0x1df6 + i * 2);
        /// short[2] @ +0x1ef6  [star-drift pair].
        public short StarDrift(int i) => Block.ShortAt(0x1ef6 + i * 2);
        /// short[2] @ +0x1efa  [star-jitter pair].
        public short StarJitter(int i) => Block.ShortAt(0x1efa + i * 2);
    }

    // LEGACY (0x69-format) MAIN record layout (OpïL 0x80): the header matches the
    // current PilotRec; the array regions shift after the 64-entry owned-outfit band
    // (current has 128) and the 36-slot weapon bands (current has 64).
    private readonly struct LegacyRec
    {
        public readonly PilotBlock Block;
        private LegacyRec(PilotBlock block) { Block = block; }
        public static LegacyRec From(int resourceHandle) => new LegacyRec(BlockFrom(resourceHandle));

        /// +0x00 short — docked spob index.
        public short DockedSpobIndex => Block.ShortAt(0x00);
        /// +0x02 short — player ship class.
        public short ShipClass => Block.ShortAt(0x02);
        /// short[6] @ +0x04  [player ship +0x3a array].
        public short ShipSlot(int i) => Block.ShortAt(0x04 + i * 2);
        /// +0x10 short — shield (legacy restores it directly).
        public short Shield => Block.ShortAt(0x10);
        /// +0x12 short — fuel.
        public short Fuel => Block.ShortAt(0x12);
        /// +0x14/+0x16/+0x18 short — save date (month/day/year).
        public short DateMonth => Block.ShortAt(0x14);
        public short DateDay => Block.ShortAt(0x16);
        public short DateYear => Block.ShortAt(0x18);
        /// short[1000] @ +0x1a  [per-system state].
        public short SystState(int i) => Block.ShortAt(0x1a + i * 2);
        /// short[64] @ +0x7ea  [owned outfits — legacy band is HALF the current one].
        public short OwnedOutfit(int i) => Block.ShortAt(0x7ea + i * 2);
        /// short[1000] @ +0x86a  [kill count by system].
        public short KillsBySyst(int i) => Block.ShortAt(0x86a + i * 2);
        /// short[36] @ +0x103a / +0x1082  [weapon slot types / ammo].
        public short WeaponType(int i) => Block.ShortAt(0x103a + i * 2);
        public short WeaponAmmo(int i) => Block.ShortAt(0x1082 + i * 2);
        /// +0x10ca int — credits.
        public int Credits => Block.IntAt(0x10ca);
        /// +0x24ba int — player combat rating.
        public int CombatRating => Block.IntAt(0x24ba);
        /// mission-state records [8] × 0x12 @ +0x10ce.
        public int MissionStateOffset(int i) => 0x10ce + i * 0x12;
        /// mission records [8] × 0x186 @ +0x115e.
        public int MissionRecordOffset(int i) => 0x115e + i * 0x186;
        /// byte[192] @ +0x1d8e  [control bits — legacy band is 192 of the 512].
        public byte ControlBit(int i) => Block.ByteAt(0x1d8e + i);
        /// byte[1500] @ +0x1e4e  [per-spob trading-enabled bit].
        public byte SpobScanned(int i) => Block.ByteAt(0x1e4e + i);
        /// short[36] @ +0x242a / +0x2472  [escort / carried ship classes].
        public short EscortClass(int i) => Block.ShortAt(0x242a + i * 2);
        public short CarriedClass(int i) => Block.ShortAt(0x2472 + i * 2);
    }
}
