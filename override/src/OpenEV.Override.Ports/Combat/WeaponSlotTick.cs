using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1002f710 (EV Override-11.c lines 19615-19803).
public static class WeaponSlotTick
{
    public static void Run(int weaponSlotArg)
    {
        short weaponSlot = (short)weaponSlotArg;
        var s = ShipTable.Player;
        if (s.WeaponSlotReload[weaponSlot] <= 0.0f && s.JumpWindupTimer == 0)
        {
            short shotCount = 0;
            var weapon = GameData.Weapons[weaponSlot];
            var guidance = (WeaponGuidanceType)weapon.GuidanceType;

            short shotsPerBurst;
            if (((WeaponFlags)weapon.Flags & WeaponFlags.FiresFromAllMatchingSlots) == 0)
            {
                shotsPerBurst = 1;
            }
            else
            {
                shotsPerBurst = s.WeaponSlotType[weaponSlot];
            }

            for (short burstIndex = 0; burstIndex < shotsPerBurst; burstIndex++)
            {
                bool fired = false;
                if (ShipDerivedStats.CanFireWeapon(s, weaponSlot))
                {
                    if (guidance == WeaponGuidanceType.UnguidedProjectile || guidance == WeaponGuidanceType.HomingWeapon ||
                        guidance == WeaponGuidanceType.FreefallBomb || guidance == WeaponGuidanceType.FreeflightRocket)
                    {
                        SpawnFromShip.Run(0, s.TargetSlot, weaponSlotArg);
                        shotCount++;
                        fired = true;
                    }
                    if (guidance == WeaponGuidanceType.BeamWeapon)
                    {
                        int angle = WeaponShotSpread.Run(s, weaponSlot);
                        AllocateBeamSlot.Run(0, s.TargetSlot, weaponSlot, s.Heading,
                                   (byte)((short)angle == 0 ? 1 : 0), (short)angle);
                        shotCount++;
                        fired = true;
                    }
                    if (guidance == WeaponGuidanceType.TurretedBeam && s.TargetSlot != -1 &&
                        !IsTurretBlindToTarget.Run(s, ShipTable.Ships[s.TargetSlot], weaponSlot))
                    {
                        var t = ShipTable.Ships[s.TargetSlot];
                        int angle = EvMath.HeadingBetween(s.PosX, s.PosY, t.PosX, t.PosY);
                        AllocateBeamSlot.Run(0, s.TargetSlot, weaponSlot, angle, 0, 0);
                        shotCount++;
                        fired = true;
                    }
                    if (guidance == WeaponGuidanceType.TurretedUnguided && s.TargetSlot != -1 &&
                        !IsTurretBlindToTarget.Run(s, ShipTable.Ships[s.TargetSlot], weaponSlot))
                    {
                        SpawnFromShip.Run(0, s.TargetSlot, weaponSlotArg);
                        shotCount++;
                        fired = true;
                    }
                    if (guidance == WeaponGuidanceType.FrontQuadrantTurret || guidance == WeaponGuidanceType.RearQuadrantTurret)
                    {
                        if (s.TargetSlot == -1)
                        {
                            if (guidance == WeaponGuidanceType.FrontQuadrantTurret)
                            {
                                SpawnFromShip.Run(0, -1, weaponSlotArg);
                                shotCount++;
                                fired = true;
                            }
                        }
                        else
                        {
                            var t = ShipTable.Ships[s.TargetSlot];
                            short bearing = (short)EvMath.HeadingBetween(s.PosX, s.PosY, t.PosX, t.PosY);
                            // Always written below before the read at "if (angleDelta < 46)": guidance is
                            // Front or Rear here, so exactly one of the two ifs runs, but the compiler
                            // can't see that exclusivity, hence the initializer.
                            int angleDelta = default;
                            if (guidance == WeaponGuidanceType.FrontQuadrantTurret)
                            {
                                int diff = bearing - s.Heading;
                                // Cast to uint AFTER the shift: shifting a uint here would be a logical
                                // shift and break the abs-value idiom below.
                                uint signMask = (uint)(diff >> 0x1f);
                                angleDelta = (int)((signMask ^ (uint)diff) - signMask) % 360;
                            }
                            if (guidance == WeaponGuidanceType.RearQuadrantTurret)
                            {
                                // The % binds to (s.Heading + 180) only -- the difference itself isn't
                                // additionally wrapped here (that's the abs-mod step below).
                                uint diff = (uint)(bearing - (s.Heading + 180) % 360);
                                uint signMask = (uint)((int)diff >> 0x1f);
                                angleDelta = (int)((signMask ^ diff) - signMask) % 360;
                            }
                            if ((short)angleDelta < 46)
                            {
                                SpawnFromShip.Run(0, s.TargetSlot, weaponSlotArg);
                                shotCount++;
                                fired = true;
                            }
                            else if (guidance == WeaponGuidanceType.FrontQuadrantTurret)
                            {
                                SpawnFromShip.Run(0, -1, weaponSlotArg);
                                shotCount++;
                                fired = true;
                            }
                        }
                    }
                    if (guidance == WeaponGuidanceType.CarriedShip && SpawnSpecialWeaponShip.Run(s, weaponSlot) != 0)
                    {
                        shotCount++;
                        fired = true;
                    }
                    if (fired)
                    {
                        if (s.AltFireSide < 0)
                        {
                            s.AltFireSide = 1;
                        }
                        else
                        {
                            s.AltFireSide = -1;
                        }
                        if (weapon.AmmoLink != -1)
                        {
                            if (weapon.AmmoLink < 1)
                            {
                                if (weapon.AmmoLink < -999)
                                {
                                    uint rawAmmoLink = (uint)weapon.AmmoLink;
                                    uint signMask = (uint)((int)rawAmmoLink >> 0x1f);
                                    s.Fuel -= (int)(((signMask ^ rawAmmoLink) - signMask) - 1000u);
                                    if (s.Fuel < 0.0f)
                                    {
                                        s.Fuel = 0.0f;
                                    }
                                    WorldState.ShieldEnergyBarDirty = 1;
                                }
                            }
                            else
                            {
                                s.WeaponSlotAmmo[weaponSlot]--;
                                if (s.WeaponSlotAmmo[weaponSlot] < 0)
                                {
                                    s.WeaponSlotAmmo[weaponSlot] = 0;
                                }
                                WorldState.HudWeaponPanelDirty = 1;
                            }
                        }
                    }
                }
            }

            if (0 < shotCount)
            {
                bool playFireSound = true;
                if (((WeaponFlags)weapon.Flags & WeaponFlags.FireSoundLoopedSingleInstance) != 0 &&
                    weapon.FireSound >= 0 &&
                    CountMatchingSoundVoices.Run(CombatSoundCells.WeaponSoundTable[weapon.FireSound]) != 0)
                {
                    playFireSound = false;
                }
                if (weapon.FireSound >= 0 && playFireSound &&
                    (guidance == WeaponGuidanceType.BeamWeapon || guidance == WeaponGuidanceType.TurretedBeam))
                {
                    // src = listener = player (faithful -- both args are the player record).
                    PlayPositionalSound.Run(2, CombatSoundCells.WeaponSoundTable[weapon.FireSound], 6,
                               s.PosX, s.PosY, s.PosX, s.PosY);
                    playFireSound = false;
                }
                if (weapon.FireSound < 0)
                {
                    playFireSound = false;
                }
                if (playFireSound)
                {
                    int fireSoundHandle = CombatSoundCells.WeaponSoundTable[weapon.FireSound];
                    if (guidance == WeaponGuidanceType.CarriedShip)
                    {
                        PlayPositionalSound.Run(-1, fireSoundHandle, 6, s.PosX, s.PosY, s.PosX, s.PosY);
                    }
                    else if (((WeaponFlags)weapon.Flags & WeaponFlags.FiresOnSecondaryTrigger) == 0)
                    {
                        PlayPositionalSound.Run(-1, fireSoundHandle, 5, s.PosX, s.PosY, s.PosX, s.PosY);
                    }
                    else
                    {
                        PlayPositionalSound.Run(-1, fireSoundHandle, 6, s.PosX, s.PosY, s.PosX, s.PosY);
                    }
                }
                s.WeaponSlotReload[weaponSlot] = shotCount * (weapon.ReloadTime / s.WeaponSlotType[weaponSlot]);
            }
        }
    }
}
