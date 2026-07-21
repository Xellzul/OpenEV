using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads the 4 nebula resources ('nëbu' 0x80..0x83) into MapNebulaTable.Store
// (X/Y/Width/Height; the Charted flag is set elsewhere per system).
public static class LoadNebulaResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < MapNebulaTable.Count; loopIdx++)
        {
            var neb = GameData.MapNebulas[loopIdx];
            neb.Height = 0;
            neb.Width = 0;
            neb.Charted = 0;
            int resHandle = MacToolbox.GetResource(MacResType.Nebula, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                neb.X = MacToolbox.ReadResourceShort(resHandle, 0);
                neb.Y = MacToolbox.ReadResourceShort(resHandle, 2);
                neb.Width = MacToolbox.ReadResourceShort(resHandle, 4);
                neb.Height = MacToolbox.ReadResourceShort(resHandle, 6);
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
