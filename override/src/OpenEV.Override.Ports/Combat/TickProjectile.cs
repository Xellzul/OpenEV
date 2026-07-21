using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_100269a4 (EV Override-11.c lines 16531-16827). Per-frame tick of one
// projectile sprite node — node.UpdaterPayload holds the projectile slot. Live shots
// (in the player's system, life > 0) age the life by 1.0/TimeScale, run the guidance
// AI, integrate pos += vel*TimeScale, pick the sprite frame, stamp the frame rect +
// screen coords into the node, and optionally spawn a streak trail. Dead shots (life
// exhausted, or the shot left the system) spawn the weapon's death explosion — plus a
// type-2 spark-ring shower and blast-radius ship damage for an area-blast weapon —
// then free the slot.
public static class TickProjectile
{
    public static void Run(int projPtr)
    {
        var n = SpriteNodes.At(projPtr);
        if (n.UpdaterPayload < 0 || n.UpdaterPayload >= ProjectileTable.Count)
        {
            n.UpdateUpp = 0;
            return;
        }
        short slot = (short)n.UpdaterPayload;
        var proj = GameData.Projectiles[slot];
        var weap = GameData.Weapons[proj.WeaponType];

        if (GameData.Ships[0].CurrentSystem == proj.SystemId && 0 < proj.LifeRemaining)
        {
            // Age: life -= 1.0/TimeScale.
            const double LifeStep = 1.0;   // 0x10081d40
            proj.LifeRemaining = (short)(int)((double)proj.LifeRemaining - LifeStep / WorldState.TimeScale);
            if (WorldState.ClearShotsFlag != 0)
            {
                proj.LifeRemaining = -32000;   // the global "clear shots" flag kills it
            }
            TickProjectileAi.Run(slot);
            proj.PosX += (float)(WorldState.TimeScale * proj.VelX);
            proj.PosY += (float)(WorldState.TimeScale * proj.VelY);

            // Sprite frame: heading/10 clamped to [0,35], or the self-cycling AnimFrame
            // counter (wraps past 36) when the weapon spins its graphic continuously.
            short frameIndex;
            if (((WeaponFlags)weap.Flags & WeaponFlags.SpinGraphicContinuously) == 0)
            {
                frameIndex = (short)(proj.Heading / 10);
                if (35 < frameIndex)
                {
                    frameIndex = 35;
                }
                if (frameIndex < 0)
                {
                    frameIndex = 0;
                }
            }
            else
            {
                frameIndex = proj.AnimFrame;
                proj.AnimFrame += 1;
                if (36 < proj.AnimFrame)
                {
                    proj.AnimFrame = 0;
                }
            }
            n.SpritePtr = WeaponDefTable.Store[weap.SpriteIndex * 36 + frameIndex];
            short halfW = (short)MacRectWidth.Run(n.SpritePtr);
            halfW = (short)((halfW >> 1) + ((halfW < 0 && (halfW & 1) != 0) ? 1 : 0));

            // Node screen coords: camera centre + (world - player) - halfW. (The
            // original uses the half-WIDTH for both axes — faithful.)
            n.PosX = (short)(int)(((float)WorldFlags.CameraCentreX
                                 + (proj.PosX - ShipTable.PosX))
                                - (float)halfW);
            n.PosY = (short)(int)(((float)WorldFlags.CameraCentreY
                                 + (proj.PosY - ShipTable.PosY))
                                - (float)halfW);

            // Streak trail (gated on the streaks-active flag): type from the smoke-trail
            // bit pair 0x200/0x400 (+2 when 0x800 is also set); weap.TrailSmokeSet is the
            // streak ROW into StreakFrames.
            if (WorldFlags.StreaksActiveFlag != 0)
            {
                short streakType = -1;
                if (((ushort)weap.Flags & 0x200) == 0)
                {
                    if (((ushort)weap.Flags & 0x400) != 0)
                    {
                        streakType = 0;
                    }
                }
                else
                {
                    streakType = 1;
                }
                if (streakType != -1)
                {
                    if (((ushort)weap.Flags & 0x800) != 0)
                    {
                        streakType += 2;
                    }
                    SpawnProjectileStreak.Run(proj.PosX, proj.PosY, streakType, weap.TrailSmokeSet);
                }
            }
            // Submunition cadence: DamageFalloffTimer counts up; past the weapon's
            // AnimationRate period it resets and bumps DamageFalloffSteps.
            if (0 < weap.AnimationRate)
            {
                proj.DamageFalloffTimer += 1;
                if (weap.AnimationRate < proj.DamageFalloffTimer)
                {
                    proj.DamageFalloffTimer = 0;
                    proj.DamageFalloffSteps += 1;
                }
            }
            if (GamePrefs.GfxDetailFlag != 0)
            {
                junkcode.FUN_10060094();
            }
        }
        else
        {
            // Death: still in the player's system and not already force-killed.
            if (GameData.Ships[0].CurrentSystem == proj.SystemId && -32000 < proj.LifeRemaining)
            {
                if (((WeaponFlags)weap.Flags & WeaponFlags.AreaBlastDetonation) == 0)
                {
                    if (0 < weap.ExplosionType)
                    {
                        SpawnExplosion.Run(proj.PosX, proj.PosY, 0, 0, 0);
                    }
                }
                else
                {
                    if (weap.ExplosionType == 0)
                    {
                        SpawnExplosion.Run(proj.PosX, proj.PosY, -1, 0, 0);
                    }
                    if (weap.ExplosionType == 1)
                    {
                        PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[1], 6,
                                 proj.PosX, proj.PosY, ShipTable.PosX, ShipTable.PosY);
                        SpawnExplosion.Run(proj.PosX, proj.PosY, -1, 1, 0);
                    }
                    if (weap.ExplosionType == 2)
                    {
                        PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[0], 6,
                                 proj.PosX, proj.PosY, ShipTable.PosX, ShipTable.PosY);
                        SpawnExplosion.Run(proj.PosX, proj.PosY, -1, 2, 0);
                        short blast = weap.Submunitions;

                        // Ring 1: type-1 sparks, count = ArmorMidThreshold * (blast / 50),
                        // jitter rand(blast/2) - blast/4.
                        int count = (int)(ShipStatConstants.ArmorMidThreshold *
                                          (float)((double)(float)blast / ShipStatConstants.DamageDivisor));
                        for (short i = 0; i < (short)count; i++)
                        {
                            int half = (int)(ShipStatConstants.DamageRandScale * blast);
                            short randX = (short)SeedEvoRng.Run((short)half);
                            int quarter = (blast >> 2) + ((blast < 0 && (blast & 3) != 0) ? 1 : 0);
                            float sparkX = (proj.PosX + (float)randX) - (float)quarter;
                            half = (int)(ShipStatConstants.DamageRandScale * blast);
                            short randY = (short)SeedEvoRng.Run((short)half);
                            quarter = (blast >> 2) + ((blast < 0 && (blast & 3) != 0) ? 1 : 0);
                            float sparkY = (proj.PosY + (float)randY) - (float)quarter;
                            int frame = (int)SeedEvoRng.Run(8);
                            SpawnExplosion.Run(sparkX, sparkY, -1, 1, (short)(frame + 4));
                        }

                        // Ring 2: type-0 sparks, count = SubmunitionCountScale * (blast / 50),
                        // jitter rand(blast/2) - blast.
                        count = (int)(ShipStatConstants.SubmunitionCountScale *
                                      (float)((double)(float)blast / ShipStatConstants.DamageDivisor));
                        for (short i = 0; i < (short)count; i++)
                        {
                            int half = (int)(ShipStatConstants.DamageRandScale * blast);
                            short randX = (short)SeedEvoRng.Run((short)half);
                            float sparkX = (proj.PosX + (float)randX) - (float)blast;
                            half = (int)(ShipStatConstants.DamageRandScale * blast);
                            short randY = (short)SeedEvoRng.Run((short)half);
                            float sparkY = (proj.PosY + (float)randY) - (float)blast;
                            int frame = (int)SeedEvoRng.Run(16);
                            SpawnExplosion.Run(sparkX, sparkY, -1, 0, (short)(frame + 8));
                        }
                    }
                    // Blast-radius ship damage: every active ship within the blast box
                    // (the owner is exempt unless flag 0x100 is clear AND it is the player).
                    if (0 < weap.Submunitions)
                    {
                        for (short i = 0; i < ShipTable.Count; i++)
                        {
                            bool inBlast = true;
                            if (i == proj.OwnerSlot &&
                               (((WeaponFlags)weap.Flags & WeaponFlags.BlastSafeForPlayer) != 0 || proj.OwnerSlot != 0))
                            {
                                inBlast = false;
                            }
                            if (GameData.Ships[i].IsActive != 0 && inBlast)
                            {
                                float dx = (float)EvMath.FloatAbs(GameData.Ships[i].PosX - proj.PosX);
                                float dy = (float)EvMath.FloatAbs(GameData.Ships[i].PosY - proj.PosY);
                                if (dx <= (float)weap.Submunitions)
                                {
                                    if (dy <= (float)weap.Submunitions)
                                    {
                                        ApplyShipDamage.Run(ShipTable.Ships[i], proj.PosX, proj.PosY,
                                                 weap.ImpactDamage, weap.MassDamage, weap.EnergyDamage,
                                                 proj.OwnerSlot, 0,
                                                 false, proj.FromGuardingEscort != 0, false);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            proj.LifeRemaining = ProjectileRecord.Killed;
            n.UpdaterPayload = -1;
            n.SpritePtr = 0;
            n.UpdateUpp = 0;
        }
    }
}
