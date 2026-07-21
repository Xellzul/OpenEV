using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_100487e8 from EV Override-11.c lines 30267-30345.
//
// Build one mission-definition table entry (MissionDefs[idx]) from the 'mïsn'
// resource: resolve the start/destination spobs (res+0xc/+0xe via the AI-target
// resolver, -1 = none), cache their systems, copy the cargo type (res+0x10) +
// the required cargo space, and stamp the expiry date (res+0x40 days out).
public static class ResolveSingleMissionSpawn
{
    public static void Run(int missionDefIndex)
    {
        short i = (short)missionDefIndex;
        var def = GameData.MissionDefs[i];

        // Anchor spob: the player's nav target in space, else the current
        // system's first stellar link.
        int anchorSpob;
        if (RenderGlobals.DrawGateFlag == 0)
        {
            anchorSpob = GameData.Ships[0].NavTargetSpob;
        }
        else
        {
            anchorSpob = 0;
            var syst = SystTable.Store[GameData.Ships[0].CurrentSystem];
            for (short k = 0; k < SystRecord.StellarLinkCount; k = (short)(k + 1))
            {
                if (syst.StellarLink[k] != -1)
                {
                    anchorSpob = syst.StellarLink[k];
                    break;
                }
            }
        }

        int resHandle = MacToolbox.GetResource(MacResType.Mission, missionDefIndex + 128);
        MissionDefTable.ResourceHandle = resHandle;
        if (resHandle != 0)
        {
            MacToolbox.HLock(resHandle);
            if (MacToolbox.ReadResourceShort(resHandle, 0xc) == -1)
            {
                def.TargetSpob = -1;
                def.TargetSystem = -1;
            }
            else
            {
                short target = (short)ResolveAiTargetByType.Run(MacToolbox.ReadResourceShort(resHandle, 0xc), anchorSpob);
                def.TargetSpob = target;
                def.TargetSystem = SpobSystem(target);
            }
            if (MacToolbox.ReadResourceShort(resHandle, 0xe) == -1)
            {
                def.ReturnSpob = def.TargetSpob;
            }
            else
            {
                def.ReturnSpob = (short)ResolveAiTargetByType.Run(MacToolbox.ReadResourceShort(resHandle, 0xe), anchorSpob);
            }
            def.ReturnSystem = SpobSystem(def.ReturnSpob);
            def.CargoType = MacToolbox.ReadResourceShort(resHandle, 0x10);
            def.CargoQty = GameData.MissionAvail[i].CargoSpaceRequired;
            if (def.CargoType == 1000)
            {
                def.CargoType = (short)SeedEvoRng.Run(6);
            }
            // Short (pre-1.0.2 layout) resources have no res+0x5e field.
            if ((uint)MacToolbox.GetHandleSize(resHandle) < 112)
            {
                def.ControlBitLink = -1;
            }
            else
            {
                def.ControlBitLink = MacToolbox.ReadResourceShort(resHandle, 0x5e);
            }
            var newDate = GameDate.AdvanceDays(MacToolbox.ReadResourceShort(resHandle, 0x40));
            if (newDate.HasValue)
            {
                def.DeadlineYear = newDate.Value.Year;
                def.DeadlineMonth = newDate.Value.Month;
                def.DeadlineDay = newDate.Value.Day;
            }
            MacToolbox.HUnlock(resHandle);
            MacToolbox.HPurge(resHandle);
            MacToolbox.ReleaseResource(resHandle);
            MissionDefTable.ResourceHandle = 0;
        }
    }

    // DEVIATION (faithful): an out-of-range spob index made the original read the
    // heap BEFORE/AFTER the spob table (the -1 case landed 0x44 into the preceding
    // ship-table allocation) — unpreservable in managed code, so those cases yield
    // -1 instead (bounds-guarded).
    private static short SpobSystem(short spobIdx)
        => (uint)spobIdx < (uint)SpobTable.Store.Length
            ? GameData.Spobs[spobIdx].System
            : (short)-1;
}
