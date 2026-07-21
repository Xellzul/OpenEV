using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10025074 (EV Override-11.c 15967-16525): the per-frame AI/physics tick for ONE ship
// slot (player = slot 0, NPCs = 1..35), run every frame from Misc.Tick. Run() is the decompile's
// straight-line body split into one named step per phase, called in the original order. Field
// byte-offsets are documented on the ShipRecord fields; only non-obvious addressing (deliberate
// deviations, faithful dead code) is noted at the use site.
public static class UpdateShipAiFrame
{
    public static void Run(ShipRec s)
    {
        var player = ShipTable.Player;

        DropInactiveTarget(s);
        DecayReloadTimers(s);
        if (ShipDerivedStats.IsDisabled(s))
            DemoteDisabledToDerelict(s);
        if (s.PersIndex != -1)
            RunPersHailing(s, player);
        if (s.IsTractored != 0)
            TractorMatchToPlayer(s, player);

        double timeScale = WorldState.TimeScale;
        s.PosX += (float)((double)s.VelX * timeScale);
        s.PosY += (float)((double)s.VelY * timeScale);

        if (!ShipDerivedStats.IsDisabled(s))
            TurnTowardHeadingAndRecharge(s);
        WrapHeading(s);
        ApplyWindupLunge(s);

        if (s.AiActionTimer < 1 || s.ShipClass == ShipRecord.EmptyShipClass)
        {
            IntegrateVelocity(s);
            FireSelectedWeapon(s, player);
        }
        else
        {
            s.AiActionTimer = (short)(s.AiActionTimer - 1);
        }

        TickDeathTimerAndMine(s, player);
        RegenAndClampFuel(s);
        s.IsTractored = 0;
    }

    private static void DropInactiveTarget(ShipRec s)
    {
        if (s.TargetSlot != -1 && GameData.Ships[s.TargetSlot].IsActive == 0)
            s.TargetSlot = -1;
    }

    private static void DecayReloadTimers(ShipRec s)
    {
        float reloadFloor = ShipStatConstants.ZeroFloat;
        for (int slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
        {
            if (s.WeaponSlotReload[slot] <= reloadFloor)
                s.WeaponSlotReload[slot] = reloadFloor;
            else
                s.WeaponSlotReload[slot] -= ShipStatConstants.OneFloat;
        }
    }

    private static void DemoteDisabledToDerelict(ShipRec s)
    {
        s.VelX *= ShipStatConstants.VelocityDampingFactor;
        s.VelY *= ShipStatConstants.VelocityDampingFactor;
        if (s.GrudgeMissionIndex == -1 && s.DefendedSpobIndex == -1 && s.OwnerSlot == 0)
        {
            if (GameData.ShipClasses[s.ShipClass].InherentAI < ShipAiType.Warship)
                RedistributeCargoAmongShips.Run(s.SlotIndex);
            s.OwnerSlot = -1;
            s.IsCarriedFighter = 0;
            s.SalvageClaimed = 1;
            s.AiBehaviorType = GameData.ShipClasses[s.ShipClass].InherentAI;
            Outfit.RebuildOwnedOutfitsFromMarket.Run();
            WorldState.HudStatusPanelDirty = 1;
        }
    }

    // Pers (named-captain) hailing.
    private static void RunPersHailing(ShipRec s, ShipRec player)
    {
        var pers = GameData.Pers[s.PersIndex];
        if (pers.HailQuote != -1
            && pers.AvailableFlag != 0
            && player.JumpWindupTimer < 1
            && !WorldState.IsCloaked
            && !ShipDerivedStats.IsDyingOrDestroyed(s)
            && !ShipAi.IsWindupAtSubstep(s))
        {
            bool allowFire = true;
            var flags = (PersFlags)(ushort)pers.Flags;
            if (ShipDerivedStats.IsDisabled(s))
            {
                if ((flags & PersFlags.HailOnlyWhileDisabled) == 0)
                    allowFire = false;
            }
            else if ((flags & PersFlags.HailOnlyWhileDisabled) != 0)
            {
                allowFire = false;
            }
            if ((flags & PersFlags.SuppressHail) != 0)
                allowFire = false;
            if ((flags & PersFlags.RequireMissionAccepted) != 0 && pers.AcceptedFlag == 0)
                allowFire = false;
            if ((flags & PersFlags.RequireEngagingPlayer) != 0 && !IsPlayerEngagementTarget.Run(s))
                allowFire = false;
            if (pers.LinkMission != -1 && (flags & PersFlags.RequiresBarMissionEligible) != 0)
            {
                Graphics.Model.RenderGlobals.DrawGateFlag = 1;
                WorldState.CurrentTargetShipId = s.SlotIndex;
                if (!Mission.IsBarPersEligible.Run(pers.LinkMission))
                    allowFire = false;
                Graphics.Model.RenderGlobals.DrawGateFlag = 0;
                WorldState.CurrentTargetShipId = -1;
            }
            if ((flags & PersFlags.LeaveAfterMissionAccept) != 0 && ShipAi.IsStateInert(s))
                allowFire = false;
            if ((flags & PersFlags.SuppressForPlayerAiTier1) != 0 && GameData.ShipClasses[player.ShipClass].InherentAI == ShipAiType.WimpyTrader)
                allowFire = false;
            if ((flags & PersFlags.SuppressForPlayerAiTier2) != 0 && GameData.ShipClasses[player.ShipClass].InherentAI == ShipAiType.BraveTrader)
                allowFire = false;
            if ((flags & PersFlags.SuppressForPlayerAiTierAbove2) != 0 && ShipAiType.BraveTrader < GameData.ShipClasses[player.ShipClass].InherentAI)
                allowFire = false;
            if ((flags & PersFlags.SayOnce) != 0 && s.HailQuoteSpoken != 0)
                allowFire = false;

            if (allowFire && Misc.SeedEvoRng.Run(140) == 0 && WorldState.FlashChatterCountdown < 1)
            {
                WorldState.CurrentTargetShipId = s.SlotIndex;
                Sound.SpeakPersHailLine.Run((int)pers.HailQuote);
                WorldState.CurrentTargetShipId = -1;
                s.HailQuoteSpoken = 1;
            }
        }
    }

    private static void TractorMatchToPlayer(ShipRec s, ShipRec player)
    {
        double step = ShipDerivedStats.EffectiveAccel(player);
        step = (double)(float)(ShipStatConstants.SpriteBoundsScale * step);
        if ((float)((double)player.VelX + step) < s.VelX)
            s.VelX = (float)((double)s.VelX - step);
        if (s.VelX < (float)((double)player.VelX - step))
            s.VelX = (float)((double)s.VelX + step);
        if ((float)((double)player.VelY + step) < s.VelY)
            s.VelY = (float)((double)s.VelY - step);
        if (s.VelY < (float)((double)player.VelY - step))
            s.VelY = (float)((double)s.VelY + step);
        if (EvMath.FloatAbs((double)(s.VelX - player.VelX)) < step)
            s.VelX = player.VelX;
        if (EvMath.FloatAbs((double)(s.VelY - player.VelY)) < step)
            s.VelY = player.VelY;
    }

    private static void TurnTowardHeadingAndRecharge(ShipRec s)
    {
        if (s.AiActionTimer < 1)
        {
            // Snap to HeadingPrev if within one maneuver step, else step the shorter way around the 360 circle.
            double headingDelta = EvMath.FloatAbs((float)s.HeadingPrev - (float)s.Heading);
            short maneuver = (short)ShipDerivedStats.EffectiveManeuver(s);
            if (headingDelta <= (float)maneuver)
            {
                s.Heading = s.HeadingPrev;
            }
            else
            {
                float diff = (float)(s.HeadingPrev - s.Heading);
                if (ShipStatConstants.AngleWrapPeriod <= diff)
                    diff -= ShipStatConstants.AngleWrapPeriod;
                if (diff < ShipStatConstants.ZeroFloat)
                    diff += ShipStatConstants.AngleWrapPeriod;
                maneuver = (short)ShipDerivedStats.EffectiveManeuver(s);
                if (diff <= ShipStatConstants.HalfAngleWrap)
                    s.Heading = (short)(s.Heading + maneuver);
                else
                    s.Heading = (short)(s.Heading - maneuver);
            }
        }

        // Shield (+0x68) is an int-valued float (positive = shield, negative = armor damage); recharges
        // 1% of max per interval while below max. The decompile's `-param_1[0x1a]` (source 16156) looks
        // like it negates the current shield, but it doesn't: the decompile typed the field float*, so the
        // ASM's sign-bit XOR (the ordinary signed int->double conversion) prints as unary minus — there
        // is no neg/fneg on this path. Keep this un-negated; a real negation would insta-explode a
        // full-shield ship on its first recharge tick.
        double maxShield = (double)(int)ShipDerivedStats.EffectiveShieldMax(s);
        double curShield = (double)(int)s.Shield;
        if ((double)(float)curShield < (double)(float)maxShield)
        {
            short recharge = (short)ShipDerivedStats.EffectiveShieldRecharge(s);
            int tick = (int)WorldState.GameFrameTickCounter;
            if (0 < recharge && tick == (tick / recharge) * recharge)
            {
                s.Shield = (float)(int)(ShipStatConstants.ProjectionOuterScale
                                        * (double)(float)maxShield + (double)(int)s.Shield);
            }
        }
    }

    private static void WrapHeading(ShipRec s)
    {
        if (359 < s.Heading)
            s.Heading = (short)(s.Heading - 360);
        if (s.Heading < 0)
            s.Heading = (short)(s.Heading + 360);
    }

    private static void ApplyWindupLunge(ShipRec s)
    {
        if (s.JumpWindupTimer <= 0 || ShipDerivedStats.IsDisabled(s))
            return;
        short maneuver = (short)ShipDerivedStats.EffectiveManeuver(s);
        if (Abs((int)s.Heading - (int)s.HeadingPrev) > maneuver)
            return;
        if (!ShipAi.IsWindupAtSubstep(s))
        {
            s.JumpWindupTimer = 0;
            return;
        }
        s.VelX *= ShipStatConstants.VelocityDampingD;
        s.VelY *= ShipStatConstants.VelocityDampingD;
        int now = (int)MacToolbox.TickCount();
        // Ticks since the windup stamp. (unsigned int->double cast)
        // == (double)(uint)x; AiTickStamp is raw int (see ShipRecord).
        double dt = (double)(uint)(now - s.AiTickStamp);
        float scale = GameData.ShipClasses[s.ShipClass].SpriteScale;
        double lunge = (double)(float)((double)(scale * (float)dt)
                                       / ShipStatConstants.AiFrameScale4p6
                                       - ShipStatConstants.DamageDivisor / (double)scale);
        if (ShipStatConstants.AimDistanceMax < lunge)
        {
            float px = s.PosX, py = s.PosY;
            EvMath.OffsetByHeading(lunge, (int)s.Heading, ref px, ref py);
            s.PosX = px;
            s.PosY = py;
        }
    }

    private static void IntegrateVelocity(ShipRec s)
    {
        if (ShipStatConstants.AimDistanceMax == (double)s.DesiredAccel)
            return;
        if (ShipStatConstants.AimDistanceMax == (double)s.DesiredSpeed)
        {
            EvMath.AccelerateAlongHeading((double)s.DesiredAccel,
                ShipDerivedStats.EffectiveSpeed(s), (int)s.Heading, s);
        }
        else if ((double)s.DesiredSpeed <= ShipStatConstants.AimDistanceMax)
        {
            s.VelY = 0f;
            s.VelX = 0f;
            // Restored: the decompile dropped the f1 magnitude (FloatAbs(|maxSpeed|)) from FUN_100586f0.
            float vx = s.VelX, vy = s.VelY;
            EvMath.OffsetByHeading(EvMath.FloatAbs((double)s.DesiredSpeed), (int)s.Heading, ref vx, ref vy);
            s.VelX = vx;
            s.VelY = vy;
            s.DesiredSpeed = (float)((double)s.DesiredSpeed + EvMath.FloatAbs((double)s.DesiredAccel));

            double speed;
            if (s.OwnerSlot == -1 || 35 < s.OwnerSlot)
            {
                speed = (double)ShipDerivedStats.EffectiveSpeed(s);
            }
            else
            {
                double leaderSpeed = (double)ShipDerivedStats.EffectiveSpeed(ShipTable.Ships[s.OwnerSlot]);
                double ownSpeed = (double)ShipDerivedStats.EffectiveSpeed(s);
                speed = leaderSpeed <= ownSpeed ? leaderSpeed : ownSpeed;
            }
            if (-speed <= (double)s.DesiredSpeed)
            {
                ResetToOriginUnlessJumping.Run(s);
                if (s.OwnerSlot == -1)
                    s.AiActionTimer = (short)(Misc.SeedEvoRng.Run(30) + 60);
            }
        }
        else
        {
            EvMath.AccelerateAlongHeading((double)s.DesiredAccel, (double)s.DesiredSpeed, (int)s.Heading, s);
        }
    }

    private static void FireSelectedWeapon(ShipRec s, ShipRec player)
    {
        if (s.HasSelectedWeapon == 0 || s.SelectedWeaponSlot == -1)
            return;
        var weapon = GameData.Weapons[s.SelectedWeaponSlot];
        short shotsFired = 0;
        if (s.WeaponSlotReload[s.SelectedWeaponSlot] <= 0f
            && 0 < s.WeaponSlotType[s.SelectedWeaponSlot])
        {
            short shotsPerBurst = ((WeaponFlags)weapon.Flags & WeaponFlags.FiresFromAllMatchingSlots) == 0
                ? (short)1
                : s.WeaponSlotType[s.SelectedWeaponSlot];
            for (short burst = 0; burst < shotsPerBurst; burst++)
            {
                if (!ShipDerivedStats.CanFireWeapon(s, s.SelectedWeaponSlot))
                    continue;
                var weaponType = (WeaponGuidanceType)weapon.GuidanceType;

                // Turreted beam / turreted-unguided: need a target, a not-blind check, and in-range.
                if (s.TargetSlot != -1 && (weaponType == WeaponGuidanceType.TurretedBeam || weaponType == WeaponGuidanceType.TurretedUnguided)
                    && !IsTurretBlindToTarget.Run(s, ShipTable.Ships[s.TargetSlot], s.SelectedWeaponSlot))
                {
                    int range = 0;
                    if (weaponType == WeaponGuidanceType.TurretedBeam)
                        range = (int)weapon.ProjectileSpeed;
                    if (weaponType == WeaponGuidanceType.TurretedUnguided)
                        range = (int)(ShipStatConstants.CollisionSlopeFactor
                            * (double)(weapon.ProjectileSpeed * (float)weapon.Lifetime));
                    if (EvMath.FloatAbs((double)(s.PosX - GameData.Ships[s.TargetSlot].PosX)) < (float)(short)range
                        && EvMath.FloatAbs((double)(s.PosY - GameData.Ships[s.TargetSlot].PosY)) < (float)(short)range)
                    {
                        if (weaponType == WeaponGuidanceType.TurretedBeam)
                        {
                            // HeadingBetween consumes target PosX/PosY as FLOATS, not their int bit-patterns.
                            int angle = EvMath.HeadingBetween(s.PosX, s.PosY,
                                GameData.Ships[s.TargetSlot].PosX, GameData.Ships[s.TargetSlot].PosY);
                            AllocateBeamSlot.Run(s.SlotIndex, s.TargetSlot, s.SelectedWeaponSlot, angle, 0, 0);
                        }
                        else
                        {
                            SpawnFromShip.Run((int)s.SlotIndex, s.TargetSlot, (int)s.SelectedWeaponSlot);
                        }
                        shotsFired++;
                    }
                }

                // Front / rear quadrant turret: fire only when the bearing is within ~46 of heading.
                if (s.TargetSlot != -1 && (weaponType == WeaponGuidanceType.FrontQuadrantTurret || weaponType == WeaponGuidanceType.RearQuadrantTurret))
                {
                    int range = (int)(ShipStatConstants.CollisionSlopeFactor
                        * (double)(weapon.ProjectileSpeed * (float)weapon.Lifetime));
                    if (EvMath.FloatAbs((double)(s.PosX - GameData.Ships[s.TargetSlot].PosX)) < (float)(short)range
                        && EvMath.FloatAbs((double)(s.PosY - GameData.Ships[s.TargetSlot].PosY)) < (float)(short)range)
                    {
                        short bearing = (short)EvMath.HeadingBetween(s.PosX, s.PosY,
                            GameData.Ships[s.TargetSlot].PosX, GameData.Ships[s.TargetSlot].PosY);
                        int angleDelta = 0;
                        if (weaponType == WeaponGuidanceType.FrontQuadrantTurret)
                            angleDelta = Abs((int)bearing - (int)s.Heading) % 360;
                        if (weaponType == WeaponGuidanceType.RearQuadrantTurret)
                            angleDelta = Abs((int)bearing - (s.Heading + 180) % 360) % 360;
                        if ((short)angleDelta < 46)
                        {
                            SpawnFromShip.Run((int)s.SlotIndex, s.TargetSlot, (int)s.SelectedWeaponSlot);
                            shotsFired++;
                        }
                    }
                }

                // Homing: unconditional shot at the target.
                if (s.TargetSlot != -1 && weaponType == WeaponGuidanceType.HomingWeapon)
                {
                    SpawnFromShip.Run((int)s.SlotIndex, s.TargetSlot, (int)s.SelectedWeaponSlot);
                    shotsFired++;
                }

                // Unguided projectile / beam / freeflight rocket (no target required).
                if (weaponType == WeaponGuidanceType.UnguidedProjectile || weaponType == WeaponGuidanceType.BeamWeapon || weaponType == WeaponGuidanceType.FreeflightRocket)
                {
                    // Faithful dead computations (the original discards these into unaff_r28).
                    if (weaponType == WeaponGuidanceType.BeamWeapon)
                        _ = (int)weapon.ProjectileSpeed;
                    if (weaponType == WeaponGuidanceType.UnguidedProjectile || weaponType == WeaponGuidanceType.FreeflightRocket)
                        _ = (int)(ShipStatConstants.CollisionSlopeFactor
                            * (double)(weapon.ProjectileSpeed * (float)weapon.Lifetime));
                    if (weaponType == WeaponGuidanceType.BeamWeapon)
                    {
                        int cost = WeaponShotSpread.Run(s, s.SelectedWeaponSlot);
                        AllocateBeamSlot.Run(s.SlotIndex, s.TargetSlot, s.SelectedWeaponSlot, (int)s.Heading,
                            (byte)((short)cost == 0 ? 1 : 0), (short)cost);
                    }
                    else
                    {
                        SpawnFromShip.Run((int)s.SlotIndex, s.TargetSlot, (int)s.SelectedWeaponSlot);
                    }
                    shotsFired++;
                }

                // Ammo / fuel cost for the shot (KamikazePersIndex = no-consume sentinel).
                if (weapon.AmmoLink != -1 && s.PersIndex != ShipRecord.KamikazePersIndex)
                {
                    if (weapon.AmmoLink < 1)
                    {
                        // Fuel-using weapon: cost = abs(ammoField) - 1000.
                        if (weapon.AmmoLink < -999)
                            s.Fuel -= (float)(Abs((int)weapon.AmmoLink) - 1000);
                    }
                    else
                    {
                        s.WeaponSlotAmmo[s.SelectedWeaponSlot] = (short)(s.WeaponSlotAmmo[s.SelectedWeaponSlot] - 1);
                    }
                }
            }
        }

        if (0 < shotsFired)
        {
            var weaponType = (WeaponGuidanceType)weapon.GuidanceType;
            if (-1 < weapon.FireSound)
            {
                int effect = Sound.Model.CombatSoundCells.WeaponSoundTable[weapon.FireSound];
                if (((WeaponFlags)weapon.Flags & WeaponFlags.FireSoundLoopedSingleInstance) == 0)
                {
                    int channels = (weaponType == WeaponGuidanceType.BeamWeapon || weaponType == WeaponGuidanceType.TurretedBeam) ? 3 : -1;
                    Sound.PlayPositionalSound.Run(channels, effect, 4, s.PosX, s.PosY, player.PosX, player.PosY);
                }
                else if (Sound.CountMatchingSoundVoices.Run(effect) == 0)
                {
                    Sound.PlayPositionalSound.Run(-1, effect, 4, s.PosX, s.PosY, player.PosX, player.PosY);
                }
            }

            // Reload timer; scaled UP when firing at the player, by the player's combat rating.
            float reload = (float)shotsFired * (weapon.ReloadTime / (float)s.WeaponSlotType[s.SelectedWeaponSlot]);
            if (s.TargetSlot == 0)
            {
                int diff = WorldState.PlayerCombatRating;
                if (diff < 100)
                    reload *= ShipStatConstants.ReloadSkillScale4;
                else if (diff < 400)
                    reload *= ShipStatConstants.ReloadSkillScale3;
                else if (diff < 800)
                    reload *= ShipStatConstants.ReloadSkillScale2;
                else if (diff < 1600)
                    reload *= ShipStatConstants.ReloadSkillScale1;
            }
            s.WeaponSlotReload[s.SelectedWeaponSlot] = reload;
            s.AltFireSide = s.AltFireSide < 0 ? (short)1 : (short)-1;
        }
    }

    private static void TickDeathTimerAndMine(ShipRec s, ShipRec player)
    {
        if (!(0f < s.DeathTimer))   // exact complement of the decompile's `0f < DeathTimer` guard (NaN-faithful)
            return;
        s.DeathTimer -= ShipStatConstants.OneFloat;
        if (s.OwnerSlot == 0)
        {
            if (s.AiBehaviorType == ShipAiType.Escort && GameData.ShipClasses[s.ShipClass].InherentAI < ShipAiType.Warship)
            {
                RedistributeCargoAmongShips.Run(s.SlotIndex);
                WorldState.HudStatusPanelDirty = 1;
                Graphics.RedrawHudStatusPanel.Run();
            }
            s.OwnerSlot = -1;
            s.AiBehaviorType = GameData.ShipClasses[s.ShipClass].InherentAI;
        }

        double deathDelay = GameData.ShipClasses[s.ShipClass].DeathDelay;
        if ((double)s.DeathTimer != ShipStatConstants.DamageRandScale * deathDelay || s.PersIndex == -1)
            return;

        bool allowMine;
        if (((PersFlags)(ushort)GameData.Pers[s.PersIndex].Flags & PersFlags.PodAndAfterburner) == 0)
            allowMine = false;
        else if (s.Govt == -1)
            allowMine = true;
        else if ((GameData.Governments[s.Govt].Flags & GovtFlags.PersNoEscapePod) == 0)
            allowMine = true;
        else
            allowMine = false;
        if (!allowMine)
            return;

        short mine = (short)AllocateShipSlot.Run(player.CurrentSystem, 2);
        if (mine == -1)
            return;
        GameData.Ships[mine].PosX = s.PosX;
        GameData.Ships[mine].PosY = s.PosY;
        GameData.Ships[mine].VelX = 0f;
        GameData.Ships[mine].VelY = 0f;
        GameData.Ships[mine].Heading = s.Heading;
        GameData.Ships[mine].ShipClass = ShipRecord.EmptyShipClass;
        GameData.Ships[mine].Govt = -1;
        GameData.Ships[mine].OwnerSlot = -1;
        GameData.Ships[mine].AiBehaviorType = ShipAiType.WimpyTrader;
        GameData.Ships[mine].AiActionTimer = 1000;
        GameData.Ships[mine].Shield = (float)(int)ShipDerivedStats.EffectiveShieldMax(ShipTable.Ships[mine]);
        Sound.PlayPositionalSound.Run(-1, Sound.Model.CombatSoundCells.ScanSweepSnd, 5,
            GameData.Ships[mine].PosX, GameData.Ships[mine].PosY, player.PosX, player.PosY);
    }

    private static void RegenAndClampFuel(ShipRec s)
    {
        short fuelRegen = GameData.ShipClasses[s.ShipClass].FuelRegen;
        if (0 < fuelRegen && Misc.SeedEvoRng.Run(fuelRegen) == 0)
            s.Fuel += ShipStatConstants.OneFloat;

        float maxFuel = (float)(short)ShipDerivedStats.EffectiveFuelMax(s);
        if (maxFuel < s.Fuel)
            s.Fuel = maxFuel;
        if (s.Fuel < 0f)
            s.Fuel = 0f;
    }

    // Branchless |v| in the original (the (x>>31) sign-mask idiom); bit-identical for the small deltas here.
    private static int Abs(int v) => v < 0 ? -v : v;
}
