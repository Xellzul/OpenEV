// Port of FUN_1005e4e4 (EV Override-11.c lines 39173-39293).

using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Systems;

public static class ResolveSystLegalStatusCategory
{
    private const int ScannedSpobLinks = 3;   // FUN_1005e4e4 scans only the first 3 of the 4 StellarLink slots

    public static void Run(short systIndex)
    {
        var syst = SystTable.Store[systIndex];
        int govtStanding = GalaxyMapGlobals.SystemStatus(systIndex);
        short statusCategory = 0;

        int govtThreshold = (syst.Govt < 0 || syst.Govt > 0x7f)
            ? GameData.Governments[0].CrimeTolerance
            : GameData.Governments[syst.Govt].CrimeTolerance;

        if (govtStanding < 0)
        {
            statusCategory = 2;
        }
        if (govtStanding < -govtThreshold)
        {
            statusCategory = 3;
        }
        if (govtStanding < govtThreshold * -4)
        {
            statusCategory = 4;
        }
        if (govtStanding < govtThreshold * -16)
        {
            statusCategory = 5;
        }
        if (govtStanding < govtThreshold * -64)
        {
            statusCategory = 6;
        }
        if (govtStanding < govtThreshold * -256)
        {
            statusCategory = 7;
        }
        if (govtStanding < govtThreshold * -1024)
        {
            statusCategory = 8;
        }
        if (govtStanding < govtThreshold * -4096)
        {
            statusCategory = 9;
        }
        if (govtStanding == 0)
        {
            statusCategory = 1;
        }
        if (0 < govtStanding)
        {
            statusCategory = 10;
        }
        if (govtThreshold << 2 < govtStanding)
        {
            statusCategory = 11;
        }
        if (govtThreshold << 4 < govtStanding)
        {
            statusCategory = 12;
        }
        if (govtThreshold << 6 < govtStanding)
        {
            statusCategory = 13;
        }
        if (govtThreshold << 8 < govtStanding)
        {
            statusCategory = 14;
        }
        if (govtThreshold << 10 < govtStanding)
        {
            statusCategory = 15;
        }

        short undockedSpobCount = 0;
        short dockedSpobCount = 0;
        for (short slotIndex = 0; slotIndex < ScannedSpobLinks; slotIndex = (short)(slotIndex + 1))
        {
            short spobIdx = syst.StellarLink[slotIndex];
            if (spobIdx != -1)
            {
                var spob = GameData.Spobs[spobIdx];
                if (spob.Visible != 0)
                {
                    if ((spob.Flags & (int)SpobFlags.Uninhabited) == 0 &&
                        (spob.Flags & (int)SpobFlags.Landable) != 0)
                    {
                        if (spob.TradingEnabled == 0)
                        {
                            undockedSpobCount = (short)(undockedSpobCount + 1);
                        }
                        else
                        {
                            dockedSpobCount = (short)(dockedSpobCount + 1);
                        }
                    }
                }
            }
        }

        if (0 < dockedSpobCount)
        {
            if (undockedSpobCount < 1)
            {
                statusCategory = 17;
            }
            else
            {
                statusCategory = 16;
            }
        }

        if (-1 < syst.Govt)
        {
            if ((GameData.Governments[syst.Govt].Flags & GovtFlags.Xenophobic) != 0)
            {
                statusCategory = 0;
            }
        }

        if (statusCategory == 0)
        {
            MacToolbox.DrawString("N/A");   // dumped Pascal literal @0x10082210, not a STR# resource
        }
        else
        {
            MacToolbox.DrawString(ResourceGlobals.NamesStr0086[statusCategory]);
        }
    }
}
