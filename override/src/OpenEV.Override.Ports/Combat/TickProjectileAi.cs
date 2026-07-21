using System;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_100233bc from EV Override-11.c lines 15435-15869. Per-frame guidance
// tick for ONE projectile slot.
//
// Modes (proj.Mode):
//   0   — normal seeker: steer toward the target ship (jamming can flip the turn
//         direction or, with seeker flag MayAttackParentIfJammed, retarget the shot
//         at its OWNER); may acquire an asteroid lock (DecoyedByAsteroids -> Mode 1)
//         or another missile (DecoyedByFlares -> Mode 2).
//   999 — jammed spin: the heading walks ± the turn rate on a 200-frame cadence;
//         MayAttackParentIfJammed + rng(1000) recovers to Mode 0 targeting the owner.
//   1/2 — asteroid / missile-intercept lock: steer toward the locked object.
// Then per weapon guidance kind: FreeflightRocket wobbles (95/5 blend of the current
// velocity with a fresh full-speed vector along the heading); FreefallBomb slowly
// aligns (±1°/tick) to its actual velocity vector.
public static class TickProjectileAi
{
    public static void Run(short slot)
    {
        var proj = GameData.Projectiles[slot];
        var weap = GameData.Weapons[proj.WeaponType];
        var guidance = (WeaponGuidanceType)weap.GuidanceType;
        var seeker = (WeapSeekerFlags)(ushort)weap.SeekerFlags;

        if (proj.Mode == 0)
        {
            bool targetValid = proj.TargetSlot == -1 ||
                (GameData.Ships[proj.TargetSlot].IsActive != 0 &&
                 proj.SystemId == GameData.Ships[proj.TargetSlot].CurrentSystem);
            if (targetValid && guidance == WeaponGuidanceType.HomingWeapon)
            {
                short desired;
                if (proj.TargetSlot == -1)
                {
                    desired = proj.Heading;
                }
                else
                {
                    var target = GameData.Ships[proj.TargetSlot];
                    desired = (short)EvMath.HeadingBetween(proj.PosX, proj.PosY, target.PosX, target.PosY);
                }

                if (InGuidanceWindow(weap, proj, 15.0 * WorldState.TimeScale))
                {
                    double turnRate = (short)WeaponTurnRate.Run(proj.WeaponType);

                    // Jamming sweep over the 4 jam types (the JammingTypeNHalf/Full seeker
                    // flags, N=1..4 — not a *Table walk, so no Count const applies): Half
                    // (with a coin flip) and Full (always) flip the turn direction;
                    // MayAttackParentIfJammed can also retarget at the owner.
                    WeapSeekerFlags jamHalf = WeapSeekerFlags.JammingType1Half;
                    WeapSeekerFlags jamFull = WeapSeekerFlags.JammingType1Full;
                    if (proj.TargetSlot != -1)
                    {
                        for (short jamType = 0; jamType < 4; jamType++)
                        {
                            if ((seeker & jamHalf) != 0 &&
                                JammingLevel.Run(GameData.Ships[proj.TargetSlot], jamType) != 0 &&
                                (short)SeedEvoRng.Run(2) == 0)
                            {
                                if (0.0 < turnRate)
                                    turnRate *= -1.0;
                                RetargetOwnerIfJammed(proj, seeker, 1000);
                            }
                            if ((seeker & jamFull) != 0 &&
                                JammingLevel.Run(GameData.Ships[proj.TargetSlot], jamType) != 0)
                            {
                                if (0.0 < turnRate)
                                    turnRate *= -1.0;
                                RetargetOwnerIfJammed(proj, seeker, 500);
                            }
                            jamHalf = (WeapSeekerFlags)((int)jamHalf << 1);
                            jamFull = (WeapSeekerFlags)((int)jamFull << 1);
                        }
                    }

                    if (proj.Mode == 0)   // always true here (Mode hasn't changed) — kept for fidelity
                    {
                        // Shots locked on the PLAYER while the player is cloaked: zero the turn
                        // rate; MayAttackParentIfJammed + rng(1000) retargets the owner.
                        if (proj.TargetSlot == 0 && WorldState.IsCloaked)
                        {
                            turnRate = 0.0;
                            RetargetOwnerIfJammed(proj, seeker, 1000);
                        }
                        // Proximity overshoot drop: target within 250 on both axes but more
                        // than 45° off the nose -> lose lock.
                        if ((seeker & WeapSeekerFlags.LosesLockIfNotAhead) != 0 && proj.TargetSlot != -1)
                        {
                            var target = GameData.Ships[proj.TargetSlot];
                            if (WithinRange(proj, target.PosX, target.PosY, 250) &&
                                45 < AngleErrorDegrees(proj, target.PosX, target.PosY))
                                proj.TargetSlot = -1;
                        }
                    }

                    // Steer toward the desired heading by turnRate * timeScale, through the
                    // short side of the circle.
                    if (Math.Abs((int)turnRate) < Math.Abs((short)(desired - proj.Heading)))
                    {
                        short delta = (short)(desired - proj.Heading);
                        if (359 < delta)
                            delta -= 360;
                        if (delta < 0)
                            delta += 360;
                        if (delta < 181)
                            proj.Heading = (short)(int)(turnRate * WorldState.TimeScale + proj.Heading);
                        else
                            proj.Heading = (short)(int)-(turnRate * WorldState.TimeScale - proj.Heading);
                    }
                }
                ResolveVelocityFromHeading(proj, weap);
            }

            // Asteroid lock acquisition (guided weapon, DecoyedByAsteroids, 1-in-10): an
            // active asteroid within 200 on both axes and under 16° off the nose.
            if (guidance == WeaponGuidanceType.HomingWeapon &&
                (seeker & WeapSeekerFlags.DecoyedByAsteroids) != 0 &&
                (short)SeedEvoRng.Run(10) == 0)
            {
                for (short i = 0; i < AsteroidTable.Count; i++)
                {
                    var a = GameData.Asteroids[i];
                    if (a.Active != 0 && WithinRange(proj, a.PosX, a.PosY, 200) &&
                        AngleErrorDegrees(proj, a.PosX, a.PosY) < 16)
                    {
                        proj.Mode = 1;
                        proj.TargetSlot = i;
                        break;
                    }
                }
            }

            // Missile-intercept lock acquisition (DecoyedByFlares, 1-in-15): another live
            // shot from a DIFFERENT owner whose weapon ActsAsMissileDecoy, within 200 on
            // both axes and under 11° off the nose.
            if (proj.Mode == 0 && (seeker & WeapSeekerFlags.DecoyedByFlares) != 0 &&
                guidance == WeaponGuidanceType.HomingWeapon && (short)SeedEvoRng.Run(15) == 0)
            {
                for (short i = 0; i < ProjectileTable.Count; i++)
                {
                    var other = GameData.Projectiles[i];
                    if (0 < other.LifeRemaining && other.OwnerSlot != proj.OwnerSlot &&
                        ((WeaponFlags)GameData.Weapons[other.WeaponType].Flags & WeaponFlags.ActsAsMissileDecoy) != 0 &&
                        WithinRange(proj, other.PosX, other.PosY, 200) &&
                        AngleErrorDegrees(proj, other.PosX, other.PosY) < 11)
                    {
                        proj.Mode = 2;
                        proj.TargetSlot = i;
                        break;
                    }
                }
            }
        }
        else if (proj.Mode == 999)
        {
            // Jammed spin: inside the guidance window (no time-scale factor here), the
            // heading walks ± the turn rate on a 200-frame cadence.
            if (InGuidanceWindow(weap, proj, 15.0))
            {
                short turn = (short)WeaponTurnRate.Run(proj.WeaponType);
                if (WorldState.GameFrameTickCounter % 200 < 100)
                    proj.Heading -= turn;
                else
                    proj.Heading += turn;
            }
            ResolveVelocityFromHeading(proj, weap);

            // Recover from the jam: MayAttackParentIfJammed + rng(1000) -> Mode 0 targeting
            // the owner.
            if (RetargetOwnerIfJammed(proj, seeker, 1000))
                proj.Mode = 0;
        }
        else
        {
            // Modes 1 (asteroid) / 2 (missile intercept): steer toward the locked object.
            // NOTE the decompile's unaff_r20 — when the lock is dropped below, `desired` keeps
            // this untracked-register default (0), faithful to the original's garbage-
            // register read.
            short desired = default;
            if (proj.Mode == 1)
            {
                if (proj.TargetSlot == -1)
                {
                    desired = proj.Heading;
                }
                else if (GameData.Asteroids[proj.TargetSlot].Active == 0)
                {
                    proj.TargetSlot = -1;
                }
                else
                {
                    var lockedAsteroid = GameData.Asteroids[proj.TargetSlot];
                    desired = (short)EvMath.HeadingBetween(proj.PosX, proj.PosY, lockedAsteroid.PosX, lockedAsteroid.PosY);
                }
            }
            if (proj.Mode == 2)
            {
                if (proj.TargetSlot == -1)
                {
                    desired = proj.Heading;
                }
                // ORIGINAL BUG kept (bounds-guarded): the decompile checks ship-table
                // activity at TargetSlot (decompile 15752), but Mode 2 targets are
                // PROJECTILE slots (0..127) — for target >= 36 the original read past the
                // 36-ship heap into whatever followed. The managed Store can't read out of
                // bounds, so out-of-range slots just skip the drop-lock branch here (the
                // common heap-garbage-nonzero outcome on real hardware).
                else if (proj.TargetSlot < ShipTable.Count && GameData.Ships[proj.TargetSlot].IsActive == 0)
                {
                    proj.TargetSlot = -1;
                }
                else
                {
                    var lockedProjectile = GameData.Projectiles[proj.TargetSlot];
                    desired = (short)EvMath.HeadingBetween(proj.PosX, proj.PosY, lockedProjectile.PosX, lockedProjectile.PosY);
                }
            }

            // Guidance window, then steer by ± the turn rate (not time-scaled here).
            if (InGuidanceWindow(weap, proj, 15.0))
            {
                double turnRate = (short)WeaponTurnRate.Run(proj.WeaponType);
                if (turnRate < Math.Abs((short)(desired - proj.Heading)))
                {
                    short delta = (short)(desired - proj.Heading);
                    if (359 < delta)
                        delta -= 360;
                    if (delta < 0)
                        delta += 360;
                    if (delta < 181)
                        proj.Heading = (short)(int)(proj.Heading + turnRate);
                    else
                        proj.Heading = (short)(int)(proj.Heading - turnRate);
                }
            }
            ResolveVelocityFromHeading(proj, weap);
        }

        // Guidance kind FreeflightRocket — wobble: blend 95% of the current velocity
        // with 5% of a fresh full-speed vector along the heading, scaled by 0.01.
        if (guidance == WeaponGuidanceType.FreeflightRocket)
        {
            float oldVx = proj.VelX;
            float oldVy = proj.VelY;
            float newVx = 0f;
            float newVy = 0f;
            EvMath.OffsetByHeading(weap.ProjectileSpeed, proj.Heading, ref newVx, ref newVy);
            proj.VelX = (float)(0.01 * (95f * oldVx + 5f * newVx));
            proj.VelY = (float)(0.01 * (95f * oldVy + 5f * newVy));
        }

        // Guidance kind FreefallBomb — slow alignment: walk the heading ±1°/tick toward
        // the atan2 of the (×1000) velocity vector.
        if (guidance == WeaponGuidanceType.FreefallBomb)
        {
            float vx = (float)(1000.0 * proj.VelX);
            float vy = (float)(1000.0 * proj.VelY);
            short desired = (short)EvMath.HeadingBetween(0f, 0f, vx, vy);
            if (0 < Math.Abs((short)(desired - proj.Heading)))
            {
                short delta = (short)(desired - proj.Heading);
                if (359 < delta)
                    delta -= 360;
                if (delta < 0)
                    delta += 360;
                if (delta < 181)
                    proj.Heading += 1;
                else
                    proj.Heading -= 1;
            }
        }
    }

    // True once (1.5 * weap.Lifetime - proj.LifeRemaining) clears `threshold` (a flat
    // 15, or — Mode 0 only — 15 * timeScale). LifeRemaining only counts down, so this
    // is false for a short window right after launch and stays true for the rest of
    // the flight; only a very short-fused weapon (Lifetime well under the threshold)
    // can spend most or all of its life with guidance suppressed. Used at decompile
    // 15478-15486 (Mode 0, with the timeScale factor), 15684-15691 (Mode 999),
    // 15762-15769 (Mode 1/2).
    private static bool InGuidanceWindow(WeaponRecord weap, ProjectileRecord proj, double threshold)
        => threshold < 1.5 * weap.Lifetime - proj.LifeRemaining;

    // Seeker flag MayAttackParentIfJammed: an N-in-rngRange roll retargets a jammed
    // shot at its OWNER, then clears OwnerSlot so it can't refire. Used at the
    // bitLo/bitHi jam-sweep (decompile 15504-15512 / 15523-15531), the cloaked-player
    // check (15538-15548), and the Mode 999 recovery (15721-15729, which also clears
    // Mode back to 0 on success).
    private static bool RetargetOwnerIfJammed(ProjectileRecord proj, WeapSeekerFlags seeker, short rngRange)
    {
        if ((seeker & WeapSeekerFlags.MayAttackParentIfJammed) != 0 &&
            (short)SeedEvoRng.Run(rngRange) == 0 && proj.OwnerSlot != -1)
        {
            proj.TargetSlot = proj.OwnerSlot;
            proj.OwnerSlot = -1;
            return true;
        }
        return false;
    }

    // |targetX - proj.PosX| and |targetY - proj.PosY| (each truncated to a 16-bit
    // magnitude, matching the decompile's float-diff -> int -> short -> abs chain),
    // both under halfExtent. Used at decompile 15549-15568 (proximity-drop, 250),
    // 15627-15647 (asteroid-lock, 200), 15649-15679 (missile-intercept, 200).
    private static bool WithinRange(ProjectileRecord proj, float targetX, float targetY, int halfExtent)
        => Math.Abs((short)(int)(targetX - proj.PosX)) < halfExtent &&
           Math.Abs((short)(int)(targetY - proj.PosY)) < halfExtent;

    // Heading FROM proj TO (targetX, targetY) vs proj.Heading, as |delta| mod 360 —
    // the decompile never flips this to the short way round (unlike the steering code
    // in Run), so e.g. a 359° raw delta stays 359, not 1. Same three call sites as
    // WithinRange.
    private static short AngleErrorDegrees(ProjectileRecord proj, float targetX, float targetY)
    {
        short towards = (short)EvMath.HeadingBetween(proj.PosX, proj.PosY, targetX, targetY);
        return (short)(Math.Abs(towards - proj.Heading) % 360);
    }

    // Wrap Heading into [0,359], then rebuild VelX/VelY from scratch along it at the
    // weapon's projectile speed (clamped to that speed). Used at decompile 15603-15620
    // (Mode 0), 15703-15720 (Mode 999), 15803-15820 (Mode 1/2).
    private static void ResolveVelocityFromHeading(ProjectileRecord proj, WeaponRecord weap)
    {
        if (359 < proj.Heading)
            proj.Heading -= 360;
        if (proj.Heading < 0)
            proj.Heading += 360;

        proj.VelY = 0f;
        proj.VelX = 0f;
        EvMath.OffsetByHeading(weap.ProjectileSpeed, proj.Heading, ref proj.VelX, ref proj.VelY);
        EvMath.ClampVector(weap.ProjectileSpeed, ref proj.VelX, ref proj.VelY);
    }
}
