using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Ship;

// Port of FUN_1001eb2c (EV Override-11.c 13750-14063).
//
// Per-frame sprite-node updater for one ship slot: refreshes the node's sprite
// rect / bounds / screen position from the ship record, and when the ship has
// run out of armor plays the staged death sequence (tiered burn-off explosions,
// the final blast with splash damage to nearby ships, mission kill-goal
// bookkeeping, pers retirement) before freeing the slot.
//
// `node` is an int handle to a managed SpriteNode (resolved via SpriteNodes.At);
// the field offsets are documented on that accessor.
public static class UpdateShipSlotTick
{
    public static void Run(int node)
    {
        var spriteNode = SpriteNodes.At(node);
        if (spriteNode.ObjectPtr == 0)
        {
            spriteNode.UpdateUpp = 0;
            return;
        }
        int ship = spriteNode.ObjectPtr;
        var shipRec = ShipTable.FromPtr(ship);
        var player = ShipTable.Player;

        if (shipRec.IsActive == 0)
        {
            shipRec.HasWorldSpriteNode = 0;
            if (player.TargetSlot == shipRec.SlotIndex)
            {
                player.TargetSlot = -1;
                WorldState.WeaponSlotDirty = 1;
            }
            spriteNode.UpdateUpp = 0;
            return;
        }
        if ((shipRec.CurrentSystem != player.CurrentSystem) || (shipRec.IsActive == 0))
        {   // second IsActive==0 test is always false here (returned above) — faithful to the decompile
            spriteNode.SpritePtr = 0;
            spriteNode.UpdateUpp = 0;
            shipRec.IsActive = 0;
            shipRec.HasWorldSpriteNode = 0;
            shipRec.OwnerSlot = -1;
            return;
        }

        var cls = GameData.ShipClasses[shipRec.ShipClass];

        // Pick the sprite header for the ship's class + heading (36-heading frame table).
        if (cls.Cost == -1)
        {
            spriteNode.SpritePtr = 0;
        }
        else if (shipRec.SlotIndex == 0)
        {
            spriteNode.SpritePtr = WeaponGraphicsTable.Store[shipRec.ShipClass * 36 + shipRec.Heading / 10];
        }
        else
        {
            double absDx = EvMath.FloatAbs(shipRec.PosX);
            double absDy = EvMath.FloatAbs(shipRec.PosY);
            if ((ShipStatConstants.MinMoveThreshold <= (float)absDx) || (ShipStatConstants.MinMoveThreshold <= (float)absDy))
            {
                spriteNode.SpritePtr = 0;
            }
            else
            {
                spriteNode.SpritePtr = WeaponGraphicsTable.Store[shipRec.ShipClass * 36 + shipRec.Heading / 10];
            }
        }

        // Node bounding box from the sprite half-extents, then the camera-relative
        // screen position.
        {
            double boundsScale = ShipStatConstants.SpriteBoundsScale;
            int rectPtr = spriteNode.SpritePtr;
            int spW = (short)MacRectWidth.Run(rectPtr);
            int spH = (short)MacRectHeight.Run(rectPtr);
            short halfW = (short)(int)(boundsScale * spW);
            spriteNode.ExtentLeft = halfW;
            short halfH = (short)(int)(boundsScale * spH);
            spriteNode.ExtentTop = halfH;
            spriteNode.ExtentRight = (short)(halfW * 3);
            spriteNode.ExtentBottom = (short)(halfH * 3);
            int scrCX = WorldFlags.CameraCentreX;
            int scrCY = WorldFlags.CameraCentreY;
            // spW / 2 (not spW >> 1): the ASM is srawi+addze = signed truncating divide.
            spriteNode.PosX = (short)(int)((scrCX + (shipRec.PosX - player.PosX)) - (spW / 2));
            spriteNode.PosY = (short)(int)((scrCY + (shipRec.PosY - player.PosY)) - (spH / 2));
        }
        if (GamePrefs.GfxDetailFlag != 0)
        {
            junkcode.FUN_10060094();
        }

        // Death sequence — nothing further unless the ship is dying/destroyed.
        if (!ShipDerivedStats.IsDyingOrDestroyed(shipRec))
        {
            return;
        }

        // Phase 1: countdown just expired -> seed the death delay from the class.
        if (shipRec.DeathTimer <= ShipStatConstants.ZeroFloat)
        {
            shipRec.DeathTimer = (float)cls.DeathDelay;
            if (shipRec.SlotIndex != 0)
            {
                return;
            }
            // The player's death plays out slower.
            shipRec.DeathTimer = shipRec.DeathTimer * ShipStatConstants.ArmorDamageScale;
            return;
        }

        // Phase 2: mid-countdown -> tiered random burn-off explosions on the hull.
        if (ShipStatConstants.ArmorMidThreshold < shipRec.DeathTimer)
        {
            ushort tierWeight = shipRec.DeathTimer < ShipStatConstants.ArmorTier2Threshold ? (ushort)1
                : shipRec.DeathTimer < ShipStatConstants.ArmorTier4Threshold ? (ushort)2
                : shipRec.DeathTimer < ShipStatConstants.ArmorTier8Threshold ? (ushort)4
                : (ushort)8;
            if ((short)SeedEvoRng.Run((short)tierWeight) != 0)
            {
                return;
            }
            short spriteW = (short)MacRectWidth.Run(spriteNode.SpritePtr);
            int spread = (int)(ShipStatConstants.SpriteBoundsScale * spriteW);
            if ((short)spread < 1)
            {
                spread = 1;
            }
            short rollX = (short)SeedEvoRng.Run((short)(spread << 1));
            double offX = rollX;
            double halfSpreadX = (short)spread;
            short rollY = (short)SeedEvoRng.Run((short)(spread << 1));
            double offY = rollY;
            double halfSpreadY = (short)spread;
            int burnKind = (tierWeight < 3) && (59 < cls.DeathDelay) ? (int)SeedEvoRng.Run(2) : 0;
            SpawnExplosion.Run((shipRec.PosX + (float)offX) - (float)halfSpreadX,
                     (shipRec.PosY + (float)offY) - (float)halfSpreadY, shipRec.SlotIndex,
                     (short)burnKind, 0);
            if ((short)SeedEvoRng.Run(4) == 0)
            {
                PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[2], 1, shipRec.PosX, shipRec.PosY, player.PosX, player.PosY);
            }
            return;
        }

        // Phase 3: countdown finished -> the final blast.
        if (shipRec.SlotIndex == 0)
        {
            // The player died: silence the looping death-countdown, jump-windup, and UI chimes.
            foreach (int snd in new[] {
                SoundResourceCells.DeathCountdownSnd,
                SoundResourceCells.BoardingChimeSnd,
                SoundResourceCells.UiChimeSnd })
            {
                if ((short)CountMatchingSoundVoices.Run(snd) != 0)
                {
                    FlushMixQueueEntries.Run(snd);
                }
            }
        }

        // Splash parameters scale with the hull mass (light hulls: none).
        int blastRadius;
        int dmgKind;
        int dmgSpread;
        if (cls.Mass < 100)
        {
            blastRadius = 0;
            dmgKind = 1;
            dmgSpread = 0;
        }
        else
        {
            blastRadius = (int)(ShipStatConstants.DamageScaleB * cls.Mass + ShipStatConstants.DamageDivisor);
            dmgKind = 1000;
            dmgSpread = (int)(ShipStatConstants.DamageSpreadScale * cls.Mass + ShipStatConstants.DamageBaseB);
        }
        short radius = (short)blastRadius;
        if (0 < radius)
        {
            // Splash damage to every other live ship inside the blast box.
            float radiusF = radius;
            for (short i = 0; i < ShipTable.Count; i++)
            {
                var other = ShipTable.Ships[i];
                if ((i == shipRec.SlotIndex) || (other.IsActive == 0))
                {
                    continue;
                }
                double absDx = EvMath.FloatAbs(other.PosX - shipRec.PosX);
                double absDy = EvMath.FloatAbs(other.PosY - shipRec.PosY);
                if (((float)absDx <= radiusF) && ((float)absDy <= radiusF) && (shipRec.PersIndex != ShipRecord.KamikazePersIndex))
                {
                    // Ship-on-ship splash: not an intended-target hit, but the ASM sets both
                    // overflow-scaling and clamp-loss-to-shield (stack args 0x3c/0x40 = 1).
                    ApplyShipDamage.Run(other, shipRec.PosX, shipRec.PosY, (short)dmgKind,
                             (short)dmgSpread, (short)dmgSpread, shipRec.SlotIndex, 0,
                             false, true, true);
                }
            }
            // Ring of secondary explosions (count scales with the blast radius).
            float ringCount = ShipStatConstants.ArmorDamageScale * (float)(radiusF / ShipStatConstants.DamageDivisor);
            double randScale = ShipStatConstants.DamageRandScale;
            for (short i = 0; i < (short)(int)ringCount; i++)
            {
                short rollX = (short)SeedEvoRng.Run((short)blastRadius);
                double offX = rollX;
                double halfX = randScale * radius;
                short rollY = (short)SeedEvoRng.Run((short)blastRadius);
                double offY = rollY;
                double halfY = randScale * radius;
                int frame = (int)SeedEvoRng.Run(8);
                SpawnExplosion.Run((float)-(halfX - (double)(shipRec.PosX + (float)offX)),
                         (float)-(halfY - (double)(shipRec.PosY + (float)offY)),
                         shipRec.SlotIndex, 1, (short)(frame + 4));
            }
            // Inner cluster of fireballs.
            ringCount = ShipStatConstants.DamageScaleC * (float)(radiusF / ShipStatConstants.DamageDivisor);
            for (short i = 0; i < (short)(int)ringCount; i++)
            {
                short backRollX = (short)SeedEvoRng.Run((short)(int)(randScale * radius));
                double backX = backRollX;
                short rollX = (short)SeedEvoRng.Run((short)blastRadius);
                double offX = rollX;
                short backRollY = (short)SeedEvoRng.Run((short)(int)(randScale * radius));
                double backY = backRollY;
                short rollY = (short)SeedEvoRng.Run((short)blastRadius);
                double offY = rollY;
                int frame = (int)SeedEvoRng.Run(16);
                SpawnExplosion.Run((shipRec.PosX + (float)offX) - (float)backX,
                         (shipRec.PosY + (float)offY) - (float)backY, shipRec.SlotIndex,
                         0, (short)(frame + 8));
            }
        }

        // Mission kill-goal bookkeeping: destroying a ship tied to a grudge mission can
        // fail that mission, and always bumps its destroyed / spawn counters.
        if ((shipRec.GrudgeMissionIndex != -1) && (shipRec.SlotIndex != 0))
        {
            var mission = GameData.Missions[shipRec.GrudgeMissionIndex];
            var missionState = GameData.MissionStates[shipRec.GrudgeMissionIndex];
            if (KillFailsGrudgeMission(mission, missionState, shipRec))
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                // "Mission failed." = data-seg string DAT_10083386 (not invented UI).
                EnqueueChatterEvent.Run("Mission failed.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                MarkMissionFailed.Run(shipRec.GrudgeMissionIndex);
            }
            mission.DestroyedShipCount = (short)(mission.DestroyedShipCount + 1);
            if (0 < mission.SpawnCount)
            {
                mission.SpawnCount = (short)(mission.SpawnCount - 1);
            }
        }

        // A dying player escort hands its fuel/outfits back to the fleet.
        if ((shipRec.OwnerSlot == 0) && (shipRec.AiBehaviorType == ShipAiType.Escort) &&
            (shipRec.GrudgeMissionIndex == -1) && (cls.InherentAI < ShipAiType.Warship))
        {
            RedistributeCargoAmongShips.Run(shipRec.SlotIndex);
            RebuildOwnedOutfitsFromMarket.Run();
            WorldState.HudStatusPanelDirty = 1;
        }

        // Pers ships mostly retire on death (EngagePlayerPersIndex only 1-in-8).
        if (shipRec.PersIndex != -1)
        {
            if (shipRec.PersIndex == ShipRecord.EngagePlayerPersIndex)
            {
                if ((short)SeedEvoRng.Run(8) == 0)
                {
                    GameData.Pers[shipRec.PersIndex].AvailableFlag = 0;
                }
            }
            else if ((GameData.Pers[shipRec.PersIndex].Flags & 2) == 0)
            {
                GameData.Pers[shipRec.PersIndex].AvailableFlag = 0;
            }
        }

        SpawnExplosion.Run(shipRec.PosX, shipRec.PosY, shipRec.SlotIndex, 2, 0);
        PlayPositionalSound.Run(-1, CombatSoundCells.WeaponHitSnd[3], 10, shipRec.PosX, shipRec.PosY, player.PosX, player.PosY);
        spriteNode.SpritePtr = 0;
        spriteNode.UpdateUpp = 0;
        shipRec.IsActive = 0;
        shipRec.HasWorldSpriteNode = 0;
        shipRec.OwnerSlot = -1;
    }

    // FUN_1001eb2c 14015-14023 — a kill fails an active, not-yet-failed grudge mission
    // whose goal is Disable/Escort, or Board/RescueDisabled while the hull is unsalvaged.
    private static bool KillFailsGrudgeMission(MissionRecord mission, MissionStateRecord missionState, ShipRec ship) =>
        missionState.IsActive != 0 && mission.DestroyedShipCount == 0 &&
        (mission.MissionGoalType == MissionGoalKind.Disable ||
         (mission.MissionGoalType == MissionGoalKind.Board && ship.SalvageClaimed == 0) ||
         (mission.MissionGoalType == MissionGoalKind.RescueDisabled && ship.SalvageClaimed == 0) ||
         mission.MissionGoalType == MissionGoalKind.Escort) &&
        missionState.Failed == 0;
}
