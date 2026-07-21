using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads 'flët' resources 0x80.. into the typed FleetTable.Store.
public static class LoadFleetResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < FleetTable.Count; loopIdx++)
        {
            var fleet = GameData.Fleets[loopIdx];
            fleet.LeadShipType = -1;
            int resHandle = MacToolbox.GetResource(MacResType.Fleet, loopIdx + 0x80);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                fleet.LeadShipType = (short)(MacToolbox.ReadResourceShort(resHandle, 0) - 0x80);
                fleet.Govt = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                fleet.LinkSyst = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                if (0x7f < fleet.Govt)
                {
                    fleet.Govt = (short)(fleet.Govt - 0x80);
                }
                for (int field = 0; field < FleetRecord.EscortGroupCount; field++)
                {
                    fleet.EscortType[field] = (short)(MacToolbox.ReadResourceShort(resHandle, field * 2 + 2) - 0x80);
                    fleet.EscortMin[field] = MacToolbox.ReadResourceShort(resHandle, field * 2 + 0xa);
                    fleet.EscortMax[field] = MacToolbox.ReadResourceShort(resHandle, field * 2 + 0x12);
                }
                // Short 'flët' records (no +0x1e field in the resource) get no MissionBit gate.
                if ((uint)MacToolbox.GetHandleSize(resHandle) < 0x30)
                {
                    fleet.MissionBit = -1;
                }
                else
                {
                    fleet.MissionBit = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                }
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
