using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads 'öops' resources (timed news / commodity-price events) into the typed
// managed CronTable.Store.
public static class LoadCronResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < CronTable.Count; loopIdx++)
        {
            var rec = GameData.Crons[loopIdx];
            rec.DailyOdds = -1;
            rec.StateCountdown = -1;
            rec.ChosenSpob = -1;
            rec.LocationSelector = -32767;   // 0x8001 empty sentinel
            rec.ControlBit = -1;
            int resHandle = MacToolbox.GetResource(MacResType.Oops, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                rec.LocationSelector = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.Commodity = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.PriceDelta = MacToolbox.ReadResourceShort(resHandle, 4);
                rec.DurationDays = MacToolbox.ReadResourceShort(resHandle, 6);
                rec.DailyOdds = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.StateCountdown = 0;
                rec.ControlBit = MacToolbox.ReadResourceShort(resHandle, 10);
                if (rec.ControlBit < 0 || 0x1ff < rec.ControlBit)
                    rec.ControlBit = -1;
                // GetResInfo+FUN_10076178(maxLen=0x3f=63) copies the Str255 raw, so dst[0]
                // receives the source LENGTH byte -- only 0x3e=62 of the copied bytes are chars.
                string name = MacToolbox.GetResInfo(resHandle);
                rec.Name = name.Length > 0x3e ? name.Substring(0, 0x3e) : name;
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
