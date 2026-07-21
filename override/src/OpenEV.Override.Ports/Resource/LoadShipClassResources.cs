using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources — loads 'shïp' resources into
// GameData.ShipClasses[] (managed). Faithful 1:1 with FUN_10015e70,
// EV Override-11.c lines 10959-11163.
public static class LoadShipClassResources
{
    // Ship-stat scale constants — data-seg double/float values installed by
    // GameBootSequence at GameToc-0x6928.. ; inlined here as managed literals (the
    // cells are still written, for other readers).
    private const double AccelDivisor = 10000.0;     // toc-0x6928 (ship +0x04 → Accel)
    private const double SpeedDivisor = 100.0;       // toc-0x6930 (ship +0x06 → Speed)
    private const float SpriteScaleNoFlag = 1.0f;    // toc-0x6944 (no size flag)
    private const float SpriteScaleFlag4 = 1.6f;     // toc-0x6940 (Flags & 4)
    private const float SpriteScaleFlag2 = 1.3f;     // toc-0x693c (Flags & 2) — also the final multiplier
    private const float SpriteScaleFlag1 = 0.7f;     // toc-0x6938 (Flags & 1)

    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < ShipClassTable.Count; loopIdx++)
        {
            var rec = GameData.ShipClasses[loopIdx];
            int resHandle = MacToolbox.GetResource(MacResType.Ship, loopIdx + 128);
            if (resHandle == 0)
            {
                rec.TechLevel = 9999;
                rec.Shield = 1;
            }
            else
            {
                short[] slotType = new short[ShipClassRecord.WeaponSlotDefaultCount];
                short[] slotVal1 = new short[ShipClassRecord.WeaponSlotDefaultCount];
                short[] slotVal2 = new short[ShipClassRecord.WeaponSlotDefaultCount];

                MacToolbox.HNoPurge(resHandle);
                rec.Holds = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.Shield = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.Accel = (float)(MacToolbox.ReadResourceShort(resHandle, 4) / AccelDivisor);
                rec.Speed = (float)(MacToolbox.ReadResourceShort(resHandle, 6) / SpeedDivisor);
                rec.Maneuver = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.BaseFuel = MacToolbox.ReadResourceShort(resHandle, 10);
                rec.FreeMass = MacToolbox.ReadResourceShort(resHandle, 0xc);
                rec.BaseArmor = MacToolbox.ReadResourceShort(resHandle, 0xe);
                rec.ShieldRecharge = MacToolbox.ReadResourceShort(resHandle, 0x10);
                rec.MaxGun = MacToolbox.ReadResourceShort(resHandle, 0x2a);
                rec.MaxTur = MacToolbox.ReadResourceShort(resHandle, 0x2c);
                rec.TechLevel = MacToolbox.ReadResourceShort(resHandle, 0x2e);
                rec.Cost = MacToolbox.ReadResourceInt(resHandle, 0x30);
                rec.DeathDelay = MacToolbox.ReadResourceShort(resHandle, 0x34);
                rec.Mass = MacToolbox.ReadResourceShort(resHandle, 0x3e);
                rec.Length = MacToolbox.ReadResourceShort(resHandle, 0x40);
                rec.InherentAI = (ShipAiType)MacToolbox.ReadResourceShort(resHandle, 0x42);
                rec.Crew = MacToolbox.ReadResourceShort(resHandle, 0x44);
                rec.MissionBit = MacToolbox.ReadResourceShort(resHandle, 0x46);
                rec.InherentGovt = MacToolbox.ReadResourceShort(resHandle, 0x48);
                rec.Flags = (ShipFlags)MacToolbox.ReadResourceShort(resHandle, 0x4a);

                if (rec.Holds < 0)
                {
                    rec.Holds = (short)(-rec.Holds);
                    rec.NegativeHoldsFlag = 0;
                }
                else
                {
                    rec.NegativeHoldsFlag = 1;
                }
                if (rec.Shield < 0)
                {
                    rec.Shield = rec.Shield * -5;
                }
                if (rec.Crew < 1)
                {
                    rec.Crew = 1;
                }
                if (rec.InherentGovt < 128)
                {
                    rec.InherentGovt = -1;
                }
                else
                {
                    rec.InherentGovt = (short)(rec.InherentGovt - 128);
                }

                rec.TurretYDisp0 = MacToolbox.ReadResourceShort(resHandle, 0x36);
                rec.TurretYDisp1 = MacToolbox.ReadResourceShort(resHandle, 0x38);
                rec.TurretYDisp2 = MacToolbox.ReadResourceShort(resHandle, 0x3a);
                rec.TurretYDisp3 = MacToolbox.ReadResourceShort(resHandle, 0x3c);

                for (int slot = 0; slot < ShipClassRecord.WeaponSlotDefaultCount; slot++)
                {
                    slotType[slot] = MacToolbox.ReadResourceShort(resHandle, 0x12 + (2 * slot));
                    slotVal1[slot] = MacToolbox.ReadResourceShort(resHandle, 0x1a + (2 * slot));
                    slotVal2[slot] = MacToolbox.ReadResourceShort(resHandle, 0x22 + (2 * slot));
                }

                uint handleSize = (uint)MacToolbox.GetHandleSize(resHandle);
                if (handleSize < 114)
                {
                    for (int field = 0; field < ShipClassRecord.DefaultItemSlots; field++)
                    {
                        rec.DefaultItems[field] = -1;
                        rec.DefaultItemsCount[field] = 0;
                    }
                    rec.FuelRegen = 0;
                    rec.SkillLevel = 10;
                }
                else
                {
                    for (int i = 0; i < ShipClassRecord.DefaultItemSlots; i++)
                    {
                        rec.DefaultItems[i] = MacToolbox.ReadResourceShort(resHandle, 0x4e + (2 * i));
                        rec.DefaultItemsCount[i] = MacToolbox.ReadResourceShort(resHandle, 0x56 + (2 * i));
                    }

                    for (int field = 0; field < ShipClassRecord.DefaultItemSlots; field++)
                    {
                        short slotField = rec.DefaultItems[field];
                        if (slotField < 128)
                        {
                            rec.DefaultItems[field] = -1;
                            rec.DefaultItemsCount[field] = 0;
                        }
                        else
                        {
                            rec.DefaultItems[field] = (short)(slotField - 128);
                        }
                    }
                    rec.FuelRegen = MacToolbox.ReadResourceShort(resHandle, 0x5e);
                    rec.SkillLevel = MacToolbox.ReadResourceShort(resHandle, 0x60);
                    if (rec.SkillLevel < 1)
                    {
                        rec.SkillLevel = 1;
                    }
                    if (50 < rec.SkillLevel)
                    {
                        rec.SkillLevel = 50;
                    }
                }

                for (int slot = 0; slot < ShipClassRecord.WeaponSlotDefaultCount; slot++)
                {
                    if (127 < slotType[slot])
                    {
                        rec.DefaultWeaponType[slotType[slot] - 128] = slotVal1[slot];
                        rec.DefaultWeaponAmmo[slotType[slot] - 128] = slotVal2[slot];
                        foreach (var outfitRec in OutfitTable.Store)
                        {
                            for (int bank = 0; bank < OutfitRecord.ModBankCount; bank++)
                            {
                                if (outfitRec.ModType[bank] == OutfitModType.Weapon &&
                                    0 < slotVal1[slot] &&
                                    outfitRec.ModValue[bank] == slotType[slot] - 128)
                                {
                                    rec.FreeMass = (short)(rec.FreeMass + outfitRec.Mass * slotVal1[slot]);
                                }
                                if (outfitRec.ModType[bank] == OutfitModType.Ammo &&
                                    0 < slotVal2[slot] &&
                                    outfitRec.ModValue[bank] == slotType[slot] - 128)
                                {
                                    rec.FreeMass = (short)(rec.FreeMass + outfitRec.Mass * slotVal2[slot]);
                                }
                            }
                        }
                    }
                }

                if (((ushort)rec.Flags & 1) == 0)
                {
                    if (((ushort)rec.Flags & 2) == 0)
                    {
                        if (((ushort)rec.Flags & 4) == 0)
                            rec.SpriteScale = SpriteScaleNoFlag;
                        else
                            rec.SpriteScale = SpriteScaleFlag4;
                    }
                    else
                    {
                        rec.SpriteScale = SpriteScaleFlag2;
                    }
                }
                else
                {
                    rec.SpriteScale = SpriteScaleFlag1;
                }
                if (((ushort)rec.Flags & 0x80) == 0)
                {
                    rec.ShotXOffset = 0;
                }
                else
                {
                    rec.ShotXOffset = MacToolbox.ReadResourceShort(resHandle, 0x4c);
                }
                rec.SpriteScale = rec.SpriteScale * SpriteScaleFlag2;
                string name = MacToolbox.GetResInfo(resHandle);
                // FUN_10076178(rec+0x3e, name, 0x3f): the source is GetResInfo's raw Str255 out-param,
                // so byte 0 of the copy is the Pascal length count, not a character — it consumes one
                // of the 63-byte budget, leaving 62 real characters (0x3e) as the true faithful cap.
                rec.Name = name.Length > 0x3e ? name.Substring(0, 0x3e) : name;
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
