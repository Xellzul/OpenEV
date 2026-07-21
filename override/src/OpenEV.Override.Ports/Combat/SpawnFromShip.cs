using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10068b10 (EV Override-11.c lines 43452-43686).
public static class SpawnFromShip
{
    public static void Run(int sourceSlotArg, short targetSlot, int weaponIndexArg)
    {
        // Find a free projectile slot (life <= -2).
        short projSlot = -1;
        for (short slot = 0; slot < ProjectileTable.Count; slot++)
        {
            if (GameData.Projectiles[slot].LifeRemaining <= ProjectileRecord.Killed)
            {
                projSlot = slot;
                break;
            }
        }
        if (projSlot != -1)
        {
            bool usesTargeting = false;
            short spreadRoll = 0;
            var projectilePtr = AllocateSpriteRecord.Run(3, 0, 0, 0);
            if (projectilePtr != 0)
            {
                var node = SpriteNodes.At(projectilePtr);
                node.UpdateUpp = SpriteNodeUppCells.ProjectileUpdateUpp;
                node.CollisionUpp = SpriteNodeUppCells.ProjectileDrawUpp;
                node.TeardownUpp = 0;
                node.UpdaterPayload = projSlot;
                node.ObjectPtr = 0;
                node.SpritePtr = 0;
                short sourceSlot = (short)sourceSlotArg;
                var proj = GameData.Projectiles[projSlot];
                var src = GameData.Ships[sourceSlot];
                proj.PosX = src.PosX;
                proj.PosY = src.PosY;
                proj.VelX = src.VelX;
                proj.VelY = src.VelY;
                short launchAngle = src.Heading;
                short weaponIndex = (short)weaponIndexArg;
                var weapon = GameData.Weapons[weaponIndex];
                var guidance = (WeaponGuidanceType)weapon.GuidanceType;
                if (guidance == WeaponGuidanceType.TurretedUnguided ||
                    guidance == WeaponGuidanceType.FrontQuadrantTurret ||
                    guidance == WeaponGuidanceType.RearQuadrantTurret)
                {
                    // Cycle the ship's turret-mount index 0..3, offsetting the spawn
                    // position by the class's matching TurretYDisp mount.
                    uint ammoWrap = (uint)src.TurretMountCycle + 1;
                    src.TurretMountCycle = (short)(ammoWrap + (((int)ammoWrap >> 2) + ((ammoWrap & 3) != 0 && ((int)ammoWrap < 0) ? 1u : 0u)) * -4);
                    var cls = GameData.ShipClasses[src.ShipClass];
                    short turretDisp = src.TurretMountCycle switch
                    {
                        0 => cls.TurretYDisp0,
                        1 => cls.TurretYDisp1,
                        2 => cls.TurretYDisp2,
                        _ => cls.TurretYDisp3
                    };
                    EvMath.OffsetByHeading((double)(float)turretDisp,
                         src.Heading, ref proj.PosX, ref proj.PosY);
                    if (targetSlot == -1)
                    {
                        if (guidance == WeaponGuidanceType.FrontQuadrantTurret)
                        {
                            launchAngle = src.Heading;
                        }
                    }
                    else
                    {
                        launchAngle = (short)LeadTargetAngle.Run(ShipTable.Ships[sourceSlot], ShipTable.Ships[targetSlot],
                                           (short)weaponIndexArg);
                    }
                    node.SortKey = 12;
                }
                else
                {
                    node.SortKey = 4;
                }
                if (guidance == WeaponGuidanceType.UnguidedProjectile)
                {
                    spreadRoll = (short)WeaponShotSpread.Run(ShipTable.Ships[sourceSlotArg], (short)weaponIndexArg);
                    if (spreadRoll == 0)
                    {
                        usesTargeting = true;
                    }
                    else
                    {
                        usesTargeting = false;
                    }
                }
                if (guidance == WeaponGuidanceType.HomingWeapon)
                {
                    spreadRoll = 0;
                    usesTargeting = true;
                }
                if (sourceSlot == 0)
                {
                    if (guidance != WeaponGuidanceType.TurretedUnguided &&
                        guidance != WeaponGuidanceType.FrontQuadrantTurret &&
                        guidance != WeaponGuidanceType.RearQuadrantTurret)
                    {
                        proj.VelX = GameData.Ships[0].VelX;
                        proj.VelY = GameData.Ships[0].VelY;
                    }
                    if (guidance == WeaponGuidanceType.FreefallBomb)
                    {
                        proj.VelX *= ShipStatConstants.VelocityFieldScale;
                        proj.VelY *= ShipStatConstants.VelocityFieldScale;
                    }
                }
                proj.WeaponType = weaponIndex;
                proj.OwnerSlot = sourceSlot;
                proj.TargetSlot = targetSlot;
                proj.FromGuardingEscort = 0;
                proj.LifeRemaining = (short)(int)(weapon.Lifetime / WorldState.TimeScale);
                if (((WeaponFlags)weapon.Flags & WeaponFlags.CyclingStartsOnFirstFrame) == 0)
                {
                    proj.AnimFrame = (short)SeedEvoRng.Run(36);
                }
                else
                {
                    proj.AnimFrame = 0;
                }
                proj.DamageFalloffSteps = 0;
                if (weapon.AnimationRate < 1)
                {
                    proj.DamageFalloffTimer = -1;
                }
                else
                {
                    proj.DamageFalloffTimer = 0;
                }
                if (sourceSlot != 0 && ShipAi.IsStateGuardingParent(ShipTable.Ships[sourceSlot]))
                {
                    proj.FromGuardingEscort = 1;
                }
                if (sourceSlot != 0 && GameData.Ships[sourceSlot].OwnerSlot != 0)
                {
                    proj.LifeRemaining = (short)(int)(proj.LifeRemaining * ShipStatConstants.EscortShotLifeScale);
                }
                proj.SystemId = src.CurrentSystem;
                proj.Heading = launchAngle;
                proj.Mode = 0;
                short frameWidth = (short)MacRectWidth.Run(WeaponDefTable.Store[weapon.SpriteIndex * 36]);
                short frameHeight = (short)MacRectHeight.Run(WeaponDefTable.Store[weapon.SpriteIndex * 36]);
                node.ExtentTop = 0;
                node.ExtentLeft = 0;
                node.ExtentRight = frameWidth;
                node.ExtentBottom = frameHeight;
                if (guidance == WeaponGuidanceType.HomingWeapon &&
                    ((WeapSeekerFlags)(ushort)weapon.SeekerFlags & WeapSeekerFlags.ConfusedBySensorInterference) != 0)
                {
                    short randomRoll = (short)SeedEvoRng.Run(100);
                    if (randomRoll + 1 <= SystTable.Store[GameData.Ships[0].CurrentSystem].Interference)
                    {
                        proj.Mode = 999;   // jammed at launch by system interference
                    }
                }
                if (0 < weapon.ShotOffset)
                {
                    node.ExtentTop = (short)(node.ExtentTop - weapon.ShotOffset);
                    node.ExtentLeft = (short)(node.ExtentLeft - weapon.ShotOffset);
                    node.ExtentRight = (short)(node.ExtentRight + weapon.ShotOffset);
                    node.ExtentBottom = (short)(node.ExtentBottom + weapon.ShotOffset);
                }
                if (spreadRoll == 0)
                {
                    if (usesTargeting)
                    {
                        // Offset the spawn point half the firing ship's sprite width
                        // along the launch heading (out of the nose).
                        spreadRoll = (short)MacRectWidth.Run(WeaponGraphicsTable.Store[src.ShipClass * 36 + launchAngle / 10]);
                        EvMath.OffsetByHeading(
                             (double)(float)(short)(int)(ShipStatConstants.Half * spreadRoll),
                             proj.Heading, ref proj.PosX, ref proj.PosY);
                    }
                }
                else
                {
                    EvMath.OffsetByHeading((double)(float)spreadRoll,
                         (proj.Heading + 90) % 360, ref proj.PosX, ref proj.PosY);
                }
                if (0 < weapon.Inaccuracy && guidance != WeaponGuidanceType.FreefallBomb)
                {
                    spreadRoll = (short)SeedEvoRng.Run((short)(weapon.Inaccuracy << 1));
                    proj.Heading += (short)(spreadRoll - weapon.Inaccuracy);
                }
                if (guidance != WeaponGuidanceType.FreeflightRocket || sourceSlot != 0)
                {
                    EvMath.OffsetByHeading((double)weapon.ProjectileSpeed, proj.Heading, ref proj.VelX, ref proj.VelY);
                }
                if (0 < weapon.Inaccuracy && guidance == WeaponGuidanceType.FreefallBomb)
                {
                    spreadRoll = (short)SeedEvoRng.Run((short)(weapon.Inaccuracy << 1));
                    proj.Heading += (short)(spreadRoll - weapon.Inaccuracy);
                }
                if (((WeaponFlags)weapon.Flags & WeaponFlags.ActsAsMissileDecoy) == 0)
                {
                    node.State = -1;
                    node.UpdaterFlag = -1;
                }
                else
                {
                    node.State = 2;
                    node.UpdaterFlag = 5;
                }
                if (weapon.AmmoLink == -999 && sourceSlot != -1)
                {
                    // -32000 is the self-destruct/disabled-armor marker. Shield must hold it as
                    // a numeric value (not a bit-pattern reinterpret) — it's read back via
                    // (int)Shield elsewhere.
                    GameData.Ships[sourceSlot].Shield = -32000f;
                    WorldState.WeaponSlotDirty = 1;
                }
            }
        }
    }
}
