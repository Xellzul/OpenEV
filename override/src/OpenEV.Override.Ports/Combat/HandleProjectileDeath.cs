using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_1006aa7c (EV Override-11.c lines 43946-44041).
//
// Resolve the death of a projectile (node death flag +0x52 == -1): spawn the weapon's
// explosion kind (+0x12: 0/1/2), and for kind 2 shower two rings of sparks jittered
// inside the blast radius (+0x16); then mark the slot killed.
public static class HandleProjectileDeath
{
    // param_1 (the colliding node) is unused — FUN_1006aa7c reads only the projectile node.
    public static void Run(SpriteNode collidingNode, SpriteNode projNode)
    {
        if (projNode.UpdaterFlag != -1)   // proceed only once the death flag (-1) is set
            return;

        short shotIndex = (short)projNode.UpdaterPayload;
        var shot = GameData.Projectiles[shotIndex];
        var weap = GameData.Weapons[shot.WeaponType];
        // "Passes over asteroids" weapons skip the asteroid-collision death entirely.
        var seeker = (WeapSeekerFlags)(ushort)weap.SeekerFlags;
        if ((seeker & WeapSeekerFlags.PassesOverAsteroids) == 0)
        {
            if (weap.ExplosionType == 0)
            {
                SpawnExplosion.Run(shot.PosX, shot.PosY, -1, 0, 0);
            }
            if (weap.ExplosionType == 1)
            {
                PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[1], 6,
                    shot.PosX, shot.PosY, GameData.Ships[0].PosX, GameData.Ships[0].PosY);
                SpawnExplosion.Run(shot.PosX, shot.PosY, -1, 1, 0);
            }
            if (weap.ExplosionType == 2)
            {
                PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[0], 6,
                    shot.PosX, shot.PosY, GameData.Ships[0].PosX, GameData.Ships[0].PosY);
                SpawnExplosion.Run(shot.PosX, shot.PosY, -1, 2, 0);

                short blast = weap.Submunitions;   // +0x16 blast/spark radius
                // Ring 1: type-1 sparks at half-blast jitter.
                double sparkCount = ShipStatConstants.DeathParticleScale1 * blast;
                for (short i = 0; i < (short)(int)sparkCount; i++)
                {
                    short randX = (short)SeedEvoRng.Run((short)(int)(ShipStatConstants.Half * blast));
                    double halfBlastX = ShipStatConstants.DamageScaleX * blast;
                    short randY = (short)SeedEvoRng.Run((short)(int)(ShipStatConstants.Half * blast));
                    double halfBlastY = ShipStatConstants.DamageScaleX * blast;
                    int frame = (int)SeedEvoRng.Run(8);
                    SpawnExplosion.Run(
                        (float)-(halfBlastX - (shot.PosX + (float)randX)),
                        (float)-(halfBlastY - (shot.PosY + (float)randY)),
                        -1, 1, (short)(frame + 4));
                }
                // Ring 2: type-0 sparks at full-blast jitter.
                sparkCount = ShipStatConstants.DeathParticleScale0 * blast;
                for (short i = 0; i < (short)(int)sparkCount; i++)
                {
                    short randX = (short)SeedEvoRng.Run(blast);
                    double fullBlastX = ShipStatConstants.Half * blast;
                    short randY = (short)SeedEvoRng.Run(blast);
                    double fullBlastY = ShipStatConstants.Half * blast;
                    int frame = (int)SeedEvoRng.Run(16);
                    SpawnExplosion.Run(
                        (float)-(fullBlastX - (shot.PosX + (float)randX)),
                        (float)-(fullBlastY - (shot.PosY + (float)randY)),
                        -1, 0, (short)(frame + 8));
                }
            }
            shot.LifeRemaining = ProjectileRecord.Killed;
            projNode.UpdateUpp = 0;
            projNode.UpdaterPayload = -1;
        }
    }
}
