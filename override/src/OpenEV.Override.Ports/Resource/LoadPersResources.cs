using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources (FUN_10015e70, EV Override-11.c 10694-11567) —
// loads the 511 'përs' resources (MacResType.Person, ids 0x80..) into PersTable.Store,
// then hand-builds slot 511: Cap'n Hector, the Ambrosia-mascot special pers.
public static class LoadPersResources
{
    public static void Run()
    {
        short[] bankSlotType = new short[4];
        short[] bankWeaponType = new short[4];
        short[] bankWeaponAmmo = new short[4];

        for (int loopIdx = 0; loopIdx < 511; loopIdx++)
        {
            var rec = GameData.Pers[loopIdx];
            rec.AvailableFlag = 0;
            rec.AppearGate = ShipAiType.Inactive;
            int resHandle = MacToolbox.GetResource(MacResType.Person, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                rec.LinkSyst = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.Govt = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.AppearGate = (ShipAiType)MacToolbox.ReadResourceShort(resHandle, 4);
                rec.AiCourage = MacToolbox.ReadResourceShort(resHandle, 6);
                rec.Coward = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.ShipType = (short)(MacToolbox.ReadResourceShort(resHandle, 10) - 128);
                rec.Credits = MacToolbox.ReadResourceInt(resHandle, 0x24);
                // PPC int->double magic idiom: short field / 100.0 is an exact (double)x cast.
                rec.ShieldMultiplier = (float)((double)MacToolbox.ReadResourceShort(resHandle, 0x28) / 100.0);
                rec.CommQuote = MacToolbox.ReadResourceShort(resHandle, 0x2c);
                rec.HailQuote = MacToolbox.ReadResourceShort(resHandle, 0x2e);
                rec.LinkMission = MacToolbox.ReadResourceShort(resHandle, 0x30);
                rec.Flags = MacToolbox.ReadResourceShort(resHandle, 0x32);
                rec.AvailabilityBit = MacToolbox.ReadResourceShort(resHandle, 0x2a);
                if (rec.LinkMission < 128)
                    rec.LinkMission = -1;
                else
                    rec.LinkMission = (short)(rec.LinkMission - 128);
                if (rec.Govt != -1)
                    rec.Govt = (short)(rec.Govt - 128);
                bankSlotType[1] = MacToolbox.ReadResourceShort(resHandle, 0xe);
                bankSlotType[0] = MacToolbox.ReadResourceShort(resHandle, 0xc);
                bankSlotType[3] = MacToolbox.ReadResourceShort(resHandle, 0x12);
                bankSlotType[2] = MacToolbox.ReadResourceShort(resHandle, 0x10);
                bankWeaponType[1] = MacToolbox.ReadResourceShort(resHandle, 0x16);
                bankWeaponType[0] = MacToolbox.ReadResourceShort(resHandle, 0x14);
                bankWeaponType[3] = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                bankWeaponType[2] = MacToolbox.ReadResourceShort(resHandle, 0x18);
                bankWeaponAmmo[1] = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                bankWeaponAmmo[0] = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                bankWeaponAmmo[3] = MacToolbox.ReadResourceShort(resHandle, 0x22);
                bankWeaponAmmo[2] = MacToolbox.ReadResourceShort(resHandle, 0x20);
                for (int slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
                {
                    rec.WeaponType[slot] = 0;
                    rec.WeaponAmmo[slot] = 0;
                }
                // The 4 weapon banks scatter into the 64-slot arrays by (type - 128).
                for (int bank = 0; bank < 4; bank++)
                {
                    if (127 < bankSlotType[bank])
                    {
                        rec.WeaponType[bankSlotType[bank] - 128] = bankWeaponType[bank];
                        rec.WeaponAmmo[bankSlotType[bank] - 128] = bankWeaponAmmo[bank];
                    }
                }
                rec.AvailableFlag = 1;
                MacToolbox.GetResInfo(resHandle, rec.Name, 30);
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
        // Slot 511 = Cap'n Hector (pers 0x1ff), hand-built: Rapier-ish loadout with
        // forward cannon (slot 8) + two specials (slots 12/14).
        var hector = GameData.Pers[511];
        hector.LinkSyst = -1;
        hector.Govt = -1;
        hector.AppearGate = ShipAiType.Warship;
        hector.AiCourage = 4;
        hector.Coward = -1;
        hector.ShipType = 15;
        hector.Credits = 0;
        hector.ShieldMultiplier = 10.0f;   // src *(float*)(toc-0x6948)=10.0f (direct float load, not the int-magic idiom)
        hector.CommQuote = 8;
        hector.HailQuote = -1;
        hector.LinkMission = -1;
        hector.Flags = 2;
        hector.AvailabilityBit = -1;
        hector.WeaponType[8] = (short)(hector.WeaponType[8] + 2);
        hector.WeaponAmmo[8] = (short)(hector.WeaponAmmo[8] + 100);
        hector.WeaponType[14] = (short)(hector.WeaponType[14] + 4);
        hector.WeaponType[12] = (short)(hector.WeaponType[12] + 2);
        hector.WeaponAmmo[12] = (short)(hector.WeaponAmmo[12] + 250);
        hector.AvailableFlag = 1;
        MacToolbox.WritePascalString(hector.Name, "Cap'n Hector", 30);
    }
}
