// Port of FUN_1004adf8 from EV Override-11.c lines 31011-31210.

using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Systems;

namespace OpenEV.Override.Ports.Mission;

// Loads one 'mïsn' resource (id missionIdx+0x80) into the active-mission slot record
// GameData.Missions[slot], plus the four header shorts copied from
// GameData.MissionDefs[missionIdx]. Picks a random mission name from the STR#
// referenced at res+0x2a. The raw 0x186-byte mission-record range is retired;
// this loads directly onto the typed managed MissionRecord instead.
public static class LoadMissionResource
{
    public static void Run(int missionIdx, short slot)
    {
        // slot is the active-mission SLOT index into the mission table, not a govt id.
        var rec = GameData.Missions[slot];
        var def = GameData.MissionDefs[(short)missionIdx];

        rec.TargetSpob = def.TargetSpob;
        rec.ReturnSpob = def.ReturnSpob;
        rec.CargoStringIndex = def.CargoType;
        rec.CargoMass = def.CargoQty;

        int h = MacToolbox.GetResource(MacResType.Mission, missionIdx + 128);
        MissionDefTable.ResourceHandle = h;
        if (h != 0)
        {
            MacToolbox.HLock(h);
            rec.PickupMode = (MissionCargoPickupMode)MacToolbox.ReadResourceShort(h, 0x14);
            rec.DropOffMode = MacToolbox.ReadResourceShort(h, 0x16);
            rec.ScanPersIndex = MacToolbox.ReadResourceShort(h, 0x18);
            if (rec.ScanPersIndex != -1)
            {
                rec.ScanPersIndex -= 128;
            }
            rec.Pay = MacToolbox.ReadResourceInt(h, 0x1c);
            rec.SpawnCount = MacToolbox.ReadResourceShort(h, 0x20);
            rec.GoalThreshold = rec.SpawnCount;
            rec.ShipToBoardOrScan = MacToolbox.ReadResourceShort(h, 0x24);
            if (0 < rec.ShipToBoardOrScan)
            {
                rec.ShipToBoardOrScan -= 128;
            }
            rec.MissionGoalType = (MissionGoalKind)MacToolbox.ReadResourceShort(h, 0x26);
            rec.ShipBehavior = MacToolbox.ReadResourceShort(h, 0x28);
            rec.CompletionBitA = MacToolbox.ReadResourceShort(h, 0x2c);
            rec.CompletionBitB = MacToolbox.ReadResourceShort(h, 0x4e);
            rec.CompletionBitC = MacToolbox.ReadResourceShort(h, 0x52);
            rec.CompletionBitD = MacToolbox.ReadResourceShort(h, 0x54);
            rec.CargoType = MacToolbox.ReadResourceShort(h, 0x2e);
            rec.CargoQty = MacToolbox.ReadResourceShort(h, 0x30);
            if (127 < rec.CargoType)
            {
                rec.CargoType -= 128;
            }
            if (rec.CargoType < 0 || 127 < rec.CargoType)
            {
                rec.CargoQty = 0;
            }
            rec.FailBitA = MacToolbox.ReadResourceShort(h, 0x32);
            rec.FailBitB = MacToolbox.ReadResourceShort(h, 0x56);
            rec.Flags = (MisnFlags)MacToolbox.ReadResourceShort(h, 0x50);
            rec.AcceptText = MacToolbox.ReadResourceShort(h, 0x34);
            rec.MissionInfoText = MacToolbox.ReadResourceShort(h, 0x36);
            rec.LoadCargoText = MacToolbox.ReadResourceShort(h, 0x38);
            rec.DumpCargoText = MacToolbox.ReadResourceShort(h, 0x3a);
            rec.CompletionText = MacToolbox.ReadResourceShort(h, 0x3c);
            rec.FailText = MacToolbox.ReadResourceShort(h, 0x3e);
            rec.RefuseText = MacToolbox.ReadResourceShort(h, 0x58);
            rec.TimeLimit = MacToolbox.ReadResourceShort(h, 0x40);
            rec.Field0x52 = MacToolbox.ReadResourceShort(h, 0x44);
            rec.AuxShipCount = MacToolbox.ReadResourceShort(h, 0x48);
            rec.SpawnDudeId = MacToolbox.ReadResourceShort(h, 0x4a);
            rec.AuxSpawnSystem = MacToolbox.ReadResourceShort(h, 0x4c);
            rec.SpawnCountdown = (short)(SeedEvoRng.Run(70) + 70);
            rec.RemainingSpawnCount = rec.AuxShipCount;
            while (127 < rec.SpawnDudeId)
            {
                rec.SpawnDudeId -= 128;
            }
            if (rec.AuxShipCount < 1)
            {
                rec.AuxShipCount = -1;
                rec.RemainingSpawnCount = -1;
            }
            if (rec.ShipBehavior < 9)
            {
                rec.MissionShipSpawnCountdown = -1;
            }
            else
            {
                rec.MissionShipSpawnCountdown = (short)(SeedEvoRng.Run(100) + 100);
            }
            rec.NameStrId = -1;
            rec.NameStrIndex = -1;
            if (rec.TimeLimit < 0)
            {
                rec.TimeLimit = -32000;
            }
            rec.ContrabandScanArmed = (byte)(MacToolbox.ReadResourceShort(h, 0x1a) == 0 ? 0 : 1);
            rec.AbortMissionOnScan = (byte)(MacToolbox.ReadResourceShort(h, 0x42) == 0 ? 0 : 1);

            // Destination-system selector (res+0x22): -1 = player's current system;
            // -3/-4 = system of the spob at def.TargetSpob/def.ReturnSpob; -6 = the
            // -6 sentinel; -5 or >9998 = ResolveSystSentinel; else a plain syst id (-128).
            short dest = MacToolbox.ReadResourceShort(h, 0x22);
            if (dest == -1)
            {
                rec.DestSystem = GameData.Player.CurrentSystem;
            }
            else if (dest == -3)
            {
                rec.DestSystem = GameData.Spobs[def.TargetSpob].System;
            }
            else if (dest == -4)
            {
                rec.DestSystem = GameData.Spobs[def.ReturnSpob].System;
            }
            else if (dest == -6)
            {
                rec.DestSystem = -6;
            }
            else if (dest == -5 || 9998 < dest)
            {
                rec.DestSystem = (short)ResolveSystSentinel.Run(dest, GameData.Player.CurrentSystem);
            }
            else
            {
                rec.DestSystem = (short)(dest - 128);
            }

            // Default name copy source resolves (under the real TOC) to 0x100820DC —
            // three NUL bytes, i.e. this clears the name rather than copying scratch.
            rec.Name = "";
            short strId = MacToolbox.ReadResourceShort(h, 0x2a);
            if (strId != -1)
            {
                rec.NameStrId = strId;
                int strHandle = MacToolbox.GetResource(MacResType.StringList, rec.NameStrId);
                if (strHandle != 0)
                {
                    MacToolbox.HLock(strHandle);
                    short strTotal = MacToolbox.ReadResourceShort(strHandle, 0);
                    MacToolbox.HUnlock(strHandle);
                    MacToolbox.HPurge(strHandle);
                    MacToolbox.ReleaseResource(strHandle);
                    int pick = (int)SeedEvoRng.Run(strTotal);
                    string name = MacToolbox.GetIndString(strId, (short)(pick + 1));
                    // ASM: FUN_10076178(rec.Name, GetIndString-buffer, 0x1f) copies a 31-byte
                    // Str255 raw (byte 0 = the Pascal length prefix) — real character capacity
                    // is 31-1 = 30, matching the same idiom in LoadGovtResources/LoadSpobResources.
                    rec.Name = name.Length > 30 ? name.Substring(0, 30) : name;
                    rec.NameStrIndex = (short)(pick + 1);
                }
            }
            rec.CargoPickedUp = 0;
            rec.DestroyedShipCount = 0;
            rec.BoardedShipCount = 0;
            rec.DisabledShipCount = 0;
            rec.DepartedShipCount = 0;
            if (rec.ShipBehavior < 9)
            {
                rec.MissionShipsSpawnedCount = rec.GoalThreshold;
            }
            else
            {
                rec.MissionShipsSpawnedCount = 0;
            }
            rec.LiveSpawnCount = 0;
            MacToolbox.HUnlock(h);
            MacToolbox.HPurge(h);
            MacToolbox.ReleaseResource(h);
        }
        rec.MissionDefIndex = (short)missionIdx;
    }
}
