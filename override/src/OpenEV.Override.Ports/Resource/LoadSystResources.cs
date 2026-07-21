using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10784-10900) — loads 'sÿst' resources (MacResType.System).
public static class LoadSystResources
{
    // Resource-layout chunk sizes (not full-array sizes — see SystRecord.cs for those).
    private const int FleetSpawnBiasedCount = 4;     // FleetSpawn[0..3]: −128-biased pers/fleet type refs
    private const int HyperLinkPrimaryCount = 5;     // HyperLink[0..4], packed at resource offset 0x4
    private const int HyperLinkSecondaryCount = 11;  // HyperLink[5..15], packed at resource offset 0x32 (5+11 = SystRecord.HyperLinkCount)

    public static void Run()
    {
        short loadedCount = 0;
        short resCount = (short)MacToolbox.CountResources(MacResType.System);
        for (int loopIdx = 0; loopIdx < SystTable.Count; loopIdx++)
        {
            var sys = SystTable.Store[loopIdx];
            sys.ShownFlag = 0;
            sys.Govt = -32767;   // 0x8001 sentinel (ASM: li r6,-0x7FFF) — no owning government yet
            for (short field = 0; field < SystRecord.HyperLinkCount; field++)
                sys.HyperLink[field] = -1;

            int resHandle = MacToolbox.GetResource(MacResType.System, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                loadedCount++;
                MacToolbox.GetResInfo(resHandle, sys.Name, 31);
                sys.ShownFlag = 1;
                sys.FleetSpawn[1] = MacToolbox.ReadResourceShort(resHandle, 0x18);
                sys.FleetSpawn[0] = MacToolbox.ReadResourceShort(resHandle, 0x16);
                sys.FleetSpawn[3] = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                sys.FleetSpawn[2] = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                sys.FleetSpawn[5] = MacToolbox.ReadResourceShort(resHandle, 0x20);
                sys.FleetSpawn[4] = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                sys.FleetSpawn[7] = MacToolbox.ReadResourceShort(resHandle, 0x24);
                sys.FleetSpawn[6] = MacToolbox.ReadResourceShort(resHandle, 0x22);
                sys.FleetSpawn[8] = MacToolbox.ReadResourceShort(resHandle, 0x26);
                sys.Govt = MacToolbox.ReadResourceShort(resHandle, 0x28);
                sys.Message = MacToolbox.ReadResourceShort(resHandle, 0x2a);
                sys.AsteroidCount = MacToolbox.ReadResourceShort(resHandle, 0x2c);
                sys.Interference = MacToolbox.ReadResourceShort(resHandle, 0x2e);
                if (sys.Govt != -1)
                {
                    sys.Govt = (short)(sys.Govt - 128);
                }
                sys.XPos = MacToolbox.ReadResourceShort(resHandle, 0);
                sys.YPos = MacToolbox.ReadResourceShort(resHandle, 0x2);
                sys.Visibility = MacToolbox.ReadResourceShort(resHandle, 0x30);
                for (short field = 0; field < FleetSpawnBiasedCount; field++)
                {
                    if (sys.FleetSpawn[field] > 0)
                    {
                        sys.FleetSpawn[field] = (short)(sys.FleetSpawn[field] - 128);
                    }
                }
                for (short field = 0; field < HyperLinkPrimaryCount; field++)
                {
                    if (MacToolbox.ReadResourceShort(resHandle, field * 2 + 0x4) < 128)
                    {
                        sys.HyperLink[field] = -1;
                    }
                    else
                    {
                        sys.HyperLink[field] = (short)(MacToolbox.ReadResourceShort(resHandle, field * 2 + 0x4) - 128);
                    }
                }
                for (short field = 0; field < HyperLinkSecondaryCount; field++)
                {
                    if (MacToolbox.ReadResourceShort(resHandle, field * 2 + 0x32) < 128)
                    {
                        sys.HyperLink[field + 5] = -1;
                    }
                    else
                    {
                        sys.HyperLink[field + 5] = (short)(MacToolbox.ReadResourceShort(resHandle, field * 2 + 0x32) - 128);
                    }
                }
                for (short field = 0; field < SystRecord.StellarLinkCount; field++)
                {
                    if (MacToolbox.ReadResourceShort(resHandle, field * 2 + 0xe) == -1)
                    {
                        sys.StellarLink[field] = -1;
                    }
                    else
                    {
                        sys.StellarLink[field] = (short)(MacToolbox.ReadResourceShort(resHandle, field * 2 + 0xe) - 128);
                    }
                }
                uint handleSize = (uint)MacToolbox.GetHandleSize(resHandle);
                if (handleSize < 72)
                {
                    sys.Visibility = -1;
                    for (short field = 5; field < SystRecord.HyperLinkCount; field++)
                    {
                        sys.HyperLink[field] = -1;
                    }
                }
                handleSize = (uint)MacToolbox.GetHandleSize(resHandle);
                if (handleSize < 96)
                {
                    for (short field = 0; field < SystRecord.ForcedPersCount; field++)
                    {
                        sys.ForcedPers[field] = -1;
                    }
                }
                else
                {
                    sys.ForcedPers[1] = MacToolbox.ReadResourceShort(resHandle, 0x4a);
                    sys.ForcedPers[0] = MacToolbox.ReadResourceShort(resHandle, 0x48);
                    sys.ForcedPers[3] = MacToolbox.ReadResourceShort(resHandle, 0x4e);
                    sys.ForcedPers[2] = MacToolbox.ReadResourceShort(resHandle, 0x4c);
                    for (short field = 0; field < SystRecord.ForcedPersCount; field++)
                    {
                        if (sys.ForcedPers[field] < 128)
                        {
                            sys.ForcedPers[field] = -1;
                        }
                        else
                        {
                            sys.ForcedPers[field] = (short)(sys.ForcedPers[field] - 128);
                        }
                    }
                }
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
            if (resCount <= loadedCount) break;
        }
    }
}
