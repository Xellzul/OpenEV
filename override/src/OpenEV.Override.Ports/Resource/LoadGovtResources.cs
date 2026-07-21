using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads 'gövt' resources 0x80.. into the typed GovtTable.Store.
public static class LoadGovtResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < GovtTable.Count; loopIdx++)
        {
            int resHandle = MacToolbox.GetResource(MacResType.Govt, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                var govt = GameData.Governments[loopIdx];
                govt.Flags = (GovtFlags)MacToolbox.ReadResourceShort(resHandle, 2);
                govt.Ally = MacToolbox.ReadResourceShort(resHandle, 4);
                govt.Enemy = MacToolbox.ReadResourceShort(resHandle, 6);
                govt.CrimeTolerance = MacToolbox.ReadResourceShort(resHandle, 8);
                govt.InitialRecord = MacToolbox.ReadResourceShort(resHandle, 0x14);
                govt.InherentJamming = MacToolbox.ReadResourceShort(resHandle, 0);
                if (govt.Ally != -1)
                    govt.Ally = (short)(govt.Ally - 128);  // res-id base 128 -> table index
                if (govt.Enemy != -1)
                    govt.Enemy = (short)(govt.Enemy - 128);  // res-id base 128 -> table index
                govt.ScanPenalty = MacToolbox.ReadResourceShort(resHandle, 10);
                govt.DisablePenalty = MacToolbox.ReadResourceShort(resHandle, 12);
                govt.BoardPenalty = MacToolbox.ReadResourceShort(resHandle, 14);
                govt.DestroyPenalty = MacToolbox.ReadResourceShort(resHandle, 16);
                govt.ShootPenalty = MacToolbox.ReadResourceShort(resHandle, 18);
                // GetResInfo+FUN_10076178(maxLen=0x1f=31) copies the Str255 raw, so dst[0]
                // receives the source LENGTH byte -- only 30 of the 31 copied bytes are chars.
                string name = MacToolbox.GetResInfo(resHandle);
                govt.Name = name.Length > 30 ? name.Substring(0, 30) : name;
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
