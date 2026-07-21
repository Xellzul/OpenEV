using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10722-10782) —
// loads 'spöb' (stellar object) resources into the typed managed SpobTable.Store[].
public static class LoadSpobResources
{
    public static void Run()
    {
        short loadedCount = 0;
        short resCount = (short)MacToolbox.CountResources(MacResType.Spob);

        for (int loopIdx = 0; loopIdx < SpobTable.Count; loopIdx++)
        {
            var rec = GameData.Spobs[loopIdx];
            rec.System = -1;
            rec.Visible = 0;

            int resHandle = MacToolbox.GetResource(MacResType.Spob, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                loadedCount++;

                // ASM: li r5,0x1F before sub_76178, copying a 31-byte Pascal buffer (byte 0 =
                // length prefix) — real character capacity is 31-1 = 30.
                string name = MacToolbox.GetResInfo(resHandle);
                rec.Name = name.Length > 30 ? name.Substring(0, 30) : name;
                rec.XPos = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.YPos = MacToolbox.ReadResourceShort(resHandle, 0x2);
                rec.SpriteId = MacToolbox.ReadResourceShort(resHandle, 0x4);
                rec.System = (short)(MacToolbox.ReadResourceShort(resHandle, 0xa) - 128);
                rec.Flags = MacToolbox.ReadResourceInt(resHandle, 0x6);
                rec.Govt = MacToolbox.ReadResourceShort(resHandle, 0x14);
                if (rec.Govt != -1)
                    rec.Govt = (short)(rec.Govt - 128);
                rec.MinCoolness = MacToolbox.ReadResourceShort(resHandle, 0x16);
                rec.TechLevel = MacToolbox.ReadResourceShort(resHandle, 0xc);
                rec.CustomPicId = MacToolbox.ReadResourceShort(resHandle, 0x18);
                rec.CustomSoundId = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                for (short field = 0; field < rec.SpecialTech.Length; field++)
                    rec.SpecialTech[field] = MacToolbox.ReadResourceShort(resHandle, field * 2 + 0xe);
                rec.Visible = 1;
                rec.TradingEnabled = 0;
                rec.TributeMax = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                rec.DefenseDude = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                if (rec.TributeMax < 1001)
                    rec.Tribute = rec.TributeMax;
                else if (rec.TributeMax < 10001)
                    rec.Tribute = (short)(rec.TributeMax / 10 - 100);
                else
                    rec.Tribute = (short)(rec.TributeMax / 10 - 1000);
                if (rec.DefenseDude > 0)
                    rec.DefenseDude = (short)(rec.DefenseDude - 128);
                if (rec.TechLevel < 1)
                    rec.TechLevel = 1;

                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
            if (resCount <= loadedCount) break;
        }
    }
}
