using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Combat;

namespace OpenEV.Override.Ports.Resource;

// FUN_100473e8 (EV Override-11.c lines 29732-29833). Loads the 'mïsn' (mission)
// resources into MissionAvailTable.Store (512 records) and copies each
// resource's name into MissionBoardGlobals.Names; 'ëbug' bits gate the reset
// and load sub-steps. ("Bar person" = the mission-granting NPC at the
// spaceport bar, per IsBarPersEligible/MissionAvailTable — not a distinct Mac
// resource type; the resources loaded here are 'mïsn'.)
public static class LoadBarPersonResources
{
    public static void Run()
    {
        // These six writes are through-pointer cells in the decompile
        // (`*_DAT_10080c0c = -1` etc.); each managed field below is the cell's
        // pointee, not the pointer, so a direct field write is the faithful port.
        SpaceportGlobals.BbsLastSpob = -1;
        RenderGlobals.DrawGateFlag = 0;
        WorldState.CurrentTargetShipId = -1;
        MissionBoardGlobals.DialogWindow = 0;
        GalaxyMapState.PreviewSystem = -1;
        GalaxyMapGlobals.MissionsDirty = 1;
        SubstituteMissionDescTags.SkipSubstitution = (byte)(BugBits.IsSet(BugBit.SkipTooltipDescSubstitution) ? 1 : 0);
        MissionAvailTable.AvailOverride = (byte)(BugBits.IsSet(BugBit.MissionAvailOverride) ? 1 : 0);

        // 'ëbug' bit 1 skips the whole load.
        if (BugBits.IsSet(BugBit.SkipBarPersonMissionLoad))
            return;

        // bit 9 skips the table reset.
        if (!BugBits.IsSet(BugBit.SkipMissionTableReset))
        {
            for (short i = 0; i < MissionStateTable.Count; i++)
            {
                GameData.MissionStates[i].IsActive = 0;
                GameData.MissionStates[i].Failed = 0;
            }
            for (short i = 0; i < MissionAvailTable.Count; i++)
            {
                ControlBits.Set(i, 0);
                var rec = GameData.MissionAvail[i];
                rec.LocationSelector = -32000;   // 0x8300 sentinel; IsBarPersEligible tests (< -31999)
                rec.AppearOdds = 0;
                rec.AvailLocation = -1;
                rec.AvailShipType = -1;
            }
        }

        // bit 8 skips the resource load loop.
        if (BugBits.IsSet(BugBit.SkipMissionAvailResLoad))
            return;

        for (int loopIdx = 0; loopIdx < MissionAvailTable.Count; loopIdx++)
        {
            short i = (short)loopIdx;
            MissionDefTable.ResourceHandle = 0;
            int resHandle = MacToolbox.GetResource(MacResType.Mission, loopIdx + 128);
            MissionDefTable.ResourceHandle = resHandle;   // publish the live handle (managed)
            if (resHandle == 0)
                continue;

            MacToolbox.HLock(resHandle);
            var rec = GameData.MissionAvail[i];

            if (!BugBits.IsSet(BugBit.SkipMissionAvailFieldRead))
            {
                rec.LocationSelector = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.RequireBit = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.ForbidBit = MacToolbox.ReadResourceShort(resHandle, 0x46);
                rec.AvailLocation = MacToolbox.ReadResourceShort(resHandle, 4);
                rec.RecordRequirement = MacToolbox.ReadResourceShort(resHandle, 6);
                rec.ScoreRequirement = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.AppearOdds = MacToolbox.ReadResourceShort(resHandle, 10);
                rec.CargoSpaceRequired = MacToolbox.ReadResourceShort(resHandle, 0x12);
                rec.Flags = MacToolbox.ReadResourceShort(resHandle, 0x50);
                rec.AvailShipType =
                    (uint)MacToolbox.GetHandleSize(resHandle) < 112 ? (short)-1
                                                                     : MacToolbox.ReadResourceShort(resHandle, 0x5a);
                if (rec.AvailLocation < 0)
                    rec.AvailLocation = 0;
                if (2 < rec.AvailLocation)
                    rec.AvailLocation = 2;
            }
            if (!BugBits.IsSet(BugBit.SkipCargoSpaceReroll))
                rec.CargoSpaceRequired = ResolveSignedRollShort.Run(rec.CargoSpaceRequired);
            if (!BugBits.IsSet(BugBit.SkipMissionNameCopy))
                MissionBoardGlobals.Names[i] = TextScratch.Trunc(MacToolbox.GetResInfo(resHandle), 250);

            MacToolbox.HUnlock(resHandle);
            MacToolbox.HPurge(resHandle);
            MacToolbox.ReleaseResource(resHandle);
        }
    }
}
