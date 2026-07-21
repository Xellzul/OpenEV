// Port of FUN_1000366c (EV Override-11.c lines 2662-3448): the NPC steering/turn AI tick.
// Runs per non-player ship each frame (Misc.Tick -> DispatchAi). A per-frame prologue resets
// accel/max-speed and snapshots the heading, then control dispatches by the AI sub-state
// AiManeuverState to one handler per state. Each handler sets desired heading (HeadingPrev), accel
// (DesiredAccel) and max speed (DesiredSpeed), which UpdateShipAiFrame then integrates. A handler may
// advance AiManeuverState to a later state, which then also runs this frame (order preserved from the
// decompile's straight-line block sequence). Headings are in 360 units/rev (see FullCircle).
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

public static class UpdateShipAiSteering
{
    // EVO measures headings in 360 integer units per revolution (the decompile's 0x168);
    // 180 is a half-turn (reverse heading).
    private const int FullCircle = 360;
    private const int HalfCircle = 180;

    // |v| — faithful for all inputs incl. int.MinValue, where System.Math.Abs would throw.
    private static int Abs(int v) => v < 0 ? -v : v;

    public static void Run(ShipRec ship)
    {
        // Per-frame prologue (decompile 2683-2690): reset accel to zero, clamp the cached max
        // speed, recompute the disabled flag and snapshot the heading.
        ship.DesiredAccel = 0f;
        ship.HasSelectedWeapon = 0;
        if (0.0 <= ship.DesiredSpeed)
            ship.DesiredSpeed = (float)ShipDerivedStats.EffectiveSpeed(ship);
        byte isDisabled = (byte)(ShipDerivedStats.IsDisabled(ship) ? 1 : 0);
        ship.HeadingPrev = ship.Heading;

        // Dispatch by AI sub-state (AiManeuverState). Each handler self-gates and returns if not it.
        if (ship.AiManeuverState == ShipManeuverState.None) ShipAi.UpdateFollowParent(ship);
        DecelerateToStop(ship, isDisabled);
        ApproachNavSpob(ship, isDisabled);
        ReturnToOrigin(ship, isDisabled);
        JumpOutWindup(ship, isDisabled);
        CarriedFollowParent(ship, isDisabled);
        EvadeTarget(ship, isDisabled);
        AttackRun(ship, isDisabled);
        StrafingPass(ship, isDisabled);
        BreakoffPass(ship, isDisabled);
        MissileAttack(ship, isDisabled);
        Board(ship, isDisabled);
        HoldAndFire(ship, isDisabled);
        ChaseSlow(ship, isDisabled);
        Chase(ship, isDisabled);
        FormationFly(ship, isDisabled);
        DockInCarrier(ship, isDisabled);
        ClampToIdle(ship);
    }

    // a76 == 1: turn to face the current velocity vector, then decelerate.
    private static void DecelerateToStop(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.KillSpeed && isDisabled == 0)
        {
            double dist = EvMath.FloatAbs(ship.VelX);
            if (ShipStatConstants.AiVelSettleThreshold <= dist ||
                ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelY))
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                ship.HeadingPrev = (short)((short)(turnRate + HalfCircle) + (short)((turnRate + HalfCircle) / FullCircle) * -FullCircle);
                turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
                {
                    dist = EvMath.FloatAbs(ship.VelX);
                    if (ShipStatConstants.AiVelSnapThreshold <= dist ||
                        ShipStatConstants.AiVelSnapThreshold <= EvMath.FloatAbs(ship.VelY))
                    {
                        ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                    }
                    else
                    {
                        ship.DesiredAccel = (float)(ShipStatConstants.AiSettleAccelScale * ShipDerivedStats.EffectiveAccel(ship));
                        ship.VelX = ship.VelX * ShipStatConstants.VelocityDampingC;
                        ship.VelY = ship.VelY * ShipStatConstants.VelocityDampingC;
                    }
                }
            }
            else
            {
                ship.VelX = ship.VelX * ShipStatConstants.VelocityDampingB;
                ship.VelY = ship.VelY * ShipStatConstants.VelocityDampingB;
                ship.AiManeuverState = ShipManeuverState.None;
            }
            ShipAi.UpdateFollowParent(ship);
        }
    }

    // a76 == 2: turn toward the nav-target spob; if within the 500-unit arrival radius, set
    // approach accel/speed.
    private static void ApproachNavSpob(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.FlyToStellar && ship.NavTargetSpob != -1 && isDisabled == 0)
        {
            int navSpob = ship.NavTargetSpob;
            float destX = (float)GameData.Spobs[navSpob].XPos;
            float destY = (float)GameData.Spobs[navSpob].YPos;
            ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, destX, destY);
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
            {
                // dx/dy stay float (ASM computes fsubs, no int truncation).
                float dx = ship.PosX - (float)(int)GameData.Spobs[navSpob].XPos;
                float dy = ship.PosY - (float)(int)GameData.Spobs[navSpob].YPos;
                double dist = EvMath.FloatAbs(dx);
                // Arrival radius (spob-approach threshold).
                if (dist < 500.0)
                {
                    dist = EvMath.FloatAbs(dy);
                    if (dist < 500.0)
                    {
                        ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                        // Quarter-speed spob approach; the same constant doubles as
                        // TickDefenderAi's flee-shield fraction.
                        ship.DesiredSpeed = (float)(ShipStatConstants.DefenderFleeShieldFraction * ShipDerivedStats.EffectiveSpeed(ship));
                        return;
                    }
                }
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
            }
        }
    }

    // a76 == 3: turn toward the origin (0,0) and accelerate once aligned.
    private static void ReturnToOrigin(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.FlyToHyperExit && isDisabled == 0)
        {
            ship.HeadingPrev = (short)EvMath.HeadingBetween(0, 0, ship.PosX, ship.PosY);
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
            }
        }
    }

    // a76 == 4: jump/expire wind-up — once enough ticks elapse, decrement the govt fleet count and
    // remove the ship from the system.
    private static void JumpOutWindup(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.HyperJump && isDisabled == 0)
        {
            ship.HeadingPrev = (short)EvMath.HeadingBetween(0, 0, ship.PosX, ship.PosY);
            if (ship.JumpWindupTimer < 1)
            {
                ship.JumpWindupTimer = 1;
                ship.AiTickStamp = (int)MacToolbox.TickCount();
            }
            ship.JumpWindupTimer = (short)(ship.JumpWindupTimer + 1);
            int nowTicks = (int)MacToolbox.TickCount();
            // Ticks elapsed since the windup started: the decompile's UNSIGNED int->double idiom,
            // i.e. (double)(uint) of the tick delta (no sign-flip).
            float elapsedTicks = (float)(double)(uint)(nowTicks - ship.AiTickStamp);
            if (ShipStatConstants.AiJumpWindupTicks / GameData.ShipClasses[ship.ShipClass].SpriteScale <= elapsedTicks)
            {
                if (ship.GrudgeMissionIndex != -1 &&
                    GameData.Missions[ship.GrudgeMissionIndex].DestSystem != -6 &&
                    GameData.Missions[ship.GrudgeMissionIndex].MissionGoalType == MissionGoalKind.ChaseOff)
                {
                    var govt = GameData.Missions[ship.GrudgeMissionIndex];
                    govt.DepartedShipCount = (short)(govt.DepartedShipCount + 1);
                    if (0 < govt.SpawnCount)
                        govt.SpawnCount = (short)(govt.SpawnCount - 1);
                }
                ship.JumpWindupTimer = 0;
                ship.IsActive = 0;
                ship.CurrentSystem = -1;
            }
        }
    }

    // a76 == 13: carried/escort ship — inherit the parent's nav heading and bleed off velocity.
    private static void CarriedFollowParent(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.JumpWithParent && isDisabled == 0)
        {
            ship.NavTargetSpob = GameData.Ships[ship.OwnerSlot].NavTargetSpob;
            ship.HeadingPrev = GameData.Ships[ship.OwnerSlot].Heading;
            if (ship.JumpWindupTimer == 0)
            {
                ship.AiTickStamp = (int)MacToolbox.TickCount();
            }
            ship.VelX = ship.VelX * ShipStatConstants.VelocityDampingB;
            ship.VelY = ship.VelY * ShipStatConstants.VelocityDampingB;
            ship.DesiredAccel = 0f;
            ship.DesiredSpeed = ShipStatConstants.AiCarriedMaxSpeed;
            ship.JumpWindupTimer = (short)(ship.JumpWindupTimer + 1);
        }
    }

    // a76 == 5: face directly away from the target (evade), accelerate when aligned.
    private static void EvadeTarget(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.RunAway && ship.TargetSlot != -1 && isDisabled == 0)
        {
            var target = ShipTable.Ships[ship.TargetSlot];
            ship.HeadingPrev = (short)EvMath.HeadingBetween(target.PosX, target.PosY, ship.PosX, ship.PosY);
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
            }
            if (ship.AiState == ShipAiState.DefendRetreat)
            {
                PickBestWeaponForTarget.Run(ship);
            }
        }
    }

    // a76 == 6: attack run — aim (or lead) at the target, fire-select, and possibly break into a
    // strafing pass (a76 -> 16 / 17).
    private static void AttackRun(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.TurnAndFire && ship.TargetSlot != -1 && isDisabled == 0)
        {
            if (ship.SelectedWeaponSlot == -1)
            {
                var target = ShipTable.Ships[ship.TargetSlot];
                ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            }
            else if ((WeaponGuidanceType)GameData.Weapons[ship.SelectedWeaponSlot].GuidanceType == WeaponGuidanceType.UnguidedProjectile)
            {
                ship.HeadingPrev = (short)LeadTargetAngle.Run(ship, ShipTable.Ships[ship.TargetSlot], ship.SelectedWeaponSlot);
            }
            else
            {
                var target = ShipTable.Ships[ship.TargetSlot];
                ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            }
            PickBestWeaponForTarget.Run(ship);
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
            }
            turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate * 3)
            {
                PickForwardWeaponForTarget.Run(ship);
            }
            if ((ship.TargetSlot != 0 && (short)SeedEvoRng.Run(2) == 0 || PassesCombatRatingRoll.Run()) &&
                ShipAiType.BraveTrader < GameData.ShipClasses[ship.ShipClass].InherentAI && GameData.ShipClasses[ship.ShipClass].Mass < 200)
            {
                uint delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
                if (Abs((int)delta) < 123)
                {
                    delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
                    if (Abs((int)delta) < 123)
                    {
                        var target = ShipTable.Ships[ship.TargetSlot];
                        int angleResult = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
                        turnRate = (short)EvMath.AngleDelta(ship.Heading, (short)angleResult);
                        if (turnRate < 31 &&   // alignment angle window
                            EvMath.AngleDelta(target.Heading, (short)(((short)angleResult + HalfCircle) % FullCircle)) < 31)
                        {
                            ship.AiManeuverState = ShipManeuverState.VeerOff;
                            delta = (uint)ship.SlotIndex;
                            if (delta == (((int)delta >> 1) + (uint)(((int)delta < 0 && (delta & 1) != 0) ? 1 : 0)) * 2)
                            {
                                ship.StrafeHeading = (short)(ship.Heading + 135);
                            }
                            else
                            {
                                ship.StrafeHeading = (short)(ship.Heading + -135);
                            }
                            if (ship.StrafeHeading < 0)
                            {
                                ship.StrafeHeading = (short)(ship.StrafeHeading + FullCircle);
                            }
                            if ((FullCircle - 1) < ship.StrafeHeading)
                            {
                                ship.StrafeHeading = (short)(ship.StrafeHeading + -FullCircle);
                            }
                            if (ship.HeadingPrev < 0)
                            {
                                ship.HeadingPrev = (short)(ship.HeadingPrev + FullCircle);
                            }
                            if ((FullCircle - 1) < ship.HeadingPrev)
                            {
                                ship.HeadingPrev = (short)(ship.HeadingPrev + -FullCircle);
                            }
                        }
                    }
                }
            }
            if (ship.HasAfterburner != 0)
            {
                uint delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
                if (82 < Abs((int)delta))
                {
                    delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
                    if (82 < Abs((int)delta))
                    {
                        var target = ShipTable.Ships[ship.TargetSlot];
                        int angleResult = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
                        turnRate = (short)EvMath.AngleDelta(ship.Heading, (short)angleResult);
                        if (turnRate < 31)
                        {
                            ship.AiManeuverState = ShipManeuverState.Afterburner;
                        }
                    }
                }
            }
        }
    }

    // a76 == 16: strafing pass — hold the offset heading (StrafeHeading) and slow accel until aligned,
    // then revert to the attack run (a76 -> 6).
    private static void StrafingPass(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.VeerOff && ship.TargetSlot != -1 && isDisabled == 0)
        {
            ship.HeadingPrev = ship.StrafeHeading;
            ship.DesiredAccel = (float)(ShipStatConstants.AiStrafeAccelScale * ShipDerivedStats.EffectiveAccel(ship));
            ship.DesiredSpeed = 0f;
            uint delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
            if (Abs((int)delta) < 165)
            {
                delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
                if (Abs((int)delta) < 165)
                {
                    PickBestWeaponForTarget.Run(ship);
                }
            }
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            short angDiff = (short)EvMath.AngleDelta(ship.Heading, ship.StrafeHeading);
            if (angDiff < turnRate * 3)
            {
                ship.AiManeuverState = ShipManeuverState.TurnAndFire;
            }
        }
    }

    // a76 == 17: break-off pass — full accel/speed away while aiming at the target, weapon select,
    // then (usually) revert to the attack run (a76 -> 6).
    private static void BreakoffPass(ShipRec ship, byte isDisabled)
    {
        if (!(ship.AiManeuverState == ShipManeuverState.Afterburner && ship.TargetSlot != -1 && isDisabled == 0))
            return;
        ship.DesiredAccel = (float)(ShipStatConstants.AiBreakoffAccelScale * ShipDerivedStats.EffectiveAccel(ship));
        ship.DesiredSpeed = (float)(ShipStatConstants.AiBreakoffSpeedScale * ShipDerivedStats.EffectiveSpeed(ship));
        var target = ShipTable.Ships[ship.TargetSlot];
        ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);

        uint delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
        bool targetOutOfWeaponRange = !(Abs((int)delta) < 165);
        if (!targetOutOfWeaponRange)
        {
            delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
            if (164 < Abs((int)delta))
            {
                targetOutOfWeaponRange = true;
            }
            else
            {
                PickBestWeaponForTarget.Run(ship);
                short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                if (Abs(ship.HeadingPrev - ship.Heading) < turnRate * 3)
                {
                    PickForwardWeaponForTarget.Run(ship);
                }
            }
        }
        if (targetOutOfWeaponRange)
        {
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate * 3)
            {
                PickHomingWeaponForTarget.Run(ship);
            }
        }

        delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
        bool targetOutOfBreakoffRange = !(Abs((int)delta) < 166);
        if (!targetOutOfBreakoffRange)
        {
            delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
            if (165 < Abs((int)delta))
            {
                targetOutOfBreakoffRange = true;
            }
        }
        if (targetOutOfBreakoffRange)
        {
            short roll = (short)SeedEvoRng.Run(100);
            if (roll != 0) return;
        }
        ship.AiManeuverState = ShipManeuverState.TurnAndFire;
    }

    // a76 == 7 (MissileAttack): stand-off homing/missile attack — aim at the target, accelerate when
    // aligned and fire the homing/secondary weapon; escalate to Afterburner (a76 -> 17) if cornered.
    // was mislabelled `TurretDefense` (an early guessed name); the decompile "Missle" maneuver is a
    // homing-weapon attack, not turret defence — FUN_1000366c 3027-3059. Renamed 2026-07-04.
    private static void MissileAttack(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.MissileAttack && ship.TargetSlot != -1 && isDisabled == 0)
        {
            var target = ShipTable.Ships[ship.TargetSlot];
            ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
                PickHomingWeaponForTarget.Run(ship);
            }
            if (ship.HasAfterburner != 0)
            {
                uint delta = (uint)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
                if (82 < Abs((int)delta))
                {
                    delta = (uint)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
                    if (82 < Abs((int)delta))
                    {
                        int angleResult = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
                        turnRate = (short)EvMath.AngleDelta(ship.Heading, (short)angleResult);
                        if (turnRate < 31)
                        {
                            ship.AiManeuverState = ShipManeuverState.Afterburner;
                        }
                    }
                }
            }
        }
    }

    // a76 == 15 (Board): board/salvage the disabled target — match its position and velocity, and once
    // velocity-matched claim it (SalvageClaimed / the boarded flag) and start the boarding timer.
    // was mislabelled `MatchNavVelocity` (named after the velocity-match step, missing the point);
    // the decompile "Board" maneuver sets the victim's boarded flag — FUN_1000366c 3060-3154.
    // Renamed 2026-07-04.
    private static void Board(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.Board && ship.NavTargetSpob != -1 && isDisabled == 0)
        {
            float diffX = ship.VelX - GameData.Ships[ship.NavTargetSpob].VelX;
            float diffY = ship.VelY - GameData.Ships[ship.NavTargetSpob].VelY;
            double dist = EvMath.FloatAbs(diffX);
            if (ShipStatConstants.AiVelMatchThreshold <= dist ||
                ShipStatConstants.AiVelMatchThreshold <= EvMath.FloatAbs(diffY))
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * diffX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * diffY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                ship.HeadingPrev = (short)((short)(turnRate + HalfCircle) + (short)((turnRate + HalfCircle) / FullCircle) * -FullCircle);
                turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
                {
                    ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                    ship.DesiredSpeed = 0f;
                }
                dist = EvMath.FloatAbs(diffX);
                if (dist <= ShipStatConstants.AiVelSnapThreshold ||
                    EvMath.FloatAbs(diffY) <= ShipStatConstants.AiVelSnapThreshold)
                {
                    diffX = diffX * ShipStatConstants.VelocityDampingB;
                    diffY = diffY * ShipStatConstants.VelocityDampingB;
                    ship.VelX = diffX + GameData.Ships[ship.NavTargetSpob].VelX;
                    ship.VelY = diffY + GameData.Ships[ship.NavTargetSpob].VelY;
                }
            }
            else
            {
                ship.HeadingPrev = GameData.Ships[ship.NavTargetSpob].Heading;
                // The decompile computes the nav-target's sprite frame width here but never uses the
                // result (dead read, preserved faithfully) — only the table lookup has a live effect.
                _ = MacRectWidth.Run(WeaponGraphicsTable.Store[GameData.Ships[ship.NavTargetSpob].ShipClass * 36]);
                // The decompile stores the FULL float |dx|/|dy| and compares AiFormationGap against
                // that — don't floor to int here, the (3,4)-unit gap band sits right at the threshold.
                float magX = (float)EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.NavTargetSpob].PosX);
                float magY = (float)EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.NavTargetSpob].PosY);
                if (ShipStatConstants.AiFormationGap < magX || ShipStatConstants.AiFormationGap < magY)
                {
                    if (ship.AiActionTimer < 1 || 100 < ship.AiActionTimer)
                    {
                        ship.VelX = GameData.Ships[ship.NavTargetSpob].VelX;
                        ship.VelY = GameData.Ships[ship.NavTargetSpob].VelY;
                        float navPosX = GameData.Ships[ship.NavTargetSpob].PosX;
                        float navPosY = GameData.Ships[ship.NavTargetSpob].PosY;
                        int heading = EvMath.HeadingBetween(ship.PosX, ship.PosY, navPosX, navPosY);
                        // OffsetByHeading nudges the ship's own velocity pair toward the nav-target.
                        float vx = ship.VelX, vy = ship.VelY;
                        EvMath.OffsetByHeading((float)(ShipStatConstants.AiNudgeSpeedScale * ShipDerivedStats.EffectiveSpeed(ship)), heading, ref vx, ref vy);
                        ship.VelX = vx; ship.VelY = vy;
                    }
                    else
                    {
                        // EffectiveAccel is the magnitude arg here, not a 3-arg position-only absorber.
                        double accel = ShipDerivedStats.EffectiveAccel(ship);
                        float vx = ship.VelX, vy = ship.VelY;
                        EvMath.OffsetByHeading(accel, ship.Heading, ref vx, ref vy);
                        ship.VelX = vx; ship.VelY = vy;
                    }
                }
                else
                {
                    if (GameData.Ships[ship.NavTargetSpob].SalvageClaimed == 0)
                    {
                        GameData.Ships[ship.NavTargetSpob].SalvageClaimed = 1;
                        short roll = (short)SeedEvoRng.Run(150);
                        ship.AiActionTimer = (short)(roll + 300);
                        ship.VelX = GameData.Ships[ship.NavTargetSpob].VelX;
                        ship.VelY = GameData.Ships[ship.NavTargetSpob].VelY;
                    }
                    else if (ship.AiActionTimer < 1)
                    {
                        ship.TargetSlot = -1;
                        ship.NavTargetSpob = -1;
                        ship.AiState = ShipAiState.Idle;
                        ship.AiManeuverState = ShipManeuverState.None;
                    }
                    if (ship.AiActionTimer < 1 || 100 < ship.AiActionTimer)
                    {
                        ship.VelX = GameData.Ships[ship.NavTargetSpob].VelX;
                        ship.VelY = GameData.Ships[ship.NavTargetSpob].VelY;
                    }
                    else
                    {
                        double accel = ShipDerivedStats.EffectiveAccel(ship);
                        float vx = ship.VelX, vy = ship.VelY;
                        EvMath.OffsetByHeading(accel, ship.Heading, ref vx, ref vy);
                        ship.VelX = vx; ship.VelY = vy;
                    }
                }
            }
        }
    }

    // a76 == 14 (HoldAndFire): near-stop hold-and-fire — bleed velocity to a crawl, keep facing the
    // target and fire everything; a slow/heavy warship (InherentAI >= 3) letting the target come to it.
    // was mislabelled `Dogfight` (that is really a76 == 6 AttackRun / "Turn+Fire"); the decompile
    // "WaitTarg" maneuver is a stationary hold-and-fire — FUN_1000366c 3155-3185. Renamed 2026-07-04.
    private static void HoldAndFire(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.HoldAndFire && ship.TargetSlot != -1 && isDisabled == 0)
        {
            double dist = EvMath.FloatAbs(ship.VelX);
            if (ShipStatConstants.AiVelSettleThreshold <= dist ||
                ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelY))
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                ship.HeadingPrev = (short)((short)(turnRate + HalfCircle) + (short)((turnRate + HalfCircle) / FullCircle) * -FullCircle);
                turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
                {
                    ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                }
            }
            else
            {
                ship.VelX = ship.VelX * ShipStatConstants.VelocityDampingB;
                ship.VelY = ship.VelY * ShipStatConstants.VelocityDampingB;
                var target = ShipTable.Ships[ship.TargetSlot];
                ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            }
            PickBestWeaponForTarget.Run(ship);
            AutoFireSpecialAtTarget.Run(ship);
            PickHomingWeaponForTarget.Run(ship);
        }
    }

    // a76 == 11 (ChaseSlow): speed-limited careful approach — aim at target/nav, lead with velocity,
    // accelerate; far targets get direct aim, near ones a velocity-corrected heading, and it throttles
    // speed down as it nears so it does not overshoot (vs Chase's full-speed pursuit).
    // was mislabelled `Intercept` (the decompile "ChasSlw" maneuver) — FUN_1000366c 3186-3251.
    // Renamed 2026-07-04.
    private static void ChaseSlow(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.ChaseSlow && (ship.TargetSlot != -1 || ship.NavTargetSpob != -1) && isDisabled == 0)
        {
            // targetSlot = the decompile's unaff_r28 (untracked register); always written
            // before read on every live path, so default(short) = 0 is safe.
            short targetSlot = default;
            if (ship.TargetSlot < 0)
            {
                if (-1 < ship.NavTargetSpob)
                {
                    targetSlot = ship.NavTargetSpob;
                }
            }
            else
            {
                targetSlot = ship.TargetSlot;
            }
            var target = ShipTable.Ships[targetSlot];
            int angleResult = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            double dist = EvMath.FloatAbs(ship.PosX - target.PosX);
            if (ShipStatConstants.AiFarAimDistance < dist ||
                ShipStatConstants.AiFarAimDistance < EvMath.FloatAbs(ship.PosY - target.PosY))
            {
                ship.HeadingPrev = (short)angleResult;
            }
            else
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                if (15 < Abs(turnRate - (short)angleResult))
                {
                    float targetVx = 0f, targetVy = 0f;
                    EvMath.OffsetByHeading(ShipDerivedStats.EffectiveSpeed(ship), (short)angleResult, ref targetVx, ref targetVy);
                    float diffX = targetVx - ship.VelX;
                    float diffY = targetVy - ship.VelY;
                    dist = EvMath.FloatAbs(diffX);
                    if (ShipStatConstants.AiVelSettleThreshold < dist ||
                        ShipStatConstants.AiVelSettleThreshold < EvMath.FloatAbs(diffY))
                    {
                        ship.HeadingPrev = (short)EvMath.HeadingBetween(0f, 0f, diffX, diffY);
                    }
                }
            }
            short maneuver = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < maneuver + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                dist = EvMath.FloatAbs(ship.PosX - target.PosX);
                if (ShipStatConstants.AiScanApproachDistance < dist ||
                    ShipStatConstants.AiScanApproachDistance < EvMath.FloatAbs(ship.PosY - target.PosY))
                {
                    ship.DesiredSpeed = 0f;
                }
                else
                {
                    ship.DesiredSpeed = (float)(ShipStatConstants.AiSettleAccelScale * ShipDerivedStats.EffectiveSpeed(ship));
                }
            }
            ShipAi.UpdateFollowParent(ship);
        }
    }

    // a76 == 9 (Chase): full-speed pursuit — same lead-pursuit geometry as ChaseSlow (a76 == 11) but
    // holds full accel/speed all the way in (DesiredSpeed 0 = uncapped) rather than throttling near.
    // was mislabelled `InterceptVariant` (the decompile "Chase" maneuver) — FUN_1000366c 3252-3306.
    // Renamed 2026-07-04.
    private static void Chase(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.Chase && (ship.TargetSlot != -1 || ship.NavTargetSpob != -1) && isDisabled == 0)
        {
            // targetSlot = the decompile's unaff_r28 (untracked register); always written
            // before read on every live path, so default(short) = 0 is safe.
            short targetSlot = default;
            if (ship.TargetSlot < 0)
            {
                if (-1 < ship.NavTargetSpob)
                {
                    targetSlot = ship.NavTargetSpob;
                }
            }
            else
            {
                targetSlot = ship.TargetSlot;
            }
            var target = ShipTable.Ships[targetSlot];
            int angleResult = EvMath.HeadingBetween(ship.PosX, ship.PosY, target.PosX, target.PosY);
            double dist = EvMath.FloatAbs(ship.PosX - target.PosX);
            if (ShipStatConstants.AiFarAimDistance < dist ||
                ShipStatConstants.AiFarAimDistance < EvMath.FloatAbs(ship.PosY - target.PosY))
            {
                ship.HeadingPrev = (short)angleResult;
            }
            else
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                if (15 < Abs(turnRate - (short)angleResult))
                {
                    float targetVx = 0f, targetVy = 0f;
                    EvMath.OffsetByHeading(ShipDerivedStats.EffectiveSpeed(ship), (short)angleResult, ref targetVx, ref targetVy);
                    float diffX = targetVx - ship.VelX;
                    float diffY = targetVy - ship.VelY;
                    dist = EvMath.FloatAbs(diffX);
                    if (ShipStatConstants.AiVelSettleThreshold < dist ||
                        ShipStatConstants.AiVelSettleThreshold < EvMath.FloatAbs(diffY))
                    {
                        ship.HeadingPrev = (short)EvMath.HeadingBetween(0f, 0f, diffX, diffY);
                    }
                }
            }
            short maneuver = (short)ShipDerivedStats.EffectiveManeuver(ship);
            if (Abs(ship.HeadingPrev - ship.Heading) < maneuver + 1)
            {
                ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                ship.DesiredSpeed = 0f;
            }
            ShipAi.UpdateFollowParent(ship);
        }
    }

    // a76 == 12 (FormationFly): fly a fixed offset relative to the leader and match its velocity when
    // close (wrapped reverse heading). The close-formation state around a parent/the player.
    // was mislabelled `MatchNavVelocityVariant`; the decompile "FormFly" maneuver is formation
    // flight, distinct from the Board approach — FUN_1000366c 3307-3350. Renamed 2026-07-04.
    private static void FormationFly(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.FormationFly && ship.NavTargetSpob != -1 && isDisabled == 0)
        {
            var navTarget = ShipTable.Ships[ship.NavTargetSpob];
            float diffX = ship.VelX - navTarget.VelX;
            float diffY = ship.VelY - navTarget.VelY;
            double dist = EvMath.FloatAbs(diffX);
            if (ShipStatConstants.AiVelMatchThreshold <= dist ||
                ShipStatConstants.AiVelMatchThreshold <= EvMath.FloatAbs(diffY))
            {
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * diffX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * diffY);
                short turnRate = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
                ship.HeadingPrev = (short)((turnRate + HalfCircle) % FullCircle);
                turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
                {
                    ship.DesiredAccel = (float)(ShipStatConstants.AiVelMatchAccelScale * ShipDerivedStats.EffectiveAccel(ship));
                    ship.DesiredSpeed = 0f;
                }
                dist = EvMath.FloatAbs(diffX);
                if (dist <= ShipStatConstants.AiVelSnapThreshold ||
                    EvMath.FloatAbs(diffY) <= ShipStatConstants.AiVelSnapThreshold)
                {
                    diffX = diffX * ShipStatConstants.VelocityDampingB;
                    diffY = diffY * ShipStatConstants.VelocityDampingB;
                    ship.VelX = diffX + navTarget.VelX;
                    ship.VelY = diffY + navTarget.VelY;
                }
            }
            else
            {
                ship.VelX = navTarget.VelX;
                ship.VelY = navTarget.VelY;
                float velX = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelX);
                float velY = (float)(ShipStatConstants.AiVelHeadingScale * ship.VelY);
                ship.HeadingPrev = (short)EvMath.HeadingBetween(0f, 0f, velX, velY);
            }
            ShipAi.UpdateFollowParent(ship);
        }
    }

    // a76 == 8 (DockInCarrier): dock/land back into the parent carrier — aim in, and once inside the
    // docking box store this ship into the carrier (increment its carried-ship ammo, despawn self).
    // was mislabelled `BoardNavTarget` (that reads like boarding a victim — really a76 == 15 Board);
    // the decompile "Dock" maneuver is carrier retrieval — FUN_1000366c 3351-3435. Renamed 2026-07-04.
    private static void DockInCarrier(ShipRec ship, byte isDisabled)
    {
        if (ship.AiManeuverState == ShipManeuverState.DockInCarrier)
        {
            if (ship.NavTargetSpob == -1 || ShipDerivedStats.IsDyingOrDestroyed(ship))
            {
                ship.AiState = ShipAiState.Idle;
                ship.AiManeuverState = ShipManeuverState.None;
            }
            else if (GameData.Ships[ship.NavTargetSpob].IsActive == 0 ||
                     ship.CurrentSystem != GameData.Ships[ship.NavTargetSpob].CurrentSystem)
            {
                ship.AiBehaviorType = ShipAiType.Warship;
                ship.AiState = ShipAiState.Idle;
            }
            else
            {
                var navTarget = ShipTable.Ships[ship.NavTargetSpob];
                if (isDisabled == 0)
                {
                    ship.HeadingPrev = (short)EvMath.HeadingBetween(ship.PosX, ship.PosY, navTarget.PosX, navTarget.PosY);
                    short turnRate = (short)ShipDerivedStats.EffectiveManeuver(ship);
                    if (Abs(ship.HeadingPrev - ship.Heading) < turnRate + 1)
                    {
                        ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
                        ship.DesiredSpeed = 0f;
                    }
                }
                // Docking box = the nav-target sprite's frame width for its current heading.
                short dockingBoxWidth = (short)MacRectWidth.Run(WeaponGraphicsTable.Store[navTarget.ShipClass * 36 + navTarget.Heading / 10]);
                double dist2 = EvMath.FloatAbs(navTarget.PosX - ship.PosX);
                if (dist2 <= (float)dockingBoxWidth)
                {
                    double dist = EvMath.FloatAbs(navTarget.PosY - ship.PosY);
                    if (dist <= (float)dockingBoxWidth)
                    {
                        bool canSpawn = ship.SlotIndex == 0
                            ? CanSpawnAnotherSubMunition.Run(ship.ShipClass, -1, -1)
                            : true;
                        if (canSpawn)
                        {
                            for (short slot = 0; slot < ShipRecord.WeaponSlotCount; slot++)
                            {
                                // 128 + class = the carried-ship-class marker in the ammo/link field
                                // (WeaponGuidanceType.CarriedShip).
                                if (ship.ShipClass + 128 == GameData.Weapons[slot].AmmoLink &&
                                    (WeaponGuidanceType)GameData.Weapons[slot].GuidanceType == WeaponGuidanceType.CarriedShip)
                                {
                                    GameData.Ships[ship.NavTargetSpob].WeaponSlotAmmo[slot] = (short)(GameData.Ships[ship.NavTargetSpob].WeaponSlotAmmo[slot] + 1);
                                    if (ship.OwnerSlot == 0)
                                    {
                                        WorldState.HudWeaponPanelDirty = 1;
                                    }
                                    ship.IsActive = 0;
                                    ship.OwnerSlot = -1;
                                    ship.AiBehaviorType = ShipAiType.Inactive;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ship.AiState = ShipAiState.GuardPlayer;
                            ship.AiManeuverState = ShipManeuverState.None;
                            ship.TargetSlot = -1;
                            ship.NavTargetSpob = -1;
                        }
                    }
                }
            }
        }
    }

    // a76 == 10: clamp the cached max speed/accel to their idle constants.
    private static void ClampToIdle(ShipRec ship)
    {
        if (ship.AiManeuverState == ShipManeuverState.HyperArriveZoom)
        {
            if (0.0 <= ship.DesiredSpeed)
            {
                ship.DesiredSpeed = ShipStatConstants.AiIdleMaxSpeed;
            }
            ship.DesiredAccel = ShipStatConstants.AiIdleAccel;
        }
    }
}
