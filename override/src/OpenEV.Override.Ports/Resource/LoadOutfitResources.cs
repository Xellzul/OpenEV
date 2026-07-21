using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10901-10958) —
// loads 'oütf' resources into the typed managed OutfitTable.Store[]. (The decompile stashes
// the resource handle in a scratch stack slot toc-0x15c8; here it is just a local.)
public static class LoadOutfitResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < OutfitTable.Count; loopIdx++)
        {
            var rec = OutfitTable.Store[loopIdx];
            int resHandle = MacToolbox.GetResource(MacResType.Outfit, loopIdx + 128);
            if (resHandle == 0)
            {
                rec.TechLevel = 32767;
                rec.ModType[0] = 0;
                rec.ModType[1] = 0;
                rec.ModValue[0] = 0;
                rec.ModValue[1] = 0;
                continue;
            }

            MacToolbox.HNoPurge(resHandle);
            rec.TechLevel = MacToolbox.ReadResourceShort(resHandle, 0x4);
            rec.Cost = MacToolbox.ReadResourceInt(resHandle, 0xe);
            rec.Mass = MacToolbox.ReadResourceShort(resHandle, 0x2);
            rec.ModType[0] = (OutfitModType)MacToolbox.ReadResourceShort(resHandle, 0x6);
            rec.ModValue[0] = MacToolbox.ReadResourceShort(resHandle, 0x8);
            rec.MaximumCount = MacToolbox.ReadResourceShort(resHandle, 0xa);
            rec.AvailabilityBit = MacToolbox.ReadResourceShort(resHandle, 0);
            short flags = MacToolbox.ReadResourceShort(resHandle, 0xc);
            rec.Flags = (OutfFlags)flags;
            rec.PersistentFlagSet = (byte)((rec.Flags & OutfFlags.Persistent) != 0 ? 1 : 0);

            // ASM: li r5,0x3F before sub_76178, copying a 63-byte Pascal buffer (byte 0 =
            // length prefix) — real character capacity is 63-1 = 62.
            string name = MacToolbox.GetResInfo(resHandle);
            rec.Name = name.Length > 62 ? name.Substring(0, 62) : name;

            // The second mod (type/value) is only present in larger 'oütf' resources.
            if ((uint)MacToolbox.GetHandleSize(resHandle) < 38)
            {
                rec.ModType[1] = OutfitModType.Invalid;
                rec.ModValue[1] = -1;
            }
            else
            {
                rec.ModType[1] = (OutfitModType)MacToolbox.ReadResourceShort(resHandle, 0x12);
                rec.ModValue[1] = MacToolbox.ReadResourceShort(resHandle, 0x14);
            }

            // Mod types 1/3/21 with a value >= 128 are stored biased by 128.
            for (short field = 0; field < OutfitRecord.ModBankCount; field++)
            {
                short mt = (short)rec.ModType[field];
                if ((mt == 1 || mt == 3 || mt == 21) && 127 < rec.ModValue[field])
                    rec.ModValue[field] = (short)(rec.ModValue[field] - 128);
            }

            MacToolbox.HPurge(resHandle);
            MacToolbox.ReleaseResource(resHandle);
        }
    }
}
