using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10062800 (EV Override-11.c 41352-41372) — the collision handler for two
// projectile sprite nodes (InvokeNodeCollisionUpp's ProjectileDrawUpp token). When one node is
// UpdaterFlag 5 and the other -1 (a missile and its interceptor), and the two shots have
// different owners with at least one in intercept mode (Mode 2), spawn an explosion, play
// the hit sound at the shot, and kill both shots. Each node's payload is the index of its
// projectile record.
public static class HandleMissileIntercept
{
    public static void Run(SpriteNode nodeA, SpriteNode nodeB)
    {
        if ((nodeB.UpdaterFlag == 5 && nodeA.UpdaterFlag == -1) ||
            (nodeA.UpdaterFlag == 5 && nodeB.UpdaterFlag == -1))
        {
            var shotA = GameData.Projectiles[(short)nodeA.UpdaterPayload];
            var shotB = GameData.Projectiles[(short)nodeB.UpdaterPayload];
            // Different owners, and at least one of the pair is in intercept mode.
            if (shotA.OwnerSlot != shotB.OwnerSlot && (shotA.Mode == 2 || shotB.Mode == 2))
            {
                SpawnExplosion.Run(shotA.PosX, shotA.PosY, -1, 0, 0);
                PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[1], 5,
                    shotA.PosX, shotA.PosY, ShipTable.PosX, ShipTable.PosY);
                shotA.LifeRemaining = ProjectileRecord.Killed;   // kill both shots
                shotB.LifeRemaining = ProjectileRecord.Killed;
            }
        }
    }
}
