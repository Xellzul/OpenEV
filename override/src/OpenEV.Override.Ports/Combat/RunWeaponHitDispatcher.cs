using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10062960 (EV Override-11.c 41381-41688): the weapon-hit dispatcher.
// A projectile sprite node (projNode) has struck a ship sprite node (hitNode):
// projNode+0x58 holds the projectile slot, hitNode+0x54 points at the struck ship's
// record. Runs the friendly-fire / escort / govt suppression gates, spawns the hit
// explosion + blast-particle shower per the weapon's explosion kind, applies the
// damage (plus blast-radius area damage), triggers kill chatter and the player's
// govt-defender call, then retires the projectile.
public static class RunWeaponHitDispatcher
{
    public static void Run(SpriteNode hitNode, SpriteNode projNode)
    {
        // The projectile must be flagged dead (+0x52 == -1) with a valid hit slot (0..127).
        if (projNode.UpdaterFlag != -1 || projNode.UpdaterPayload < 0 ||
            projNode.UpdaterPayload >= ProjectileTable.Count)
            return;

        var ship = ShipTable.FromPtr(hitNode.ObjectPtr);
        short hitIndex = (short)projNode.UpdaterPayload;
        var hitShot = GameData.Projectiles[hitIndex];
        var weap = GameData.Weapons[hitShot.WeaponType];
        short ownerSlot = hitShot.OwnerSlot;

        // The hit must be live, against a real ship that isn't the firer or a unique pers,
        // and (for homing shots) only against the shot's locked target.
        if (hitShot.LifeRemaining <= 0 || ship.ShipClass == ShipRecord.EmptyShipClass ||
            ship.SlotIndex == ownerSlot || ship.PersIndex == ShipRecord.KamikazePersIndex ||
            ((WeaponGuidanceType)weap.GuidanceType == WeaponGuidanceType.HomingWeapon &&
             ship.SlotIndex != hitShot.TargetSlot))
            return;

        var owner = GameData.Ships[ownerSlot];
        if (IsFriendlyFireSuppressed(ship, owner, ownerSlot))
            return;

        // The player took a hit while cloaked: drop the cloak.
        if (ship.SlotIndex == 0 && WorldState.IsCloaked)
            DisengageCloaking.Run();

        SpawnHitEffects(weap, hitShot, ship.SlotIndex);

        // Direct damage to the struck ship, after weapon-falloff erosion (+0x20).
        int massDamage = weap.MassDamage;
        int energyDamage = weap.EnergyDamage;
        bool wasDyingOrDestroyed = ShipDerivedStats.IsDyingOrDestroyed(ship);
        if (hitShot.DamageFalloffSteps > 0)
        {
            massDamage -= hitShot.DamageFalloffSteps;
            energyDamage -= hitShot.DamageFalloffSteps;
            if ((short)massDamage < 0) massDamage = 0;
            if ((short)energyDamage < 0) energyDamage = 0;
        }
        // A direct hit on the shot's locked target (ship.SlotIndex == hitShot.TargetSlot) skips the
        // collateral retaliation refinement; a guarding-escort shot (FromGuardingEscort) re-scales overkill.
        ApplyShipDamage.Run(ship, hitShot.PosX, hitShot.PosY, weap.ImpactDamage,
                            (short)massDamage, (short)energyDamage, ownerSlot, 1,
                            ship.SlotIndex == hitShot.TargetSlot, hitShot.FromGuardingEscort != 0, false);
        bool nowDyingOrDestroyed = ShipDerivedStats.IsDyingOrDestroyed(ship);

        // A kill draws "under attack" chatter from nearby player-owned combat NPCs.
        if (nowDyingOrDestroyed && !wasDyingOrDestroyed)
            AlertNearbyCombatNpcs(ship.SlotIndex);

        if (ownerSlot == 0)
            CallForGovtDefenders.Run(ship.Ptr, ShipTable.Base);

        ApplyBlastRadiusDamage(weap, hitShot, ship.SlotIndex, ownerSlot);

        hitShot.LifeRemaining = ProjectileRecord.Killed;
        projNode.UpdateUpp = 0;
        projNode.UpdaterPayload = -1;
    }

    // FUN_10062960 41422-41492 — friendly-fire / escort / govt suppression. Returns true to
    // drop the hit (no damage applied, the projectile stays live). All reads are side-effect-free.
    private static bool IsFriendlyFireSuppressed(ShipRec ship, ShipRecord owner, short ownerSlot)
    {
        // Both ships are escorts of the same carrier.
        if (owner.OwnerSlot != -1 && ship.OwnerSlot != -1 && ship.OwnerSlot == owner.OwnerSlot)
            return true;
        // Same government never friendly-fires.
        if (owner.Govt != -1 && ship.Govt == owner.Govt)
            return true;
        // Both defending the same stellar.
        if (owner.DefendedSpobIndex != -1 && ship.DefendedSpobIndex == owner.DefendedSpobIndex)
            return true;
        // A player shot striking one of the player's own escorts.
        if (ownerSlot == 0 && ship.OwnerSlot != -1 && GameData.Ships[ship.OwnerSlot].OwnerSlot == 0)
            return true;
        // The firer's govt "never attacks player" shields the player from its fire.
        if (ship.SlotIndex == 0 && owner.Govt != -1 && GovtNeverAttacksPlayer(owner.Govt))
            return true;
        // The struck non-player ship's govt "never attacks player": a player-side shot is dropped.
        if (ship.Govt != -1 && ship.SlotIndex != 0 && GovtNeverAttacksPlayer(ship.Govt))
        {
            if (ownerSlot == 0)
                return true;
            if (ownerSlot != -1 && owner.OwnerSlot == 0)
                return true;
        }
        // Don't let the firer hit its own carrier or a sibling escort.
        if (owner.OwnerSlot != -1)
        {
            if (ship.SlotIndex == owner.OwnerSlot)
                return true;
            if (ship.OwnerSlot == owner.OwnerSlot && ship.OwnerSlot != -1)
                return true;
        }
        // Non-defender repeat of the carrier / escort checks.
        if (ship.DefendedSpobIndex == -1)
        {
            if (owner.OwnerSlot != -1 && ship.SlotIndex == owner.OwnerSlot)
                return true;
            if (ship.OwnerSlot != -1 && ship.OwnerSlot == ownerSlot)
                return true;
        }
        return false;
    }

    private static bool GovtNeverAttacksPlayer(short govt) =>
        (GameData.Governments[govt].Flags & GovtFlags.NeverAttacksPlayer) != 0;

    // FUN_10062960 41497-41585 — hit sound + explosion, keyed by the weapon's explosion kind (+0x12).
    private static void SpawnHitEffects(WeaponRecord weap, ProjectileRecord hitShot, short victimSlot)
    {
        if (weap.ExplosionType == 0)
            SpawnExplosion.Run(hitShot.PosX, hitShot.PosY, victimSlot, 0, 0);
        if (weap.ExplosionType == 1)
        {
            PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[1], 6,
                hitShot.PosX, hitShot.PosY, GameData.Player.PosX, GameData.Player.PosY);
            SpawnExplosion.Run(hitShot.PosX, hitShot.PosY, victimSlot, 1, 0);
        }
        if (weap.ExplosionType == 2)
        {
            PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[0], 6,
                hitShot.PosX, hitShot.PosY, GameData.Player.PosX, GameData.Player.PosY);
            SpawnExplosion.Run(hitShot.PosX, hitShot.PosY, victimSlot, 2, 0);

            // Particle shower scaled by the blast radius (+0x16): a dense type-1 ring jittered
            // within ±radius/4, then a sparser type-0 ring jittered within ±radius.
            short blast = weap.Submunitions;
            short ring1 = (short)(int)(ShipStatConstants.BlastParticleScale1 *
                (float)((double)(float)blast / ShipStatConstants.CoordNormalizeDivisor));
            for (short i = 0; i < ring1; i++)
                SpawnBlastParticle(hitShot.PosX, hitShot.PosY, victimSlot, blast, blast / 4, 1, 8, 4);
            short ring0 = (short)(int)(ShipStatConstants.BlastParticleScale0 *
                (float)((double)(float)blast / ShipStatConstants.CoordNormalizeDivisor));
            for (short i = 0; i < ring0; i++)
                SpawnBlastParticle(hitShot.PosX, hitShot.PosY, victimSlot, blast, blast, 0, 16, 8);
        }
    }

    // One jittered particle explosion of `kind`, offset from (cx,cy) by ±rand(blast/2) per axis
    // (re-centred by `recenter`) with a random start frame. RNG order — X jitter, Y jitter, frame
    // — is preserved so the sequence advances exactly as the original.
    private static void SpawnBlastParticle(float cx, float cy, short victimSlot, short blast,
                                           int recenter, short kind, short frameRange, short frameBase)
    {
        short jitterX = (short)SeedEvoRng.Run((short)(int)(ShipStatConstants.Half * blast));
        float x = cx + jitterX - recenter;
        short jitterY = (short)SeedEvoRng.Run((short)(int)(ShipStatConstants.Half * blast));
        float y = cy + jitterY - recenter;
        short frame = (short)((int)SeedEvoRng.Run(frameRange) + frameBase);
        SpawnExplosion.Run(x, y, victimSlot, kind, frame);
    }

    // FUN_10062960 41618-41628 — a fresh kill alerts nearby active player-owned combat NPCs that
    // were targeting the victim, making one of them speak.
    private static void AlertNearbyCombatNpcs(short victimSlot)
    {
        for (short i = 1; i < ShipTable.Count; i++)
        {
            var npc = ShipTable.Ships[i];
            if (victimSlot != i && npc.IsActive != 0 && npc.OwnerSlot == 0 &&
                victimSlot == npc.TargetSlot && npc.AiBehaviorType > ShipAiType.BraveTrader && ShipAi.IsStateCombat(npc))
                SetActiveChatterSpeaker.Run(2);
        }
    }

    // FUN_10062960 41633-41679 — blast-radius area damage: every active ship within the weapon's
    // splash box (+0x16) takes the hit. The firer is excluded from its own blast when the weapon's
    // "blast safe for the player" flag is set or an NPC fired it; only a player-fired shot without
    // that flag hits the firer.
    private static void ApplyBlastRadiusDamage(WeaponRecord weap, ProjectileRecord hitShot,
                                               short victimSlot, short ownerSlot)
    {
        if (weap.Submunitions <= 0)
            return;
        for (short i = 0; i < ShipTable.Count; i++)
        {
            bool inBlast = true;
            if (i == ownerSlot &&
                (((WeaponFlags)weap.Flags & WeaponFlags.BlastSafeForPlayer) != 0 || ownerSlot != 0))
                inBlast = false;
            var other = ShipTable.Ships[i];
            if (i == victimSlot || other.IsActive == 0 || !inBlast)
                continue;
            float dx = (float)EvMath.FloatAbs((double)(other.PosX - hitShot.PosX));
            float dy = (float)EvMath.FloatAbs((double)(other.PosY - hitShot.PosY));
            if (dx <= weap.Submunitions && dy <= weap.Submunitions)
                ApplyShipDamage.Run(other, hitShot.PosX, hitShot.PosY, weap.ImpactDamage,
                                    weap.MassDamage, weap.EnergyDamage, ownerSlot, 0,
                                    false, hitShot.FromGuardingEscort != 0, false);
        }
    }
}
