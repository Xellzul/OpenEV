using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// FUN_10020ffc (EV Override-11.c lines 14662-15080) — per-frame physics tick for the
// 8 active beam/projectile slots: age each slot, aim guided shots at their homing
// target (or the firing ship's heading), fly the shot out to its screen position,
// scan for the nearest valid ship within hit range and firing arc, and on impact
// apply damage — plus any submunition burst — and spawn the explosion.
public static class UpdateProjectilePositions
{
    public static void Run()
    {
        SetGamePortAndDevice.Run();
        foreach (var beam in GameData.Beams)
        {
            // Expired / owner-less slot: age it out and skip.
            if (beam.Life < 0 || beam.OwnerSlot == -1)
            {
                if (beam.Life == -1)
                    beam.Life = (short)(beam.Life - 1);
                else
                    beam.OwnerSlot = -1;
                continue;
            }

            beam.Life = (short)(beam.Life - 1);
            if (WorldState.ClearShotsFlag != 0)
                beam.Life = -1;
            beam.PrevStartX = beam.StartX;
            beam.PrevStartY = beam.StartY;
            beam.PrevEndX = beam.EndX;
            beam.PrevEndY = beam.EndY;

            short owner = beam.OwnerSlot;
            short target = beam.TargetSlot;
            var weapon = GameData.Weapons[beam.WeaponType];
            var ownerShip = ShipTable.Ships[owner];

            // Aim: the owner's facing, or the bearing to a live homing target.
            short heading = ownerShip.Heading;
            if (target != -1 && GameData.Ships[target].IsActive != 0)
            {
                var homingTarget = ShipTable.Ships[target];
                heading = (short)EvMath.HeadingBetween(ownerShip.PosX, ownerShip.PosY, homingTarget.PosX, homingTarget.PosY);
            }
            short inaccuracy = weapon.Inaccuracy;
            if (inaccuracy > 0)
                heading = (short)(heading + ((short)SeedEvoRng.Run((short)(inaccuracy << 1)) - inaccuracy));

            // Working position = the owner's position pushed out along `heading` by the
            // weapon's reach: a fixed range, else half the firing ship's sprite height.
            float projX = ownerShip.PosX;
            float projY = ownerShip.PosY;
            short fixedRange = beam.FixedRange;
            if (fixedRange == 0)
            {
                if (beam.SourceShip != 0)
                {
                    int spriteH = MacRectHeight.Run(WeaponGraphicsTable.Store[ownerShip.ShipClass * WeaponGraphicsTable.FrameCount + ownerShip.Heading / 10]);
                    int reach = (int)(ShipStatConstants.DamageRandScale * (double)(short)spriteH);   // half the sprite height
                    EvMath.OffsetByHeading((double)(float)(short)reach, heading, ref projX, ref projY);
                }
            }
            else
            {
                EvMath.OffsetByHeading((double)(float)fixedRange, (heading + 90) % 360, ref projX, ref projY);
            }

            // Screen position = camera centre + (world position - player position).
            float newX = (float)WorldFlags.CameraCentreX + (projX - ShipTable.PosX);
            float newY = (float)WorldFlags.CameraCentreY + (projY - ShipTable.PosY);
            beam.StartX = (short)(int)newX;
            beam.StartY = (short)(int)newY;
            if (beam.Life < 0)
                continue;

            // Clamp X to the play-field right edge.
            if (GlobalState.PortRight - 152 < beam.StartX)
                beam.StartX = (short)(GlobalState.PortRight - 152);

            // Find the nearest in-system ship within hit range (skipping the firer's own
            // allies/escorts and the firer's group).
            short found = -1;
            int bestDist = 0;
            for (short otherShip = 0; otherShip < ShipTable.Count; otherShip++)
            {
                var os = ShipTable.Ships[otherShip];
                bool isCandidate = true;
                if (os.OwnerSlot != -1)
                {
                    isCandidate = os.OwnerSlot != owner;
                    if (ShipDerivedStats.IsPlayerOrEscort(os) && ShipDerivedStats.IsPlayerOrEscort(ownerShip))
                        isCandidate = false;
                }
                if (otherShip == ownerShip.OwnerSlot)
                    isCandidate = false;
                if (otherShip == owner || !isCandidate || os.IsActive == 0 ||
                    os.CurrentSystem != ownerShip.CurrentSystem || os.ShipClass == ShipRecord.EmptyShipClass)
                    continue;

                short frames = (short)MacRectHeight.Run(WeaponGraphicsTable.Store[os.ShipClass * WeaponGraphicsTable.FrameCount]);
                short halfDim = (short)(int)(ShipStatConstants.AiFrameScale0p66 * frames);
                int halfRounded = (halfDim >> 1) + ((halfDim < 0 && (halfDim & 1) != 0) ? 1 : 0);
                int hitRadius = (int)(ShipStatConstants.ArmorMidThreshold * weapon.ProjectileSpeed + (float)halfRounded);
                double distSq = EvMath.DistanceSquared(os.PosX, os.PosY, projX, projY);
                if (distSq > (double)(float)(hitRadius * hitRadius))
                    continue;

                // Within range - also require the ship to be inside the firing arc.
                short bearing = (short)EvMath.HeadingBetween(ownerShip.PosX, ownerShip.PosY, os.PosX, os.PosY);
                int absAngle = Abs(bearing - heading);
                uint rangeBits = (uint)halfDim * 10;
                int arc = (int)(short)((short)((int)rangeBits >> 5) +
                                       (ushort)(((int)rangeBits < 0 && (rangeBits & 0x1f) != 0) ? 1 : 0));
                if (absAngle > arc)
                    continue;

                int d = (int)EvMath.DistanceSquared(os, ShipTable.Ships[owner]);
                if (d < bestDist || found == -1)
                {
                    bestDist = d;
                    found = otherShip;
                }
            }

            // speedBits is a signed value in a uint variable (matching the ASM's truncate-to-int,
            // then store/reload-as-unsigned) so both assignments truncate via (int) before the
            // (uint) reinterpret -- a bare (uint) of a negative double clamps to 0 in C#, it does
            // NOT reinterpret the signed bit pattern like (uint)(int) does.
            uint speedBits;
            float tgtX;
            float tgtY;
            if (found == -1)
            {
                // No hit: keep flying at twice the weapon's base speed.
                speedBits = (uint)(int)(ShipStatConstants.AiFrameScale2p0 * weapon.ProjectileSpeed);
            }
            else
            {
                // Hit: detonate at the impact point (jittered), then apply damage.
                tgtX = ShipStatConstants.BlastJitterBase + GameData.Ships[found].PosX + (float)(short)SeedEvoRng.Run(50);
                tgtY = ShipStatConstants.BlastJitterBase + GameData.Ships[found].PosY + (float)(short)SeedEvoRng.Run(50);

                short explosionKind = weapon.ExplosionType;
                if (explosionKind == 0) SpawnExplosion.Run(tgtX, tgtY, owner, 0, 0);
                if (explosionKind == 1) SpawnExplosion.Run(tgtX, tgtY, owner, 1, 0);
                if (explosionKind == 2)
                {
                    SpawnExplosion.Run(tgtX, tgtY, owner, 2, 0);
                    short subSeed = weapon.Submunitions;

                    // First submunition burst: rng spread scales with the count, radius with the seed.
                    int n1 = (int)(ShipStatConstants.ArmorMidThreshold *
                                   (float)((double)(float)subSeed / ShipStatConstants.DamageDivisor));
                    double rad1 = ShipStatConstants.SpriteBoundsScale * subSeed;
                    int spread1 = (int)(ShipStatConstants.DamageRandScale * subSeed);
                    for (short i = 0; i < (short)n1; i++)
                    {
                        float fx = (float)-(rad1 - (tgtX + (float)(short)SeedEvoRng.Run((short)spread1)));
                        float fy = (float)-(rad1 - (tgtY + (float)(short)SeedEvoRng.Run((short)spread1)));
                        SpawnExplosion.Run(fx, fy, owner, 1, (short)((int)SeedEvoRng.Run(8) + 4));
                    }

                    // Second burst: rng spread is the seed directly, count scales differently.
                    int n2 = (int)(ShipStatConstants.SubmunitionCountScale *
                                   (float)((double)(float)subSeed / ShipStatConstants.DamageDivisor));
                    double rad2 = ShipStatConstants.DamageRandScale * subSeed;
                    for (short i = 0; i < (short)n2; i++)
                    {
                        float fx = (float)-(rad2 - (tgtX + (float)(short)SeedEvoRng.Run(subSeed)));
                        float fy = (float)-(rad2 - (tgtY + (float)(short)SeedEvoRng.Run(subSeed)));
                        SpawnExplosion.Run(fx, fy, owner, 0, (short)((int)SeedEvoRng.Run(16) + 8));
                    }
                }

                short shieldDamage = weapon.MassDamage;
                short armorDamage = weapon.EnergyDamage;
                var hitShip = ShipTable.Ships[found];
                if (hitShip.PersIndex != ShipRecord.KamikazePersIndex)
                {
                    // The decompile branches 4 ways on whether `found` is the shot's intended target -
                    // the firer's current target for an unguided beam, else the beam's homing target.
                    // That gate is ApplyShipDamage's 9th arg: a direct hit skips the collateral
                    // retaliation refinement.
                    bool hitIntendedTarget = beam.TargetSlot == -1
                        ? found == ownerShip.TargetSlot
                        : found == beam.TargetSlot;
                    ApplyShipDamage.Run(hitShip, ownerShip.PosX, ownerShip.PosY,
                                         weapon.ImpactDamage, shieldDamage, armorDamage, owner, 1,
                                         hitIntendedTarget, false, false);

                    if (weapon.ImpactDamage < 0 && found != 0)
                        GameData.Ships[found].IsTractored = 1;
                    if (owner == 0 && (weapon.MassDamage > 0 || weapon.EnergyDamage > 0))
                        CallForGovtDefenders.Run(hitShip, ShipTable.Ships[owner]);
                    if (hitShip.AiBehaviorType > 0 &&
                        (hitShip.Govt != ownerShip.Govt || hitShip.Govt == -1) && owner != -1 &&
                        owner != hitShip.OwnerSlot && hitShip.JumpWindupTimer < 1)
                    {
                        GameData.Ships[found].TargetSlot = owner;
                        if (target == -1 || found == target)
                            GameData.Ships[found].ProvokedFlag = 1;
                    }
                }
                if (found == 0)
                    WorldState.PlayerShieldBarDirty = 1;

                // (The decompile re-aims the dead shot toward the hit ship here, but writes it into
                // the target pair that is overwritten below - a dead computation, so dropped. Only
                // `speedBits` from this distance is actually used, by the final offset below.)
                double dist = MacToolbox.sqrt(EvMath.DistanceSquared(projX, projY, GameData.Ships[found].PosX, GameData.Ships[found].PosY));
                short foundFrames = (short)MacRectHeight.Run(WeaponGraphicsTable.Store[GameData.Ships[found].ShipClass * WeaponGraphicsTable.FrameCount]);
                speedBits = (uint)(int)-(ShipStatConstants.AiFrameScale0p3 * foundFrames - dist);
            }

            // Secondary position = the new screen position pushed along `heading` by speed.
            tgtX = newX;
            tgtY = newY;
            EvMath.OffsetByHeading((double)(float)(int)speedBits, heading, ref tgtX, ref tgtY);
            beam.EndX = (short)(int)tgtX;
            beam.EndY = (short)(int)tgtY;
            if (GlobalState.PortRight - 152 <= beam.EndX)
                beam.EndX = (short)(GlobalState.PortRight - 153);
        }
    }

    // abs() via the decompile's (x ^ sign) - sign two's-complement trick.
    private static int Abs(int x)
    {
        int sign = x >> 0x1f;
        return (sign ^ x) - sign;
    }
}
