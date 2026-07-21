namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

// FUN_10063984 — EV Override-11.c lines 41694-41993. Apply one weapon hit to a ship: knock it back
// by impactCount/mass, subtract the hit off shields first then hull, award the player combat rating
// + flood the legal-status kill impact on disable/destroy, flip a freshly-killed NPC to its
// wreck/escape AI, then — when addToThreat is set — run the retaliation gate that decides whether the
// victim turns on its attacker. The retaliation half is split into HitCanProvoke / VictimRetaliates /
// EngageAttacker, each tagged with the decompile lines it covers.
public static class ApplyShipDamage
{
    // The decompile signature stops at addToThreat, but FUN_10063984 reads three more stack args
    // (0x3b/0x3f/0x43) that every caller does pass (verified vs the ASM): hitIntendedTarget — the
    // struck ship is the shot's locked target, so the collateral retaliation-refinement is skipped;
    // applyOverflowScaling (0x3f) + clampLossToShield (0x43) — re-scale overkill deeper into the hull
    // / clamp the hull loss to the remaining shield.
    public static void Run(ShipRec s, float hitX, float hitY, short impactCount, short shieldDamage,
                           short armorDamage, short attackerIndex, byte addToThreat,
                           bool hitIntendedTarget, bool applyOverflowScaling, bool clampLossToShield)
    {
        if (s.ShipClass == ShipRecord.EmptyShipClass)   // skip empty ship slots
            return;
        var attacker = ShipTable.Ships[attackerIndex];
        var wasDisabledBefore = ShipDerivedStats.IsDisabled(s);

        // Knock-back from the impact (skipped while the ship is mid-jump-windup).
        if (0 < impactCount && s.JumpWindupTimer < 1)
        {
            var impactHeading = EvMath.HeadingBetween(hitX, hitY, s.PosX, s.PosY);
            EvMath.AccelerateAlongHeading(
                (double)((float)impactCount / (float)GameData.ShipClasses[s.ShipClass].Mass),
                (double)GameData.ShipClasses[s.ShipClass].Speed,
                impactHeading, s);
            // Clamp the post-knockback velocity to the ship's effective max speed.
            EvMath.ClampVector(ShipDerivedStats.EffectiveSpeed(s), s);
        }

        if ((int)s.Shield < 0)
        {
            // Faithful operand swap: shields-down scales armorDamage, shields-up scales shieldDamage
            // (decompile 41740 vs 41769) — looks like a copy-paste slip but matches the original.
            var damageAmount = (int)(ShipStatConstants.DamageScaleX * armorDamage + shieldDamage);
            if (damageAmount < 1 && (0 < shieldDamage || 0 < armorDamage))
                damageAmount = 1;
            // Overflow re-scale (when the hit overkills past the armor floor). The ASM converts Shield
            // with the signed int→double magic — positive (double)(int)s.Shield, NOT negated; the decompile's
            // `-param_1[0x1a]` is its float-field rendering of that sign-bit flip. Scale consts are the
            // real doubles 0.6 / 0.85 (dbl_823B0 / dbl_823B8).
            if (applyOverflowScaling &&
                (int)s.Shield - damageAmount < -(int)(short)ShipDerivedStats.EffectiveArmorMax(s))
            {
                var armorMax = (short)ShipDerivedStats.EffectiveArmorMax(s);
                var classFlags = GameData.ShipClasses[s.ShipClass].Flags;
                if ((classFlags & ShipFlags.DisabledAt10PctArmor) == 0)
                    damageAmount = (int)(ShipStatConstants.ArmorLossScaleX * armorMax + (double)(int)s.Shield);
                else
                    damageAmount = (int)(ShipStatConstants.ArmorLossScaleY * armorMax + (double)(int)s.Shield);
            }
            if (damageAmount < 0)
                damageAmount = 0;
            s.Shield = (float)((int)s.Shield - damageAmount);
        }
        else
        {
            var armorLoss = (float)(int)(ShipStatConstants.DamageScaleX * shieldDamage + armorDamage);
            if ((int)armorLoss < 1 && (0 < shieldDamage || 0 < armorDamage))
            {
                // Integer-1 minimum-damage floor (ASM `li r27, 1`) — same as the shields-down branch's
                // `damageAmount = 1`; r27 and the Shield store at 0x68 are integer throughout. The
                // decompile renders this as the float bit-pattern 1.4013e-45 only because the
                // clampLossToShield branch below assigns the Shield float field to this variable, widening
                // it to float — do NOT restore that literal: (int)1.4013e-45f == 0 silently defeats the floor.
                armorLoss = 1.0f;
            }
            // Overflow re-scale — see the shields-down note above (positive (double)(int)s.Shield, real doubles).
            if (applyOverflowScaling &&
                (int)s.Shield - (int)armorLoss < -(int)(short)ShipDerivedStats.EffectiveArmorMax(s))
            {
                var armorMax = (short)ShipDerivedStats.EffectiveArmorMax(s);
                var classFlags = GameData.ShipClasses[s.ShipClass].Flags;
                if ((classFlags & ShipFlags.DisabledAt10PctArmor) == 0)
                    armorLoss = (float)(int)(ShipStatConstants.ArmorLossScaleX * armorMax + (double)(int)s.Shield);
                else
                    armorLoss = (float)(int)(ShipStatConstants.ArmorLossScaleY * armorMax + (double)(int)s.Shield);
            }
            if (clampLossToShield && (int)s.Shield - (int)armorLoss < 0)
                armorLoss = s.Shield;
            if ((int)armorLoss < 0)
                armorLoss = 0.0f;
            s.Shield = (float)((int)s.Shield - (int)armorLoss);
        }

        // Legal-status flood + player combat-rating award (player or player's escort kills, on a
        // non-defending, non-special-pers ship).
        if ((attackerIndex == 0 || attacker.OwnerSlot == 0) &&
            s.DefendedSpobIndex == -1 &&
            s.PersIndex != ShipRecord.KamikazePersIndex && s.PersIndex != ShipRecord.EngagePlayerPersIndex)
        {
            if (ShipDerivedStats.IsDisabled(s) && !wasDisabledBefore)
                FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem, s.Govt, 1, s.GrudgeMissionIndex);
            if ((int)s.Shield < -(int)(short)ShipDerivedStats.EffectiveArmorMax(s))
                FloodVisitedSystsConditional.Run(GameData.Player.CurrentSystem, s.Govt, 3, s.GrudgeMissionIndex);
            if ((int)s.Shield < -(int)(short)ShipDerivedStats.EffectiveArmorMax(s) &&
                0 < WorldState.PlayerCombatRating + (int)GameData.ShipClasses[s.ShipClass].Crew &&
                WorldState.PlayerCombatRating + (int)GameData.ShipClasses[s.ShipClass].Crew < 32000)
            {
                WorldState.PlayerCombatRating += (int)GameData.ShipClasses[s.ShipClass].Crew;
            }
        }

        // A freshly-killed NPC: mission-failure chatter for an active mission-state ship, and flip an
        // escort-of-player wreck back to its inherent AI.
        if (ShipDerivedStats.IsDisabled(s))
        {
            if (s.GrudgeMissionIndex != -1 && s.DefendedSpobIndex == -1 && !wasDisabledBefore &&
                GameData.MissionStates[s.GrudgeMissionIndex].IsActive != 0)
            {
                if (GameData.MissionStates[s.GrudgeMissionIndex].Failed == 0 &&
                    GameData.Missions[s.GrudgeMissionIndex].DisabledShipCount == 0 &&
                    GameData.Missions[s.GrudgeMissionIndex].MissionGoalType == MissionGoalKind.Escort)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                    EnqueueChatterEvent.Run("Mission failed.", 240, 0, 12, UiColors.ChatterText, 0, 0);
                    MarkMissionFailed.Run(s.GrudgeMissionIndex);
                }
                // Tally this disabled ship toward the mission's disable goal.
                GameData.Missions[s.GrudgeMissionIndex].DisabledShipCount =
                    (short)(GameData.Missions[s.GrudgeMissionIndex].DisabledShipCount + 1);
            }
            if (s.OwnerSlot == 0 && s.GrudgeMissionIndex == -1)
            {
                s.OwnerSlot = -1;
                if (s.AiBehaviorType == ShipAiType.Escort && GameData.ShipClasses[s.ShipClass].InherentAI < ShipAiType.Warship)
                {
                    RedistributeCargoAmongShips.Run(s.SlotIndex);
                    WorldState.HudStatusPanelDirty = 1;
                    RedrawHudStatusPanel.Run();
                }
                s.AiBehaviorType = GameData.ShipClasses[s.ShipClass].InherentAI;
            }
        }

        // Retaliation: a threat-adding hit may turn the victim on its attacker — gate, decide, commit.
        // (FUN_10063984 41850-41990.)
        if (HitCanProvoke(s, attacker, attackerIndex, addToThreat) &&
            VictimRetaliates(s, attacker, attackerIndex, hitIntendedTarget))
        {
            EngageAttacker(s, attackerIndex, shieldDamage, armorDamage);
        }
        if (s.SlotIndex == 0)
            WorldState.PlayerShieldBarDirty = 1;
    }

    // FUN_10063984 41850-41853 — is this hit even a candidate to provoke retaliation? It must add
    // threat, the victim must have an active AI, and it must not be pure self-damage (unless a fleet
    // relation is involved). Pure — short-circuits VictimRetaliates when false.
    private static bool HitCanProvoke(ShipRec s, ShipRec attacker, short attackerIndex, byte addToThreat) =>
        addToThreat != 0 && s.AiBehaviorType > 0 &&
        (attackerIndex != s.SlotIndex || attacker.DefendedSpobIndex != -1 || s.DefendedSpobIndex != -1);

    // FUN_10063984 41854-41945 — decide whether the victim turns hostile. Walks the govt / fleet /
    // legal-status / player-leniency gates; the two SeedEvoRng rolls (and their short-circuits) are
    // kept exactly so the RNG state advances identically to the original.
    private static bool VictimRetaliates(ShipRec s, ShipRec attacker, short attackerIndex, bool skipRetaliationRefine)
    {
        // skipRetaliationRefine (arg 9, stack 0x3b): set when the hit landed on the shot's intended
        // target, so the collateral-hit refinement below (the 49/50 + 24/25 leniency rolls + the gövt
        // ShootPenalty gate, its sole reader) is skipped and the on-target hit provokes in full. A
        // stray/collateral hit or a blast clears it, running the leniency rolls.
        bool shouldRetaliate;
        if (s.DefendedSpobIndex == -1 && attacker.DefendedSpobIndex == -1)
        {
            shouldRetaliate = s.Govt != attacker.Govt;
            // Decompile 41858-41864: the &&-chain's middle term is a comma-operator whose VALUE is
            // `attacker.OwnerSlot != -1` and whose side effect is the shouldRetaliate assignment.
            // Unrolled to a nested if so the side-effect-then-test order is preserved.
            if (s.OwnerSlot != -1)
            {
                shouldRetaliate = attackerIndex != s.OwnerSlot || shouldRetaliate;
                if (attacker.OwnerSlot != -1 && s.OwnerSlot != attacker.OwnerSlot)
                    shouldRetaliate = true;
            }
            if (s.Govt == -1)
            {
                if (attackerIndex == 0 || attacker.OwnerSlot == 0)
                    shouldRetaliate = true;
            }
            else
            {
                if (-1 < SystTable.Store[GameData.Player.CurrentSystem].Govt &&
                    (attackerIndex == 0 || attacker.OwnerSlot == 0) &&
                    GameData.Player.TargetSlot != s.SlotIndex &&
                    (int)GameData.Governments[s.Govt].CrimeTolerance << 1 <=
                        (int)GalaxyMapGlobals.SystemStatus(GameData.Player.CurrentSystem))
                {
                    shouldRetaliate = false;
                }
                if (s.Govt == attacker.Govt)
                    shouldRetaliate = false;
            }
            if (s.OwnerSlot != -1 && s.OwnerSlot == attacker.OwnerSlot && attacker.DefendedSpobIndex != -1)
                shouldRetaliate = false;
            if (!skipRetaliationRefine && shouldRetaliate)
            {
                if (attackerIndex == 0)
                {
                    if (GameData.Player.TargetSlot != -1)
                    {
                        if (s.SlotIndex == GameData.Player.TargetSlot)
                        {
                            shouldRetaliate = true;
                        }
                        else
                        {
                            var targetIsEnemy = ArePersEnemies.Run(s.Ptr,
                                ShipTable.Ships[GameData.Player.TargetSlot].Ptr);
                            if (!targetIsEnemy)
                            {
                                if ((short)SeedEvoRng.Run(50) != 0)   // 49/50: a stray shot at a non-target rarely provokes
                                    shouldRetaliate = false;
                            }
                            else
                            {
                                shouldRetaliate = false;
                            }
                        }
                    }
                }
                else if (0 < attackerIndex && s.SlotIndex != attacker.TargetSlot)
                {
                    shouldRetaliate = false;
                }
                if (shouldRetaliate && s.Govt != -1 && attackerIndex == 0 &&
                    GameData.Governments[s.Govt].ShootPenalty < 1 &&
                    (short)SeedEvoRng.Run(25) != 0)   // 24/25: ShootPenalty<1 govts usually don't fight back
                    shouldRetaliate = false;
            }
            if (attackerIndex != -1)
            {
                var distX = EvMath.FloatAbs((double)(s.PosX - GameData.Player.PosX));
                bool nearPlayerX = distX <= ShipStatConstants.SplashRangeMax;
                var distY = EvMath.FloatAbs((double)(s.PosY - GameData.Player.PosY));
                bool nearPlayerY = distY <= ShipStatConstants.SplashRangeMax;
                if (attacker.OwnerSlot == 0 && attacker.AiBehaviorType == ShipAiType.NavalFighter &&
                    s.TargetSlot == 0 && nearPlayerX && nearPlayerY)
                {
                    shouldRetaliate = false;
                }
                if (s.OwnerSlot == attacker.OwnerSlot && s.OwnerSlot != -1)
                    shouldRetaliate = false;
            }
            if (0 < s.JumpWindupTimer)
                shouldRetaliate = false;
            short attackerLeaderTarget = -1;
            short ownLeaderTarget = -1;
            if (s.OwnerSlot != -1)
                ownLeaderTarget = GameData.Ships[s.OwnerSlot].OwnerSlot;
            if (attacker.OwnerSlot != -1 && attackerIndex != 0)
                attackerLeaderTarget = GameData.Ships[attacker.OwnerSlot].OwnerSlot;
            if (ownLeaderTarget == attackerLeaderTarget && ownLeaderTarget != -1 && attackerLeaderTarget != -1)
                shouldRetaliate = false;
        }
        else if (s.DefendedSpobIndex == -1 || attacker.DefendedSpobIndex == -1)
        {
            shouldRetaliate = true;
        }
        else
        {
            shouldRetaliate = false;
        }
        return shouldRetaliate;
    }

    // FUN_10063984 41947-41990 — commit the retaliation: bank the damage into the anger accumulator,
    // target the attacker, alert defenders, and (vs the player) latch the pers' engage-accepted flag.
    private static void EngageAttacker(ShipRec s, short attackerIndex, short shieldDamage, short armorDamage)
    {
        if (s.DefendedSpobIndex == -1)
        {
            s.ProvokedFlag = (short)(shieldDamage + armorDamage + s.ProvokedFlag);
            s.TargetSlot = attackerIndex;
        }
        else
        {
            s.ProvokedFlag = (short)(s.ProvokedFlag + (shieldDamage + armorDamage) * 30);
            s.TargetSlot = 0;
        }
        if (attackerIndex == 0)
            ShipAi.CallForDefendersAndEngagePlayer(s);
        if (20 < s.AiActionTimer)
            s.AiActionTimer = 20;
        if (attackerIndex == 0 && s.DefendedSpobIndex == -1)
        {
            // A grudge-bearing pers, once the player fires on it, latches "accepted to engage the player".
            if (s.PersIndex != -1 && ((PersFlags)(ushort)GameData.Pers[s.PersIndex].Flags & PersFlags.Grudge) != 0)
                GameData.Pers[s.PersIndex].AcceptedFlag = 1;
            ShipAi.ClearHyperjumpReturnToIdle(s);
            s.TargetSlot = 0;
        }
    }
}
