using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Combat.Model;

namespace OpenEV.Override.Ports.Resource;

// Extracted from LoadSpobAndStellarResources — loads 'wëap' resources into
// GameData.Weapons[]. Faithful 1:1 with FUN_10015e70, EV Override-11.c lines 11164-11229.
public static class LoadWeaponResources
{
    public static void Run()
    {
        for (int loopIdx = 0; loopIdx < WeaponTable.Count; loopIdx++)
        {
            int resHandle = MacToolbox.GetResource(MacResType.Weapon, loopIdx + 128);
            if (resHandle != 0)
            {
                MacToolbox.HNoPurge(resHandle);
                var rec = GameData.Weapons[loopIdx];
                rec.ReloadTime = MacToolbox.ReadResourceShort(resHandle, 0);
                rec.Lifetime = MacToolbox.ReadResourceShort(resHandle, 2);
                rec.MassDamage = MacToolbox.ReadResourceShort(resHandle, 4);
                rec.EnergyDamage = MacToolbox.ReadResourceShort(resHandle, 6);
                rec.GuidanceType = MacToolbox.ReadResourceShort(resHandle, 8);
                rec.ProjectileSpeed = (float)(MacToolbox.ReadResourceShort(resHandle, 10) / 100.0);   // SpeedDivisor toc-0x6930
                rec.AmmoLink = MacToolbox.ReadResourceShort(resHandle, 0xc);
                rec.SpriteIndex = MacToolbox.ReadResourceShort(resHandle, 0xe);
                rec.Inaccuracy = MacToolbox.ReadResourceShort(resHandle, 0x10);
                rec.FireSound = MacToolbox.ReadResourceShort(resHandle, 0x12);
                rec.ImpactDamage = MacToolbox.ReadResourceShort(resHandle, 0x14);
                rec.ExplosionType = MacToolbox.ReadResourceShort(resHandle, 0x16);
                rec.ShotOffset = MacToolbox.ReadResourceShort(resHandle, 0x18);
                rec.Submunitions = MacToolbox.ReadResourceShort(resHandle, 0x1a);
                rec.Flags = MacToolbox.ReadResourceShort(resHandle, 0x1c);
                rec.SeekerFlags = MacToolbox.ReadResourceShort(resHandle, 0x1e);
                WeaponNameBuffer.Names[loopIdx] = MacToolbox.GetResInfo(resHandle);

                uint handleSize = (uint)MacToolbox.GetHandleSize(resHandle);
                if (handleSize < 52)
                {
                    rec.TrailSmokeSet = 0;
                    rec.AnimationRate = 0;
                }
                else
                {
                    rec.TrailSmokeSet = MacToolbox.ReadResourceShort(resHandle, 0x20);
                    rec.AnimationRate = MacToolbox.ReadResourceShort(resHandle, 0x22);
                }
                if (7 < (ushort)rec.TrailSmokeSet)
                {
                    rec.TrailSmokeSet = 0;
                }
                if (rec.GuidanceType == 2)
                {
                    rec.GuidanceType = 1;
                    rec.SeekerFlags = 0x30fe;
                }
                MacToolbox.HPurge(resHandle);
                MacToolbox.ReleaseResource(resHandle);
            }
        }
    }
}
