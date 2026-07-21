using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Ship;

// Per-ship AI: state predicates, state-mutating actions, target engagement, and the
// per-frame dispatcher. Companion to ShipDerivedStats (derived stats / status), which
// these call into for IsDisabled / Effective* etc. Each method corresponds to
// a single decompiled function.
public static class ShipAi
{
    // ── AI-state predicates: ship.AiState holds the current AI behaviour state ──
    public static bool IsStateLeaving(ShipRec ship) => ship.AiState == ShipAiState.ReturnToParent;          // FUN_10009644 (5057-5067)
    public static bool IsStateHyperWindup(ShipRec ship) => ship.AiState == ShipAiState.EscortParent;     // FUN_10009660 (5068-5078)
    public static bool IsStateCombat(ShipRec ship) => ship.AiState == ShipAiState.AttackShip;           // FUN_1000967c (5079-5089)
    public static bool IsStateLandingApproach(ShipRec ship) => ship.AiState == ShipAiState.Wait;  // FUN_10009698 (5090-5102)
    public static bool IsStateInert(ShipRec ship) => ship.AiState == ShipAiState.HyperOut;            // FUN_10009af0 (5216-5228)
    public static bool IsStateGuardingParent(ShipRec ship) => ship.AiState == ShipAiState.Plunder; // FUN_10009db0 (5297-5309)

    // FUN_100095f8 — EV Override-11.c lines 5044-5056.
    public static bool IsWindupAtSubstep(ShipRec ship)
    {
        if ((ship.AiState == ShipAiState.HyperOut || ship.AiState == ShipAiState.HyperWithParent || ship.AiState == ShipAiState.DefendRetreat) &&
            (ship.AiManeuverState == ShipManeuverState.HyperJump || ship.AiManeuverState == ShipManeuverState.JumpWithParent))
            return true;
        return false;
    }

    // FUN_10009ab0 — EV Override-11.c lines 5204-5215.
    public static bool IsStateActiveCombatPhase(ShipRec ship)
    {
        if (ship.AiState != ShipAiState.Idle && ship.AiState != ShipAiState.HyperOut && ship.AiState != ShipAiState.GoToStellar && ship.AiState != ShipAiState.Inspect)
            return true;
        return false;
    }

    // FUN_1000778c — EV Override-11.c lines 4242-4261.
    public static bool IsEngageableTarget(ShipRec ship)
    {
        if (ship.IsActive != 0 && ship.AiActionTimer < 1 && !ShipDerivedStats.IsDisabled(ship) &&
            ship.TargetSlot == 0 && ship.AiState != ShipAiState.Inspect && ship.AiState != ShipAiState.Refuel &&
            ship.AiState != ShipAiState.EscortParent && ship.AiState != ShipAiState.HyperWithParent && ship.AiState != ShipAiState.ReturnToParent && ship.AiState != ShipAiState.GuardPlayer)
            return true;
        return false;
    }

    // FUN_1006b314 — EV Override-11.c lines 44116-44139.
    // True when the ship's current shield/armor (ship.Shield, +0x68: signed,
    // + = shield / − = armor damage) has fallen at or below its retreat threshold
    // (ship.IncomingDamageThreat, +0xa7a). Depleted armor (value < 0) is compared with a steeper
    // scale than an intact shield. (The decompile's signed int→double magic idiom is an
    // exact (double) cast.)
    public static bool ArmorBelowRetreatThreshold(ShipRec ship)
    {
        int value = (int)ship.Shield;
        double valueD = (double)value;
        double thresholdD = (double)ship.IncomingDamageThreat;
        double scale = value < 0
            ? ShipStatConstants.RetreatScaleArmorDamaged
            : ShipStatConstants.RetreatScaleShieldUp;
        return scale * valueD <= thresholdD;
    }

    // FUN_10007d64 — EV Override-11.c lines 4361-4388.
    public static bool HasGovtAlliesAlive(ShipRec ship)
    {
        if (ship.IsActive == 0 || ShipDerivedStats.IsDisabled(ship))
            return false;
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            ShipRec s = ShipTable.Ships[i];
            if (s.IsActive != 0 && ship.TargetSlot == ship.SlotIndex && i != ship.SlotIndex &&
                s.AiState != ShipAiState.Inspect && s.AiState != ShipAiState.Refuel && s.AiState != ShipAiState.EscortParent && s.AiState != ShipAiState.HyperWithParent &&
                s.AiState != ShipAiState.ReturnToParent && s.AiState != ShipAiState.GuardPlayer)
                return true;
        }
        return false;
    }

    // FUN_10007840 — EV Override-11.c lines 4262-4326. True if this ally/carrier ship
    // is actively engaged in combat. It must be active (IsActive), not disabled, an
    // escort (OwnerSlot != 0) with a target; then engaged if it has a damage marker
    // (DefendedSpobIndex), OR it is locked onto a valid target in an engageable AI state,
    // OR any ship escorting IT is so locked. (The decompile returns int 1/0; callers
    // consume it as a 0/1 flag.)
    public static bool HasEngagedAllyOrCarrier(ShipRec ship)
    {
        if (ship.IsActive == 0 || ShipDerivedStats.IsDisabled(ship) || ship.OwnerSlot == 0 || ship.TargetSlot == -1)
            return false;

        if (ship.DefendedSpobIndex != -1)
            return true;

        // The AI-state gate and damage cooldown are this ship's, re-tested in every
        // branch below (including the escort loop — see StateAllowsEngagement).
        bool engageable = StateAllowsEngagement(ship);
        bool noCooldown = ship.AiActionTimer < 1;

        if (ship.TargetSlot == 0 && noCooldown && engageable)
            return true;
        if (GameData.Ships[ship.TargetSlot].OwnerSlot == 0 && noCooldown && engageable)
            return true;
        if (ship.TargetSlot != -1 && noCooldown && engageable && GameData.Ships[ship.TargetSlot].OwnerSlot == 0)
            return true;

        // Or any ship escorting THIS ship (its OwnerSlot == this slot) is engaged.
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            var escort = ShipTable.Ships[i];
            if (ship.SlotIndex != escort.OwnerSlot || escort.AiActionTimer >= 1 ||
                escort.IsActive == 0 || ShipDerivedStats.IsDisabled(escort))
                continue;
            if (escort.TargetSlot == 0 && engageable)
                return true;
            if (escort.TargetSlot != -1 && engageable && GameData.Ships[escort.TargetSlot].OwnerSlot == 0)
                return true;
        }
        return false;
    }

    // The AI states that DON'T block engagement detection — FUN_10007840's repeated
    // `AiState != 5/7/9/10/11/12` chain. The decompile tests the ORIGINAL ship's state
    // even inside the escort loop, so this always takes the param-1 ship.
    private static bool StateAllowsEngagement(ShipRec ship)
    {
        var s = ship.AiState;
        return s != ShipAiState.ReturnToParent && s != ShipAiState.Inspect && s != ShipAiState.Refuel &&
               s != ShipAiState.EscortParent && s != ShipAiState.HyperWithParent && s != ShipAiState.GuardPlayer;
    }

    // ── AI-state actions (mutators) ─────────
    // FUN_100004a8 — EV Override-11.c lines 1299-1310.
    public static void ResetAiToIdle(ShipRec ship)
    {
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
        ship.DockedSpobIndex = -2;
        ship.LastVictimSlot = -1;
    }

    // FUN_10009e74 — EV Override-11.c lines 5325-5341. Cancel a pending hyperjump-out:
    // only when the ship is mid jump-out (Refuel), clear its target/spob and return
    // it to idle.
    public static void ClearHyperjumpReturnToIdle(ShipRec ship)
    {
        if (ship.AiState != ShipAiState.Refuel)
            return;
        ship.NavTargetSpob = -1;
        ship.TargetSlot = -1;
        ship.AiActionTimer = 0;
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
    }

    // FUN_10008c14 — EV Override-11.c lines 4699-4713.
    public static void SetStateInert(ShipRec ship)
    {
        ship.AiState = ShipAiState.HyperOut;
        ship.JumpWindupTimer = 1;
        ship.TargetSlot = -1;
        ship.AiTickStamp = (int)MacToolbox.TickCount();
    }

    // FUN_10008c60 — EV Override-11.c lines 4714-4733.
    public static void SetStateLeavingHyper(ShipRec ship)
    {
        ship.AiState = ShipAiState.HyperWithParent;
        ship.ProvokedFlag = 0;
        ship.TargetSlot = -1;
        if (ship.JumpWindupTimer < 1)
        {
            ship.JumpWindupTimer = 1;
            ship.AiTickStamp = (int)MacToolbox.TickCount();
        }
    }

    // FUN_10008d8c — EV Override-11.c lines 4757-4766.
    public static void SetStateRetaliateAgainstGovt(ShipRec ship, ShipRec target)
    {
        ship.AiState = ShipAiState.AttackShip;
        ship.TargetSlot = target.SlotIndex;
    }

    // FUN_10008da0 — EV Override-11.c lines 4767-4779.
    public static void SetStateLanding(ShipRec ship)
    {
        ship.AiState = ShipAiState.Wait;
        ship.TargetSlot = -1;
        ship.NavTargetSpob = -1;
    }

    // FUN_1000943c — EV Override-11.c lines 4960-4973.
    public static void SetStateJumpingOut(ShipRec ship)
    {
        ship.ProvokedFlag = 0;
        ship.JumpWindupTimer = 0;
        ship.TargetSlot = 0;
        ship.AiState = ShipAiState.Refuel;
        ship.AiManeuverState = ShipManeuverState.None;
        ship.AiActionTimer = -1;
    }

    // FUN_10009470 — EV Override-11.c lines 4974-4986.
    public static void SetStateLeavingFollowSelf(ShipRec ship)
    {
        ship.TargetSlot = -1;
        ship.NavTargetSpob = ship.OwnerSlot;
        ship.AiState = ShipAiState.ReturnToParent;
    }

    // FUN_100095e4 — EV Override-11.c lines 5034-5043.
    public static void SetStateWindDown(ShipRec ship)
    {
        ship.AiState = ShipAiState.HyperIn;
        ship.JumpWindupTimer = -999;   // 0xfc19
    }

    // FUN_1000948c — EV Override-11.c lines 4987-5012. Put the ship into hyperspace
    // wind-up (escorts → EscortParent; a lone non-escort → GuardPlayer) and propagate the
    // leave order to every follow-master escort of this ship.
    public static void SetStateHyperWindupAndPropagate(ShipRec s)
    {
        s.TargetSlot = -1;
        s.NavTargetSpob = s.OwnerSlot;
        if (s.AiBehaviorType == ShipAiType.NavalFighter)
            s.AiState = ShipAiState.EscortParent;
        else if (s.OwnerSlot == 0)
            s.AiState = ShipAiState.GuardPlayer;
        else
            s.AiState = ShipAiState.EscortParent;
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            var escort = ShipTable.Ships[i];
            if (escort.IsActive != 0 && i != s.SlotIndex && s.SlotIndex == escort.OwnerSlot && escort.AiBehaviorType == ShipAiType.NavalFighter)
                SetStateLeavingFollowSelf(escort);
        }
    }

    // FUN_10001bf4 — EV Override-11.c lines 1929-1967. Switch the ship into combat
    // against the player: scan for the nearest live, non-escort ship within engage
    // range (kept for parity though the result only gates the scan), clear nav, set
    // AiState=4, and default/repoint TargetSlot to the player (slot 0).
    public static void EngagePlayer(ShipRec ship)
    {
        uint nearest = 0xffffffff;
        for (short i = 0; i < ShipTable.Count; i = (short)(i + 1))
        {
            if (GameData.Ships[i].IsActive != 0 && (i == 0 || GameData.Ships[i].OwnerSlot == 0))
            {
                // The decompile's bare FloatAbs() reuses the f1 result of the preceding
                // DistanceSquared call; chaining them keeps the distance from collapsing to 0.
                double distSq = EvMath.FloatAbs(EvMath.DistanceSquared(ship, ShipTable.Ships[i]));
                if (distSq <= (double)ShipStatConstants.MaxEngageRange)
                {
                    // (float)(int)nearest is the decompile's signed int->double idiom (lines 1944-1945).
                    if (distSq < (double)(float)(int)nearest || nearest == 0xffffffff)
                        nearest = (uint)distSq;
                }
            }
        }
        ship.AiActionTimer = 0;
        ship.NavTargetSpob = -1;
        ship.AiState = ShipAiState.AttackShip;
        if (ship.TargetSlot == -1)
            ship.TargetSlot = 0;
        else if (ship.TargetSlot != 0 && GameData.Ships[ship.TargetSlot].OwnerSlot != 0)
            ship.TargetSlot = 0;
    }

    // FUN_10009d2c — EV Override-11.c lines 5280-5296. For a follow-master escort
    // (OwnerSlot == 0, AI type 6) that has a target: drop the target if it has gone
    // inactive, otherwise pick the best weapon for it.
    public static void UpdateFollowParent(ShipRec self)
    {
        if (self.OwnerSlot != 0 || self.AiBehaviorType != ShipAiType.Escort || self.TargetSlot == -1)
            return;
        if (GameData.Ships[self.TargetSlot].IsActive == 0)
            self.TargetSlot = -1;
        else
            Combat.PickBestWeaponForTarget.Run(self);
    }

    // FUN_100004cc — EV Override-11.c lines 1311-1350. Per-frame AI tick for the basic
    // wandering behaviour (AI type 1). Bails for disabled/jumping-out ships; if idle
    // (Idle) it picks (or fails to find) a destination; a recent attacker turns it
    // to flee — running for the player's cargo (EscortParent) or fleeing outright
    // (DefendRetreat, which also builds the cargo-pod hail string).
    public static void TickAi(ShipRec ship)
    {
        if (ShipDerivedStats.IsDisabled(ship) || ship.AiState == ShipAiState.Refuel)
            return;

        if (ship.AiState == ShipAiState.Idle)
        {
            if (ShipDerivedStats.IsDestinationAllowedBySyst(ship, ship.CurrentSystem))
            {
                SetStateInert(ship);
            }
            else
            {
                ship.NavTargetSpob = -1;
                PickRandomDestination(ship);
                if (ship.NavTargetSpob == -1)
                    SetStateInert(ship);
            }
        }

        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1)
        {
            if (ship.OwnerSlot == 0)
            {
                ship.AiState = ShipAiState.EscortParent;
                ship.NavTargetSpob = 0;
            }
            else
            {
                ship.AiState = ShipAiState.DefendRetreat;
            }
        }

        if (ship.AiState == ShipAiState.DefendRetreat)
            BuildCargoPodNameString(ship);
    }

    // FUN_100005cc — EV Override-11.c lines 1351-1411. Per-frame AI tick for the
    // "attacker" behaviour (AI type 2). Bails for disabled/jumping-out ships; if idle
    // (Idle) it picks (or fails to find) a destination; then, if it has a recent
    // target in range, it commits to attack (AttackShip), runs for cargo (EscortParent), or
    // pursues (DefendRetreat); finally it builds a cargo-pod hail string when fleeing or when
    // attacking a worthwhile target.
    public static void TickAttackerAi(ShipRec ship)
    {
        const int EngageProximityRange = 1251; // |Δx| and |Δy| world-units to commit to attack

        if (ShipDerivedStats.IsDisabled(ship) || ship.AiState == ShipAiState.Refuel)
            return;

        if (ship.AiState == ShipAiState.Idle)
        {
            if (ShipDerivedStats.IsDestinationAllowedBySyst(ship, (short)(int)ship.CurrentSystem))
            {
                SetStateInert(ship);
            }
            else
            {
                ship.NavTargetSpob = -1;
                PickRandomDestination(ship);
                if (ship.NavTargetSpob == -1)
                    SetStateInert(ship);
            }
        }

        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1)
        {
            ShipRec target = ShipTable.Ships[ship.TargetSlot];
            if (Abs16(ship.PosX - target.PosX) < EngageProximityRange &&
                Abs16(ship.PosY - target.PosY) < EngageProximityRange)
            {
                if (ship.JumpWindupTimer < 1)
                    ship.AiState = ShipAiState.AttackShip;
            }
            else if (ship.OwnerSlot == 0)
            {
                ship.AiState = ShipAiState.EscortParent;
                ship.NavTargetSpob = 0;
            }
            else
            {
                ship.AiState = ShipAiState.DefendRetreat;
            }
        }

        if (ship.AiState == ShipAiState.DefendRetreat)
        {
            BuildCargoPodNameString(ship);
        }
        else if (ship.AiState == ShipAiState.AttackShip && ship.TargetSlot != -1 && ship.TargetSlot != 0 &&
                 GameData.Ships[ship.TargetSlot].AiBehaviorType > ShipAiType.BraveTrader)
        {
            BuildCargoPodNameString(ship);
        }
    }

    // abs() of a float position delta truncated to 16 bits, via the decompile's
    // (x ^ sign) - sign two's-complement trick. Preserved verbatim (not System.Math.Abs)
    // for the INT16_MIN edge case and bug-for-bug parity.
    private static int Abs16(float delta)
    {
        ushort v = (ushort)(int)delta;
        ushort sign = (ushort)((short)v >> 0xf);
        return (short)((sign ^ v) - sign);
    }

    // FUN_100007d4 — EV Override-11.c lines 1412-1600. Per-frame AI tick for the
    // "defender" behaviour (AI type 3): pers dock/scan gating (as TickInterceptorAi),
    // landing-target/destination selection, fleet regrouping by counting its live
    // escorts (Wait), and the surrender/flee decision (DefendRetreat) driven by the
    // current shield against scaled fractions of EffectiveShieldMax.
    public static void TickDefenderAi(ShipRec ship)
    {
        if (ShipDerivedStats.IsDisabled(ship) || ship.AiState == ShipAiState.Refuel)
            return;

        // Govt-flag gating (GovtTable.Flags): a dockable pers (0x04 AlwaysAttacksPlayer)
        // calls for defenders and engages the player; a scannable one (0x40
        // NeverAttacksPlayer) drops its target.
        if (ship.Govt != -1 && ship.JumpWindupTimer > -900 && ship.JumpWindupTimer < 1)
        {
            GovtFlags govtFlags = GameData.Governments[ship.Govt].Flags;
            if ((govtFlags & GovtFlags.AlwaysAttacksPlayer) == 0)
            {
                if ((govtFlags & GovtFlags.NeverAttacksPlayer) != 0 && IsEngageableTarget(ship))
                {
                    ship.TargetSlot = -1;
                    ship.AiState = ShipAiState.Idle;
                }
            }
            else if (!IsEngageableTarget(ship))
            {
                CallForDefendersAndEngagePlayer(ship);
                ship.TargetSlot = 0;
                ship.ProvokedFlag = 1;
            }
        }

        if (ship.AiState == ShipAiState.Idle)
        {
            if (ship.TargetSlot == -1)
            {
                // The `!= DefendRetreat` re-test is vestigial (state is 0 here) — faithful.
                if (ship.JumpWindupTimer < 1 && ship.AiState != ShipAiState.DefendRetreat)
                {
                    PickNpcLandingTarget(ship);
                    if (ship.TargetSlot == -1)
                    {
                        if (ShipDerivedStats.IsDestinationAllowedBySyst(ship, ship.CurrentSystem))
                        {
                            SetStateInert(ship);
                        }
                        else
                        {
                            ship.NavTargetSpob = -1;
                            PickRandomDestination(ship);
                            if (ship.NavTargetSpob == -1)
                                SetStateInert(ship);
                        }
                    }
                    else
                    {
                        ship.AiState = ShipAiState.AttackShip;
                        MaybeStartGuardingParent(ship);
                    }
                }
            }
            else if (ship.JumpWindupTimer < 1 && ship.AiState != ShipAiState.Inspect)
            {
                ship.AiState = ShipAiState.AttackShip;
                MaybeStartGuardingParent(ship);
            }
        }

        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1 && ship.JumpWindupTimer < 1 &&
            ship.AiState != ShipAiState.Inspect && ship.AiState != ShipAiState.DefendRetreat)
        {
            ship.AiState = ShipAiState.AttackShip;
            MaybeStartGuardingParent(ship);
        }

        if (ship.AiState == ShipAiState.GoToStellar || ship.AiState == ShipAiState.HyperOut)
        {
            if (ship.TargetSlot == -1)
            {
                if (ship.AiState != ShipAiState.DefendRetreat)   // vestigial (state is 1/2 here) — faithful
                {
                    PickNpcLandingTarget(ship);
                    if (ship.TargetSlot == -1)
                    {
                        if (CountLiveEscortsOf(ship, requireEscortAi: true) > 0)
                            ship.AiState = ShipAiState.Wait;
                    }
                    else if (ship.JumpWindupTimer < 1)
                    {
                        ship.AiState = ShipAiState.AttackShip;
                        MaybeStartGuardingParent(ship);
                    }
                }
            }
            else if (ship.JumpWindupTimer < 1 && ship.AiState != ShipAiState.Inspect)
            {
                ship.AiState = ShipAiState.AttackShip;
                MaybeStartGuardingParent(ship);
            }
        }

        if (ship.AiState == ShipAiState.Wait)
        {
            ship.TargetSlot = -1;
            PickNpcLandingTarget(ship);
            if (ship.TargetSlot == -1 && CountLiveEscortsOf(ship, requireEscortAi: false) == 0)
                ship.AiState = ShipAiState.Idle;
        }

        // ship.Shield (+0x68) holds an int-valued float; (int)ship.Shield reproduces the
        // decompile's int read of the cell. The flee-fraction scales are managed literals
        // (ShipStatConstants.Defender*Fraction).
        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1 && ship.JumpWindupTimer < 1)
        {
            if (ship.Govt != -1)
            {
                if ((double)(int)ship.Shield <=
                    ShipStatConstants.DefenderFleeShieldFraction * EffectiveShieldMaxD(ship) &&
                    (GameData.Governments[ship.Govt].Flags & GovtFlags.PersNoEscapePod) != 0)
                {
                    ship.AiState = ShipAiState.DefendRetreat;
                }
            }
            if (ship.AiState == ShipAiState.HyperOut)
            {
                ship.AiState = ShipAiState.AttackShip;
                MaybeStartGuardingParent(ship);
            }
            else if (ship.AiState != ShipAiState.Inspect && ship.AiState != ShipAiState.DefendRetreat &&
                     ship.AiState != ShipAiState.HyperWithParent)
            {
                ship.AiState = ShipAiState.AttackShip;
                MaybeStartGuardingParent(ship);
            }
        }

        if (ship.TargetSlot != -1 && ship.AiState != ShipAiState.Inspect)
        {
            if (ship.JumpWindupTimer < 1)
            {
                ship.AiState = ShipAiState.AttackShip;
                MaybeStartGuardingParent(ship);
            }

            // Shield level below which the ship may surrender/flee (DefendRetreat).
            int fleeShieldThreshold = -0x7fff;
            if (ship.PersIndex == -1)
            {
                if (ship.AiCourage == 1)
                    fleeShieldThreshold = (int)(ShipStatConstants.DefenderShieldFractionType1 * EffectiveShieldMaxD(ship));
                if (ship.AiCourage == 2)
                    fleeShieldThreshold = (int)(ShipStatConstants.DefenderShieldFractionType2 * EffectiveShieldMaxD(ship));
                if (ship.AiCourage == 4)
                    fleeShieldThreshold = -0x7fff;
            }
            else
            {
                fleeShieldThreshold = (int)(ShipStatConstants.DefenderMissionShieldFraction
                                            * (double)PersTable.Coward(ship.PersIndex) * EffectiveShieldMaxD(ship));
            }

            if ((int)ship.Shield < fleeShieldThreshold && ship.OwnerSlot == -1)
            {
                if (ship.Govt != -1 && (GameData.Governments[ship.Govt].Flags & GovtFlags.RetreatAt25PctShield) != 0 &&
                    ship.AiState != ShipAiState.HyperOut && ship.AiState != ShipAiState.DefendRetreat &&
                    ship.AiState != ShipAiState.HyperWithParent)
                {
                    ship.AiState = ShipAiState.DefendRetreat;
                }
            }
        }
    }

    // TickDefenderAi's escort census: live (IsActive), undamaged-marker (DefendedSpobIndex
    // == -1), non-disabled ships escorting THIS ship (their OwnerSlot == this slot) —
    // optionally only those flying the escort AI (AiBehaviorType == 5).
    private static short CountLiveEscortsOf(ShipRec ship, bool requireEscortAi)
    {
        short count = 0;
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            var escort = ShipTable.Ships[i];
            if (ship.SlotIndex == escort.OwnerSlot && (!requireEscortAi || escort.AiBehaviorType == ShipAiType.NavalFighter) &&
                escort.IsActive != 0 && escort.DefendedSpobIndex == -1 && !ShipDerivedStats.IsDisabled(escort))
            {
                count = (short)(count + 1);
            }
        }
        return count;
    }

    // EffectiveShieldMax widened to double (exact for int), as the defender
    // shield-threshold formulas consume it.
    private static double EffectiveShieldMaxD(ShipRec ship)
        => (double)(int)ShipDerivedStats.EffectiveShieldMax(ship);

    // FUN_10001578 — EV Override-11.c lines 1759-1820. Per-frame AI tick for the
    // "escort" behaviour (AI type 5): a disabled escort detaches (lands or limps back
    // to its master); a target that isn't the master triggers combat (and, when the
    // master is itself escorting the player, a fresh random target pick); with no
    // target it follows/regroups on its master, inheriting a defender master's target;
    // and it mirrors the master's jump-out cooldown (HyperWithParent).
    public static void TickEscortAi(ShipRec ship)
    {
        if (ShipDerivedStats.IsDisabled(ship))
        {
            if (ship.OwnerSlot == -1 || ship.DefendedSpobIndex != -1)
            {
                ship.AiState = ShipAiState.Wait;
            }
            else
            {
                ship.AiState = ShipAiState.ReturnToParent;
                ship.AiManeuverState = ShipManeuverState.None;
                ship.NavTargetSpob = ship.OwnerSlot;
            }
            return;
        }

        if (ship.TargetSlot > -1 && ship.TargetSlot != ship.OwnerSlot)
        {
            // Decompile comma-op: AttackShip is set as a side-effect of evaluating the
            // condition chain whenever the two tests above pass, even when the master
            // checks below fail.
            ship.AiState = ShipAiState.AttackShip;
            if (ship.OwnerSlot != -1 && GameData.Ships[ship.OwnerSlot].OwnerSlot == 0)
            {
                ship.TargetSlot = -1;
                ship.AiState = ShipAiState.Idle;
                ship.AiManeuverState = ShipManeuverState.None;
                PickRandomCombatTarget(ship);
                if (ship.TargetSlot == -1)
                    PickRandomCombatTargetForGovtShip(ship);
                if (ship.TargetSlot == -1)
                {
                    ship.TargetSlot = ship.OwnerSlot;
                    ship.AiState = ShipAiState.ReturnToParent;
                }
            }
        }

        if (ship.TargetSlot == -1 && ship.AiState != ShipAiState.Wait && ship.AiState != ShipAiState.ReturnToParent &&
            ship.OwnerSlot != -1 && ship.DefendedSpobIndex == -1)
        {
            var master = ShipTable.Ships[ship.OwnerSlot];
            if (master.TargetSlot == -1 && ship.OwnerSlot != 0)
            {
                ship.AiState = ShipAiState.ReturnToParent;
                ship.NavTargetSpob = ship.OwnerSlot;
            }
            else if (ship.OwnerSlot == 0)
            {
                ship.AiState = ShipAiState.EscortParent;
                ship.NavTargetSpob = ship.OwnerSlot;
            }
            else if (master.AiBehaviorType == ShipAiType.Warship && ship.SlotIndex != master.TargetSlot)
            {
                ship.AiState = ShipAiState.AttackShip;
                ship.TargetSlot = master.TargetSlot;
            }
        }

        // Deviation from the decompile: it reads Ships[OwnerSlot].JumpWindupTimer even
        // when OwnerSlot == -1 (heap garbage just below the ship table); the typed handle
        // would throw, so this skips the read for a detached escort.
        if (ship.OwnerSlot != -1 && GameData.Ships[ship.OwnerSlot].JumpWindupTimer > 0)
            ship.AiState = ShipAiState.HyperWithParent;
    }

    // FUN_100017bc — EV Override-11.c lines 1821-1928. Per-frame AI tick for the
    // "follow-master" behaviour (AI type 6, fighters/wingmen): with no master it
    // reverts to its class AI; a dead/disabled master detaches it; while the master
    // winds up to jump it joins any engaged ally's fight; otherwise it picks fights
    // per its class AI's aggression, commits when the target is close (pursue/attack),
    // and falls back to forming up on the master (EscortParent).
    public static void TickFollowMasterAi(ShipRec ship)
    {
        const int FollowProximityRange = 251; // |Δx|,|Δy| world-units to commit to the target

        if (ship.OwnerSlot == -1)
        {
            RevertToClassAi(ship);
            return;
        }

        var master = ShipTable.Ships[ship.OwnerSlot];
        if (master.IsActive == 0 || ShipDerivedStats.IsDisabled(master))
        {
            ship.OwnerSlot = -1;
            RevertToClassAi(ship);
            return;
        }

        if (ship.OwnerSlot != 0 && IsWindupAtSubstep(master))
        {
            // Master is winding up to jump: pile onto the first engaged ally/carrier.
            for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
            {
                var ally = ShipTable.Ships[i];
                if (HasEngagedAllyOrCarrier(ally) && ally.OwnerSlot != 0)
                {
                    SetStateRetaliateAgainstGovt(ship, ally);
                    return;
                }
            }
            return;
        }

        // The ship-class inherent AI type steers the aggression below (constant for
        // the whole tick; the decompile re-reads it each time).
        ShipAiType inherentAi = GameData.ShipClasses[ship.ShipClass].InherentAI;

        if ((ship.TargetSlot == -1 || ship.AiState == ShipAiState.Idle || ship.AiState == ShipAiState.EscortParent) &&
            (ship.AiState == ShipAiState.EscortParent || ship.AiState == ShipAiState.GuardPlayer) &&
            inherentAi > ShipAiType.BraveTrader && ship.AiActionTimer < 1)
        {
            if (ship.TargetSlot == -1 || ship.TargetSlot == ship.OwnerSlot)
                ship.ProvokedFlag = 0;
            if (ship.OwnerSlot == 0)
                PickRandomCombatTarget(ship);
            else
                PickRandomCombatTargetForGovtShip(ship);
        }

        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1 && ship.TargetSlot != ship.OwnerSlot)
        {
            if (inherentAi < ShipAiType.Warship)
            {
                var target = ShipTable.Ships[ship.TargetSlot];
                if (Abs16(ship.PosX - target.PosX) < FollowProximityRange &&
                    Abs16(ship.PosY - target.PosY) < FollowProximityRange)
                {
                    if (inherentAi == ShipAiType.WimpyTrader && ship.AiState != ShipAiState.HyperOut &&
                        ship.AiState != ShipAiState.DefendRetreat && ship.AiState != ShipAiState.HyperWithParent)
                    {
                        ship.AiState = ShipAiState.DefendRetreat;
                    }
                    if (inherentAi == ShipAiType.BraveTrader)
                        ship.AiState = ShipAiState.AttackShip;
                }
                else
                {
                    ship.ProvokedFlag = 0;
                    ship.AiState = ShipAiState.EscortParent;
                    ship.NavTargetSpob = ship.OwnerSlot;
                    ship.TargetSlot = -1;
                }
            }
            else
            {
                ship.AiState = ShipAiState.AttackShip;
                if (ship.OwnerSlot != -1 && master.OwnerSlot == 0)
                {
                    ship.TargetSlot = -1;
                    ship.AiState = ShipAiState.Idle;
                    ship.AiManeuverState = ShipManeuverState.None;
                }
            }
        }

        if (ship.TargetSlot == -1 && ship.AiState != ShipAiState.Wait && ship.AiState != ShipAiState.HyperWithParent)
        {
            ship.AiState = ShipAiState.EscortParent;
            ship.NavTargetSpob = ship.OwnerSlot;
        }

        if (ship.AiState == ShipAiState.EscortParent && ship.OwnerSlot == 0)
        {
            PickRandomCombatTarget(ship);
            ship.AiState = ShipAiState.EscortParent;
            ship.NavTargetSpob = 0;
        }
    }

    // TickFollowMasterAi's detach path: revert to the ship-class inherent AI and idle.
    private static void RevertToClassAi(ShipRec ship)
    {
        ship.AiBehaviorType = GameData.ShipClasses[ship.ShipClass].InherentAI;
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
    }

    // ── Target/destination acquisition + hail actions the AI ticks call ─────────

    // FUN_10005838 — EV Override-11.c lines 3449-3515. Pick a random landable spob
    // among the system's 4 stellar links (excluding flag-0x20 spobs and spobs whose
    // government is linked to this ship's pers record) and head for it (GoToStellar).
    public static void PickRandomDestination(ShipRec ship)
    {
        bool[] persMatched = new bool[4];   // spob govt linked to the ship's pers — excluded
        bool[] landable = new bool[4];   // spob flag bit 0
        bool[] excluded = new bool[4];   // spob flag bit 5
        short shipSys = ship.CurrentSystem;
        short shipPers = ship.Govt;

        short eligibleCount = 0;
        for (short slot = 0; slot < 4; slot = (short)(slot + 1))
        {
            short spob = SystTable.SpobLink(shipSys, slot);
            if (spob == -1)
                continue;
            landable[slot] = (SpobTable.Flags(spob) & 1) != 0;
            excluded[slot] = (SpobTable.Flags(spob) & 0x20) != 0;
            short spobGovt = SpobTable.Govt(spob);
            if (shipPers != -1 && spobGovt != -1 &&
                (GameData.Governments[shipPers].Enemy == spobGovt || shipPers == GameData.Governments[spobGovt].Enemy))
            {
                persMatched[slot] = true;
            }
            if (landable[slot] && !excluded[slot] && !persMatched[slot])
                eligibleCount = (short)(eligibleCount + 1);
        }
        if (eligibleCount < 1)
            return;

        short pick;
        do
        {
            pick = (short)Misc.SeedEvoRng.Run(4);
        } while (excluded[pick] || !landable[pick] || persMatched[pick]);
        ship.NavMode = 2;
        ship.AiState = ShipAiState.GoToStellar;
        ship.NavTargetSpob = SystTable.SpobLink(shipSys, pick);
    }

    // FUN_10008dbc — EV Override-11.c lines 4780-4829. Census the engageable NPC
    // ships in-system, then roll until one is hit and attack it (AttackShip); with no
    // candidates the target is cleared.
    public static void PickRandomCombatTarget(ShipRec ship)
    {
        short candidateCount = 0;
        for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
        {
            if (IsRandomCombatCandidate(ship, i))
                candidateCount = (short)(candidateCount + 1);
        }
        if (candidateCount < 1)
        {
            ship.TargetSlot = -1;
            return;
        }
        short pick;
        do
        {
            pick = (short)(Misc.SeedEvoRng.Run(35) + 1);
        } while (!IsRandomCombatCandidate(ship, pick));
        ship.NavTargetSpob = -1;
        ship.TargetSlot = pick;
        ship.AiState = ShipAiState.AttackShip;
    }

    // FUN_10008dbc's candidate gate, in decompile test order (the slot != 0 test is
    // vestigial — the census starts at 1 and the retry roll is rng(35)+1, both ≥ 1).
    private static bool IsRandomCombatCandidate(ShipRec ship, short slot)
    {
        if (slot == ship.SlotIndex || slot == 0)
            return false;
        var s = ShipTable.Ships[slot];
        if (ShipDerivedStats.IsDisabled(s) || ShipDerivedStats.IsDyingOrDestroyed(s))
            return false;
        return ship.CurrentSystem == s.CurrentSystem && IsEngageableTarget(s);
    }

    // FUN_10008f80 — EV Override-11.c lines 4830-4909. Like PickRandomCombatTarget,
    // but for a government escort: a candidate counts only if it is engaged in this
    // ship's master's fight (see IsEngagedForGovtPick).
    public static void PickRandomCombatTargetForGovtShip(ShipRec ship)
    {
        short candidateCount = 0;
        for (short i = 0; i < ShipTable.Count; i = (short)(i + 1))
        {
            if (i == ship.SlotIndex || i == ship.OwnerSlot)
                continue;
            var s = ShipTable.Ships[i];
            if (ShipDerivedStats.IsDisabled(s) || ship.CurrentSystem != s.CurrentSystem)
                continue;
            if (IsEngagedForGovtPick(ship, i))
                candidateCount = (short)(candidateCount + 1);
        }
        if (candidateCount < 1)
        {
            ship.TargetSlot = -1;
            return;
        }
        short pick;
        while (true)
        {
            pick = (short)Misc.SeedEvoRng.Run(ShipTable.Count);
            if (pick == ship.SlotIndex || pick == ship.OwnerSlot ||
                ShipDerivedStats.IsDisabled(ShipTable.Ships[pick]) ||
                ship.CurrentSystem != GameData.Ships[pick].CurrentSystem)
                continue;
            if (IsEngagedForGovtPick(ship, pick))
                break;
        }
        ship.NavTargetSpob = -1;
        ship.TargetSlot = pick;
        ship.AiState = ShipAiState.AttackShip;
    }

    // FUN_10008f80's engagement test: the player (slot 0) counts when this ship's
    // MASTER is engaged; another ship counts as itself-engaged when the master is the
    // player, else when it is engaging this ship's master.
    private static bool IsEngagedForGovtPick(ShipRec ship, short slot)
    {
        if (slot == 0)
            return HasEngagedAllyOrCarrier(ShipTable.Ships[ship.OwnerSlot]);
        if (ship.OwnerSlot == 0)
            return HasEngagedAllyOrCarrier(ShipTable.Ships[slot]);
        return Misc.IsCandidateEngagingObserver.Run(ShipTable.Ships[ship.OwnerSlot], ShipTable.Ships[slot]);
    }

    // FUN_10009dcc — EV Override-11.c lines 5310-5324. A pers ship with the
    // guard-parent flag (0x1000 WarshipsPlunder) whose target is a peaceful trader-class NPC starts
    // guarding it (Plunder) instead of fighting.
    public static void MaybeStartGuardingParent(ShipRec ship)
    {
        // TargetSlot is tested (< 1) before the handle is built below; the decompile
        // short-circuits, and a -1 slot would throw on the first field read.
        if (ship.TargetSlot < 1 || ship.Govt == -1 ||
            (GameData.Governments[ship.Govt].Flags & GovtFlags.WarshipsPlunder) == 0)
            return;
        var target = ShipTable.Ships[ship.TargetSlot];
        if (target.AiBehaviorType < ShipAiType.Warship && target.GrudgeMissionIndex == -1 && target.SalvageClaimed == 0)
            ship.AiState = ShipAiState.Plunder;
    }

    // FUN_10008cc0 — EV Override-11.c lines 4734-4756. Turn on the player (AttackShip,
    // target slot 0); a ship whose pers has the hail flag (pers +0x14 bit 0x10) and
    // no govt grudge first speaks its pers hail line.
    public static void CallForDefendersAndEngagePlayer(ShipRec ship)
    {
        if (!HasEngagedAllyOrCarrier(ship) && ship.PersIndex != -1)
        {
            // Bit 0x10 of PersTable.Flags: here bit-set GATES speaking the hail. The same bit
            // is named PersFlags.SuppressHail from UpdateShipAiFrame's hail-suppression gate
            // (opposite sense) — left a raw mask until the two readings are reconciled.
            if (ship.GrudgeMissionIndex == -1 && (PersTable.Flags(ship.PersIndex) & 0x10) != 0)
            {
                // Name the hailing ship for the comm code, then clear it after the hail.
                WorldState.CurrentTargetShipId = ship.SlotIndex;
                WorldState.FlashChatterCountdown = -1;
                Sound.SpeakPersHailLine.Run(PersTable.HailQuote(ship.PersIndex));
                WorldState.CurrentTargetShipId = -1;
            }
        }
        ship.AiState = ShipAiState.AttackShip;
        ship.NavTargetSpob = -1;
        ship.TargetSlot = 0;
    }

    // FUN_10009b0c — EV Override-11.c lines 5229-5274. When a fleeing NPC the player
    // is engaging jettisons cargo, chime and enqueue the "<Class>: <random pod name>"
    // chatter line.
    public static void BuildCargoPodNameString(ShipRec ship)
    {
        // The chatter separator (data-seg 0x10081ac4 = GameToc−0x6b9c, dumped from
        // the PEF data section to a C# literal).
        const string NameSeparator = ": ";

        if (ship.PersIndex != -1 || WorldState.FlashChatterCountdown >= 1)
            return;
        if (!Combat.IsPlayerEngagementTarget.Run(ship))
            return;
        if (Misc.IsCandidateEngagingObserver.Run(ship.Ptr, ShipTable.Base) ||
            ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[0]))
            return;

        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
        // "<name>: <pod name>" — managed strings.
        string name = GameData.ShipClasses[ship.ShipClass].Name;
        if (ship.GrudgeMissionIndex == -1)
        {
            // PersIndex == −1 is guaranteed by the early-out above, so the decompile's
            // mission-name branch (PersIndex != −1 → mission +0x1a0) is DEAD; the class
            // name is just copied again — faithful.
            name = GameData.ShipClasses[ship.ShipClass].Name;
        }
        else if (GameData.Missions[ship.GrudgeMissionIndex].Name.Length > 0)
        {
            name = GameData.Missions[ship.GrudgeMissionIndex].Name;
        }
        short nameIndex = (short)Misc.SeedEvoRng.Run(20);
        // STR# 0x138b pod-name table (managed; filled by InitResourceNameStrings
        // at boot step 26 — pre-boot reads see "").
        string podName = Resource.ResourceGlobals.NamesStr138b[nameIndex];
        Misc.EnqueueChatterEvent.Run(name + NameSeparator + podName, 240, 0, 12,
            Graphics.Model.UiColors.ChatterText, 0, 0);
    }

    // FUN_10006ae4 — EV Override-11.c lines 3971-4238. Target acquisition for a
    // cooled-down NPC: keeps an existing live target; the engage-player pers (0x1fe)
    // and accepted hostile mission ships engage outright; a govt with an active grudge
    // engages or jumps out per its grudge mode; otherwise the ship's pers relation
    // decides — a non-hostile pers ship may turn on the player over legal status when
    // in hailing range, a hostile-scan pers ship (flag 0x01) picks a random enemy
    // (the player counts only on bad legal standing), falling back to the first live
    // pers enemy; finally a follow-master ship picks a random engageable target.
    public static void PickNpcLandingTarget(ShipRec ship)
    {
        if (ship.TargetSlot != -1 && GameData.Ships[ship.TargetSlot].IsActive != 0)
            return;

        if (ship.PersIndex != -1)
        {
            if (ship.PersIndex == ShipRecord.EngagePlayerPersIndex)
            {
                CallForDefendersAndEngagePlayer(ship);
                return;
            }
            if ((PersTable.Flags(ship.PersIndex) & 1) != 0 &&
                PersTable.AcceptedFlag(ship.PersIndex) != 0)
            {
                CallForDefendersAndEngagePlayer(ship);
                return;
            }
        }

        if (ship.GrudgeMissionIndex != -1 && GameData.MissionStates[ship.GrudgeMissionIndex].IsActive != 0)
        {
            short grudgeMode = GameData.Missions[ship.GrudgeMissionIndex].ShipBehavior;
            if ((grudgeMode == 0 || grudgeMode == 10) && !WorldState.IsCloaked)
            {
                CallForDefendersAndEngagePlayer(ship);
                return;
            }
            if (grudgeMode == 1 || grudgeMode == 11)
            {
                if (ship.TargetSlot == 0)
                    ship.TargetSlot = -1;
                if (ship.TargetSlot != -1)
                    return;
                PickRandomCombatTarget(ship);
                if (ship.TargetSlot != -1)
                    return;
                ship.AiState = ShipAiState.GuardPlayer;
                ship.NavTargetSpob = 0;
                return;
            }
        }

        if (ship.Govt != -1)
        {
            GovtFlags govtFlags = GameData.Governments[ship.Govt].Flags;
            if ((govtFlags & GovtFlags.Xenophobic) == 0)
            {
                if (ship.TargetSlot == -1 && ShouldTurnOnPlayer(ship, govtFlags))
                {
                    CallForDefendersAndEngagePlayer(ship);
                    return;
                }
            }
            else if (HostileScanAcquire(ship))
            {
                return;
            }
            // Fallback: lock onto the first live pers enemy.
            for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
            {
                var s = ShipTable.Ships[i];
                if (i != ship.SlotIndex && s.IsActive != 0 && !ShipDerivedStats.IsDisabled(s) &&
                    Misc.ArePersEnemies.Run(ship.Ptr, s.Ptr))
                {
                    ship.TargetSlot = i;
                    return;
                }
            }
        }

        if (ship.AiBehaviorType == ShipAiType.Escort)
        {
            short candidateCount = 0;
            for (short i = 1; i < ShipTable.Count; i = (short)(i + 1))
            {
                if (IsFollowMasterTargetCandidate(ship, i))
                    candidateCount = (short)(candidateCount + 1);
            }
            if (candidateCount > 0)
            {
                short pick;
                do
                {
                    do
                    {
                        pick = (short)Misc.SeedEvoRng.Run(ShipTable.Count);
                    } while (GameData.Ships[pick].IsActive == 0);
                } while (pick == ship.SlotIndex || pick == GameData.Ships[ship.OwnerSlot].SlotIndex ||
                         !IsEngageableTarget(ShipTable.Ships[pick]) ||
                         ship.CurrentSystem != GameData.Ships[pick].CurrentSystem);
                ship.TargetSlot = pick;
                ship.AiState = ShipAiState.AttackShip;
            }
        }
    }

    // FUN_10006ae4's player-aggro decision for a NON-hostile-scan pers ship (true =
    // call the defenders and engage). Within hailing range of the player
    // (|Δ| ≤ aggressiveness × 600, comms up, player not docked, not a scannable pers),
    // the player's per-system legal status is judged against the pers thresholds —
    // which comparison applies depends on how the system's government relates to this
    // pers record. Falls back to a 1-in-50 hail roll for an idle govt-allied ship.
    private static bool ShouldTurnOnPlayer(ShipRec ship, GovtFlags govtFlags)
    {
        var player = ShipTable.Ships[0];
        float engageRange = (float)(short)(ship.AiCourage * 600);
        double dx = (double)EvMath.FloatAbs((double)(ship.PosX - player.PosX));
        double dy = (double)EvMath.FloatAbs((double)(ship.PosY - player.PosY));
        bool inRange = dx <= (double)engageRange && dy <= (double)engageRange &&
                       !WorldState.IsCloaked && !ShipDerivedStats.IsDyingOrDestroyed(player);

        if (inRange && (GameData.Governments[ship.Govt].Flags & GovtFlags.NeverAttacksPlayer) == 0)
        {
            short playerSyst = player.CurrentSystem;
            short systGovt = SystTable.Store[playerSyst].Govt;
            short legalStatus = GalaxyMapGlobals.SystemStatus(playerSyst);
            short persThreshold = GameData.Governments[ship.Govt].CrimeTolerance;
            if (ship.Govt == systGovt)
            {
                if (legalStatus < -persThreshold)
                    return true;
            }
            else if (systGovt < 0)
            {
                if ((govtFlags & GovtFlags.LawEnforcementEverywhere) != 0 && legalStatus < persThreshold * -2)
                    return true;
            }
            else if (GameData.Governments[ship.Govt].Enemy == systGovt ||
                     ship.Govt == GameData.Governments[systGovt].Enemy ||
                     (GameData.Governments[systGovt].Flags & GovtFlags.Xenophobic) != 0)
            {
                if (persThreshold < legalStatus)
                    return true;
            }
            else if (GameData.Governments[ship.Govt].Ally == systGovt ||
                     ship.Govt == GameData.Governments[systGovt].Ally)
            {
                if ((double)legalStatus <
                    ShipStatConstants.AiStrafeAccelScale * (double)(-(int)persThreshold))
                    return true;
            }
            else if ((govtFlags & GovtFlags.LawEnforcementEverywhere) != 0 && legalStatus < persThreshold * -2)
            {
                return true;
            }
        }

        // 1-in-50 hail roll: an un-grudged, un-flagged escort ship of the player's
        // ship-class government (or its ally) hails a docked-out player.
        if (ship.TargetSlot == -1 && ship.OwnerSlot != 0 && ship.Govt != -1 &&
            ship.GrudgeMissionIndex == -1 && ship.SpawningMissionSlot == -1)
        {
            short playerGovt = GameData.ShipClasses[GameData.Ships[0].ShipClass].InherentGovt;
            if (playerGovt != -1 &&
                (GameData.Governments[ship.Govt].Enemy == playerGovt ||
                 ship.Govt == GameData.Governments[playerGovt].Enemy) &&
                (short)Misc.SeedEvoRng.Run(50) == 0 &&
                !ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[0]))
            {
                return true;
            }
        }
        return false;
    }

    // FUN_10006ae4's hostile-scan (pers flag 0x01) acquisition: census the eligible
    // enemies, then roll until one is taken. Returns true when a target was taken
    // (the player case also calls the defenders); false when the census was empty.
    private static bool HostileScanAcquire(ShipRec ship)
    {
        short candidateCount = 0;
        for (short i = 0; i < ShipTable.Count; i = (short)(i + 1))
        {
            var s = ShipTable.Ships[i];
            if (ShipDerivedStats.IsDyingOrDestroyed(s) || s.ShipClass == ShipRecord.EmptyShipClass)
                continue;
            if (i == 0)
            {
                if (CanPlayerBeScanTarget(ship))
                    candidateCount = (short)(candidateCount + 1);
            }
            else if (IsNpcScanEnemyCandidate(ship, i) && Misc.ArePersEnemies.Run(ship.Ptr, s.Ptr))
            {
                candidateCount = (short)(candidateCount + 1);
            }
        }
        if (candidateCount < 1)
            return false;

        while (ship.TargetSlot == -1)
        {
            short pick = (short)Misc.SeedEvoRng.Run(ShipTable.Count);
            if (!IsNpcScanEnemyCandidate(ship, pick))
                continue;
            bool engage;
            if (ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[pick]))
                engage = false;
            else if (pick == 0)
                engage = CanPlayerBeScanTarget(ship);
            else
                engage = Misc.ArePersEnemies.Run(ship.Ptr, ShipTable.Ships[pick].Ptr);
            if (!engage)
                continue;
            ship.TargetSlot = pick;
            if (pick == 0)
                CallForDefendersAndEngagePlayer(ship);
            return true;
        }
        return true;
    }

    // The player is scan-targetable when comms are up, this pers isn't scannable
    // (flag 0x40 NeverAttacksPlayer), and — in this pers's own government's system — the player's legal
    // status is bad (< 1). Used by both the census and the retry roll.
    private static bool CanPlayerBeScanTarget(ShipRec ship)
    {
        if (WorldState.IsCloaked || (GameData.Governments[ship.Govt].Flags & GovtFlags.NeverAttacksPlayer) != 0)
            return false;
        short playerSyst = GameData.Ships[0].CurrentSystem;
        if (ship.Govt == SystTable.Store[playerSyst].Govt)
            return GalaxyMapGlobals.SystemStatus(playerSyst) < 1;
        return true;
    }

    // FUN_10006ae4's hostile-scan NPC gate: a live, non-disabled ship in this system
    // that isn't us, isn't escorting us, has no damage marker, and is neither the
    // kamikaze pers (0x1ff) nor the no-AI class (0x3f).
    private static bool IsNpcScanEnemyCandidate(ShipRec ship, short slot)
    {
        var s = ShipTable.Ships[slot];
        return slot != ship.SlotIndex && s.IsActive != 0 && !ShipDerivedStats.IsDisabled(s) &&
               ship.SlotIndex != s.OwnerSlot && ship.CurrentSystem == s.CurrentSystem &&
               s.DefendedSpobIndex == -1 && s.PersIndex != ShipRecord.KamikazePersIndex && s.ShipClass != ShipRecord.EmptyShipClass;
    }

    // FUN_10006ae4's follow-master target gate: live, engageable, in-system, and
    // neither us nor our master.
    private static bool IsFollowMasterTargetCandidate(ShipRec ship, short slot)
    {
        var s = ShipTable.Ships[slot];
        return s.IsActive != 0 && slot != ship.SlotIndex &&
               slot != GameData.Ships[ship.OwnerSlot].SlotIndex &&
               IsEngageableTarget(s) && ship.CurrentSystem == s.CurrentSystem;
    }

    // FUN_10000f1c — EV Override-11.c lines 1601-1758. Per-frame AI tick for the
    // "interceptor" behaviour (AI type 4): handles the kamikaze pers case
    // (PersIndex 0x1ff), pers-flag dock/scan gating, target validation and random
    // civilian target acquisition, the government "prepare to be scanned" hail, and
    // landing on the system's default spob when no target is found.
    public static void TickInterceptorAi(ShipRec ship)
    {
        if (ShipDerivedStats.IsDisabled(ship))
            return;

        if (ship.PersIndex == ShipRecord.KamikazePersIndex)
        {
            // Kamikaze pers: once cooled down, go inert (unless already in combat) or,
            // if it has a target-of-record, lock onto the player.
            if (ship.JumpWindupTimer < 1 && ship.AiActionTimer < 1)
            {
                if (ship.LastVictimSlot == 0)
                {
                    if (ship.AiState != ShipAiState.AttackShip)
                        SetStateInert(ship);
                }
                else
                {
                    ship.AiState = ShipAiState.Inspect;
                    ship.TargetSlot = 0;
                }
            }
            return;
        }

        if (ship.AiState == ShipAiState.Refuel)
            return;

        // Govt-flag gating (GovtTable.Flags): when this ship belongs to a pers and is
        // cooled down, a scannable pers (0x40 NeverAttacksPlayer) drops its target & idles,
        // while a dockable pers (0x04 AlwaysAttacksPlayer) calls for defenders and engages the player.
        if (ship.Govt != -1 && ship.JumpWindupTimer > -900 && ship.JumpWindupTimer < 1)
        {
            GovtFlags govtFlags = GameData.Governments[ship.Govt].Flags;
            if ((govtFlags & GovtFlags.AlwaysAttacksPlayer) == 0)
            {
                if ((govtFlags & GovtFlags.NeverAttacksPlayer) != 0 && IsEngageableTarget(ship))
                {
                    ship.TargetSlot = -1;
                    ship.AiState = ShipAiState.Idle;
                }
            }
            else if (!IsEngageableTarget(ship))
            {
                CallForDefendersAndEngagePlayer(ship);
                ship.TargetSlot = 0;
                ship.ProvokedFlag = 1;
            }
        }

        if (ship.ProvokedFlag > 0 && ship.TargetSlot != -1 &&
            ship.AiState != ShipAiState.Inspect && ship.AiState != ShipAiState.Refuel && ship.JumpWindupTimer < 1)
        {
            ship.AiState = ShipAiState.AttackShip;
        }

        // Drop the target-of-record (LastVictimSlot) if its ship slot went inactive.
        for (short slot = 0; slot < ShipTable.Count; slot++)
        {
            if (GameData.Ships[slot].IsActive == 0 && slot == ship.LastVictimSlot)
                ship.LastVictimSlot = -1;
        }

        // The rest only runs while idle/cruising and cooled down.
        if ((ship.AiState != ShipAiState.Idle && ship.AiState != ShipAiState.GoToStellar) || ship.AiActionTimer >= 1)
            return;

        if (ship.TargetSlot == -1)
        {
            PickNpcLandingTarget(ship);
            if (ship.TargetSlot != -1 && ship.JumpWindupTimer < 1)
                ship.AiState = ShipAiState.AttackShip;
        }
        else if (ship.JumpWindupTimer < 1 && GameData.Ships[ship.TargetSlot].IsActive != 0 && ship.ProvokedFlag > 0)
        {
            ship.AiState = ShipAiState.AttackShip;
        }

        if (ship.TargetSlot == -1)
        {
            // Count eligible civilian targets in-system (active, not self, not the
            // target-of-record, not another interceptor, comms not jammed for the player).
            short candidateCount = 0;
            for (short slot = 0; slot < ShipTable.Count; slot++)
            {
                if (IsCivilianTargetEligible(ship, slot))
                    candidateCount++;
            }
            if (candidateCount > 0)
            {
                // Pick one at random; retry until a roll lands on an eligible slot.
                while (ship.TargetSlot == -1)
                {
                    short pick = (short)Misc.SeedEvoRng.Run(ShipTable.Count);
                    if (!IsCivilianTargetEligible(ship, pick))
                        continue;

                    ship.TargetSlot = pick;
                    ship.LastVictimSlot = pick;
                    ship.AiState = ShipAiState.Inspect;
                    if (pick == 0)
                        MaybeSendScanHail(ship);
                }
            }
        }

        if (ship.TargetSlot == -1 && ship.AiState == ShipAiState.Idle)
        {
            ship.DockedSpobIndex = -2;
            short defaultSpob = SystTable.SpobLink(ship.CurrentSystem, 0);
            if (defaultSpob == -1)
            {
                ship.AiState = ShipAiState.Wait;
            }
            else
            {
                ship.NavTargetSpob = defaultSpob;
                ship.AiState = ShipAiState.GoToStellar;
            }
        }
    }

    // FUN_10000f1c's repeated civilian-target gate: slot is active, not this ship, not
    // its target-of-record, not itself an interceptor (AiBehaviorType != 4), in the same
    // system, and — for the player (slot 0) — comms are not jammed.
    private static bool IsCivilianTargetEligible(ShipRec ship, short slot)
    {
        var s = ShipTable.Ships[slot];
        return slot != ship.SlotIndex && s.IsActive != 0 && slot != ship.LastVictimSlot &&
               s.AiBehaviorType != ShipAiType.Interceptor && (slot != 0 || !WorldState.IsCloaked) &&
               ship.CurrentSystem == s.CurrentSystem;
    }

    // When an interceptor locks onto the player, a government whose scan pers (govt
    // +0x18) matches this ship's Govt (+0x5c) sends a "prepare to be scanned" hail.
    private static void MaybeSendScanHail(ShipRec ship)
    {
        short foundGovt = -1;
        for (short g = 0; g < 8; g++)
        {
            if (GameData.MissionStates[g].IsActive == 0 || GameData.Missions[g].CargoPickedUp == 0 ||
                GameData.Missions[g].ScanPersIndex == -1)
                continue;
            if (ship.Govt == GameData.Missions[g].ScanPersIndex)
            {
                foundGovt = g;
                break;
            }
        }
        if (foundGovt == -1)
            return;

        // "<ClassName>: <ShipName><variant>" — the ": " connective and the three variant
        // suffixes are managed C# literals (dumped from the data seg; decompile 1712-1727).
        string msg = GameData.ShipClasses[ship.ShipClass].Name + ": "
                   + Pilot.Model.PilotIdentity.ShipName;
        short msgVariant = (short)Misc.SeedEvoRng.Run(3);
        if (msgVariant == 0) msg += ", prepare to be scanned.";
        if (msgVariant == 1) msg += ", prepare for a cargo inspection.";
        if (msgVariant == 2) msg += ", hold position for a cargo inspection.";

        Sound.TriggerSoundPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[4], 1, 128);
        Misc.EnqueueChatterEvent.Run(msg, 400, 0, 12, Graphics.Model.UiColors.ChatterText, 0, 0);
    }

    // FUN_1000001c — EV Override-11.c lines 1186-1248. Per-ship AI dispatcher (one ship
    // per frame, via Misc.Tick).
    public static void DispatchAi(ShipRec ship)
    {
        // Class 0x3f is the special no-AI object: just refresh its physics caches and
        // tick its lifespan (AiActionTimer) down, deactivating it when it expires.
        if (ship.ShipClass == ShipRecord.EmptyShipClass)
        {
            ship.DesiredAccel = (float)ShipDerivedStats.EffectiveAccel(ship);
            ship.DesiredSpeed = (float)ShipDerivedStats.EffectiveSpeed(ship);
            ship.HeadingPrev = ship.Heading;
            ship.AiActionTimer = (short)(ship.AiActionTimer - 1);
            if (ship.AiActionTimer < 0)
                ship.IsActive = 0;
            return;
        }

        // A pending mission grudge (GrudgeMissionIndex) the mission has since cleared is dropped; if
        // the ship was an escort (OwnerSlot != -1) it reverts to its class AI and goes
        // inert. (Decompile comma op: the grudge is cleared whenever the first two tests
        // pass, the body only runs if it was also an escort.)
        if (ship.GrudgeMissionIndex != -1 && GameData.MissionStates[ship.GrudgeMissionIndex].IsActive == 0)
        {
            ship.GrudgeMissionIndex = -1;
            if (ship.OwnerSlot != -1)
            {
                ship.AiBehaviorType = GameData.ShipClasses[ship.ShipClass].InherentAI;
                ship.OwnerSlot = -1;
                SetStateInert(ship);
            }
        }

        if (ship.JumpWindupTimer < -900)
        {
            ship.AiState = ShipAiState.HyperIn;
        }
        else if (ship.AiManeuverState != ShipManeuverState.HyperJump && ship.AiManeuverState != ShipManeuverState.JumpWithParent)
        {
            if (ship.DefendedSpobIndex == -1)
            {
                // AiBehaviorType selects the AI behaviour routine (the ship-class InherentAI).
                switch (ship.AiBehaviorType)
                {
                    case ShipAiType.WimpyTrader: TickAi(ship); break;
                    case ShipAiType.BraveTrader: TickAttackerAi(ship); break;
                    case ShipAiType.Warship: TickDefenderAi(ship); break;
                    case ShipAiType.Interceptor: TickInterceptorAi(ship); break;
                    case ShipAiType.NavalFighter: TickEscortAi(ship); break;
                    case ShipAiType.Escort: TickFollowMasterAi(ship); break;
                }
            }
            else
            {
                EngagePlayer(ship);
            }
        }
        Combat.UpdateShipAiObjective.Run(ship);
        Combat.UpdateShipAiSteering.Run(ship);
    }
}
