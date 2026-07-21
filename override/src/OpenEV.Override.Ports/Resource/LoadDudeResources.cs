using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources — loads 'düde' resources into the
// typed managed DudeSpawnTable.Store. Faithful 1:1 with FUN_10015e70,
// EV Override-11.c lines 11230-11291.
public static class LoadDudeResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < DudeSpawnTable.Count; loopIdx++)
        {
            int resHandle = MacToolbox.GetResource(MacResType.Dude, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                var rec = GameData.DudeSpawns[loopIdx];
                rec.AiType = (ShipAiType)MacToolbox.ReadResourceShort(resHandle, 0);
                rec.Govt = MacToolbox.ReadResourceShort(resHandle, 0x12);
                if (rec.Govt != -1)
                {
                    rec.Govt -= 128;
                }
                rec.ShipClass[1] = MacToolbox.ReadResourceShort(resHandle, 4);
                rec.ShipClass[0] = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.ShipClass[3] = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.ShipClass[2] = MacToolbox.ReadResourceShort(resHandle, 6);
                rec.Weight[1] = MacToolbox.ReadResourceShort(resHandle, 0xc);
                rec.Weight[0] = MacToolbox.ReadResourceShort(resHandle, 10);
                rec.Weight[3] = MacToolbox.ReadResourceShort(resHandle, 0x10);
                rec.Weight[2] = MacToolbox.ReadResourceShort(resHandle, 0xe);
                rec.Flags = MacToolbox.ReadResourceShort(resHandle, 0x14);
                rec.BarPattern = MacToolbox.ReadResourceShort(resHandle, 0x16);

                for (int slot = 0; slot < DudeSpawnRecord.RollSlotCount; slot++)
                {
                    if (rec.ShipClass[slot] == -1)
                    {
                        rec.MissionBit[slot] = -1;
                    }
                    else
                    {
                        rec.ShipClass[slot] -= 128;
                    }
                }
                if ((uint)MacToolbox.GetHandleSize(resHandle) < 48)
                {
                    for (int slot = 0; slot < DudeSpawnRecord.RollSlotCount; slot++)
                    {
                        rec.MissionBit[slot] = -1;
                    }
                }
                else
                {
                    rec.MissionBit[1] = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                    rec.MissionBit[0] = MacToolbox.ReadResourceShort(resHandle, 0x18);
                    rec.MissionBit[3] = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                    rec.MissionBit[2] = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                }
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
