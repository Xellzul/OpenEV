using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads 'jünk' resources 0x80.. into the typed GameData.Junk.
public static class LoadJunkResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < JunkTable.Count; loopIdx++)
        {
            var junk = GameData.Junk[loopIdx];
            junk.BoughtAtSpob = -1;
            junk.SoldAtSpob = -1;
            junk.PlayerQty = 0;
            int resHandle = MacToolbox.GetResource(MacResType.Junk, loopIdx + 0x80);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                junk.BoughtAtSpob = MacToolbox.ReadResourceShort(resHandle, 2);
                junk.SoldAtSpob = MacToolbox.ReadResourceShort(resHandle, 0);
                junk.BasePrice = MacToolbox.ReadResourceShort(resHandle, 4);
                junk.Flags = MacToolbox.ReadResourceShort(resHandle, 6);
                if (0x7f < junk.BoughtAtSpob)
                {
                    junk.BoughtAtSpob = (short)(junk.BoughtAtSpob - 0x80);
                }
                if (0x7f < junk.SoldAtSpob)
                {
                    junk.SoldAtSpob = (short)(junk.SoldAtSpob - 0x80);
                }
                string name = MacToolbox.GetResInfo(resHandle);
                junk.Name = name.Length > 0x3e ? name.Substring(0, 0x3e) : name;   // FUN_10076178 copies a 0x3f-byte Pascal buffer (byte 0 = length prefix), so only 0x3e (62) chars are real
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
