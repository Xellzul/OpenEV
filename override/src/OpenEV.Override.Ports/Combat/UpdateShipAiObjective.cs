using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Combat;

// Port of FUN_10001d64 (EV Override-11.c lines 1968-2661): the NPC per-frame AI
// OBJECTIVE step. Reads AND transitions ship.AiState (the high-level behaviour —
// e.g. leaving the system, fleeing, engaging, docking, refuelling) as escort
// chains break, targets die, encounters resolve, and government aggression
// rolls fire, then — for whichever AiState ends up active this frame — picks
// the AiManeuverState sub-state that UpdateShipAiSteering actually executes.
public static class UpdateShipAiObjective
{
    private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    public static void Run(ShipRec ship)
    {
        if (0 < ship.AiActionTimer && ship.AiState != ShipAiState.EscortParent)
        {
            return;
        }
        if (ship.TargetSlot != -1 && GameData.Ships[ship.TargetSlot].IsActive == 0)
        {
            ship.TargetSlot = -1;
        }
        if (ship.AiState != ShipAiState.HyperWithParent && ship.AiState != ShipAiState.HyperOut &&
            ship.AiState != ShipAiState.DefendRetreat)
        {
            ship.JumpWindupTimer = 0;
        }
        if (0 < ship.JumpWindupTimer)
        {
            if (ship.OwnerSlot == 0)
            {
                ship.AiState = ShipAiState.HyperWithParent;
            }
            else if (ship.TargetSlot == -1)
            {
                ship.AiState = ShipAiState.HyperOut;
            }
            else
            {
                ship.AiState = ShipAiState.DefendRetreat;
            }
        }

        // Government aggression roll: ship.GrudgeMissionIndex's mission ShipBehavior
        // code, reduced mod 10. 1 = defect: follow the player (AiBehaviorType 6,
        // OwnerSlot 0) while the grudge mission is active and the ship isn't
        // disabled, else revert to the ship class's normal AI. 0 = call
        // defenders and engage the player.
        if (ship.GrudgeMissionIndex != -1 && ship.AiState != ShipAiState.HyperIn &&
            ship.AiState != ShipAiState.HyperWithParent)
        {
            short behaviorMod10;
            for (behaviorMod10 = GameData.Missions[ship.GrudgeMissionIndex].ShipBehavior;
                 8 < behaviorMod10; behaviorMod10 = (short)(behaviorMod10 - 10))
            {
            }
            if (behaviorMod10 == 1)
            {
                if (GameData.MissionStates[ship.GrudgeMissionIndex].IsActive == 0 ||
                    ShipDerivedStats.IsDisabled(ship))
                {
                    ship.AiBehaviorType = GameData.ShipClasses[ship.ShipClass].InherentAI;
                    ship.OwnerSlot = -1;
                }
                else
                {
                    ship.AiBehaviorType = ShipAiType.Escort;
                    ship.OwnerSlot = 0;
                }
            }
            if (behaviorMod10 == 0)
            {
                ShipAi.CallForDefendersAndEngagePlayer(ship);
            }
        }

        if (ship.TargetSlot == 0 && WorldState.IsCloaked)
        {
            if (ShipAiType.BraveTrader < ship.AiBehaviorType)
            {
                ship.AiState = ShipAiState.Wait;
                ship.AiManeuverState = ShipManeuverState.KillSpeed;
                ship.JumpWindupTimer = 0;
                ship.NavTargetSpob = -1;
                return;
            }
            ShipAi.SetStateInert(ship);
        }

        // GoToStellar: approach the nav-target spob; settle and "park" when close + slow.
        if (ship.AiState == ShipAiState.GoToStellar && ship.NavTargetSpob != -1)
        {
            ship.JumpWindupTimer = 0;
            int navSpob = ship.NavTargetSpob;
            // dx/dy = trunc(spob - pos) as shorts (i2d idioms collapsed to plain casts).
            float deltaX = (short)(int)((float)(int)GameData.Spobs[navSpob].XPos - ship.PosX);
            float deltaY = (short)(int)((float)(int)GameData.Spobs[navSpob].YPos - ship.PosY);
            short maneuver = (short)ShipDerivedStats.EffectiveManeuver(ship);
            short approachRadius = (short)((8 - maneuver) * 48);   // park radius scales with (8 - maneuver)
            if (EvMath.FloatAbs(deltaX) <= approachRadius && EvMath.FloatAbs(deltaY) <= approachRadius)
            {
                if (ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelX) ||
                    ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelY))
                {
                    ship.AiManeuverState = ShipManeuverState.KillSpeed;
                }
                else
                {
                    ship.VelY = 0f;
                    ship.VelX = 0f;
                    ship.DockedSpobIndex = ship.NavTargetSpob;
                    ship.AiManeuverState = ShipManeuverState.None;
                    ship.AiState = ShipAiState.Idle;
                    short settleRoll = (short)SeedEvoRng.Run(200);
                    ship.AiActionTimer = (short)(settleRoll + 300);
                }
            }
            else
            {
                ship.AiManeuverState = ShipManeuverState.FlyToStellar;
            }
        }

        // HyperOut: leave the system — head home unless already far out / still moving.
        if (ship.AiState == ShipAiState.HyperOut)
        {
            if (EvMath.FloatAbs(EvMath.DistanceSquared(0f, 0f, ship.PosX, ship.PosY)) <= ShipStatConstants.AiHomeDistanceSquared)
            {
                ship.AiManeuverState = ShipManeuverState.FlyToHyperExit;
            }
            else if (ShipStatConstants.AiDriftVelThreshold <= EvMath.FloatAbs(ship.VelX) ||
                     ShipStatConstants.AiDriftVelThreshold <= EvMath.FloatAbs(ship.VelY))
            {
                ship.AiManeuverState = ShipManeuverState.KillSpeed;
            }
            else
            {
                ship.AiManeuverState = ShipManeuverState.HyperJump;
            }
        }
        if (ship.AiState == ShipAiState.HyperWithParent)
        {
            ship.NavTargetSpob = GameData.Ships[ship.OwnerSlot].NavTargetSpob;
            ship.AiManeuverState = ShipManeuverState.JumpWithParent;
        }

        // DefendRetreat: flee the target.
        if (ship.AiState == ShipAiState.DefendRetreat)
        {
            if (ship.JumpWindupTimer < 1)
            {
                if (ship.TargetSlot == -1)
                {
                    ship.AiState = ShipAiState.Idle;
                    ship.ProvokedFlag = 0;
                }
                else
                {
                    short dxAbs = (short)System.Math.Abs((short)(int)(ship.PosX - GameData.Ships[ship.TargetSlot].PosX));
                    short dyAbs = (short)System.Math.Abs((short)(int)(ship.PosY - GameData.Ships[ship.TargetSlot].PosY));
                    if (dxAbs < 251 && dyAbs < 251 && ship.AiManeuverState != ShipManeuverState.HyperJump)
                    {
                        // JumpWindupTimer < 1 here is always true (the outer guard just
                        // above already established it) — faithful to the decompile's
                        // redundant re-check.
                        if (ship.JumpWindupTimer < 1)
                        {
                            ship.AiManeuverState = ShipManeuverState.RunAway;
                        }
                    }
                    else if (EvMath.FloatAbs(EvMath.DistanceSquared(0f, 0f, ship.PosX, ship.PosY)) <= ShipStatConstants.AiHomeDistanceSquared)
                    {
                        ship.AiManeuverState = ShipManeuverState.FlyToHyperExit;
                    }
                    else if (ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelX) ||
                             ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelY))
                    {
                        ship.AiManeuverState = ShipManeuverState.KillSpeed;
                    }
                    else
                    {
                        ship.AiManeuverState = ShipManeuverState.HyperJump;
                    }
                }
            }
            else if (EvMath.FloatAbs(EvMath.DistanceSquared(0f, 0f, ship.PosX, ship.PosY)) <= ShipStatConstants.AiHomeDistanceSquared)
            {
                ship.JumpWindupTimer = 0;
                ship.AiManeuverState = ShipManeuverState.FlyToHyperExit;
            }
            else
            {
                ship.AiManeuverState = ShipManeuverState.HyperJump;
            }
        }

        // AttackShip: engage the current target (escort-chain sanity checks first).
        if (ship.AiState == ShipAiState.AttackShip)
        {
            ship.JumpWindupTimer = 0;
            if (ship.TargetSlot == -1 || 0 < ship.AiActionTimer)
            {
                ship.AiState = ShipAiState.Idle;
            }
            else
            {
                if (TargetIsInOwnEscortChain(ship)) return;

                if (ship.OwnerSlot == ship.TargetSlot && ship.DefendedSpobIndex == -1)
                {
                    ship.ProvokedFlag = 0;
                    ship.TargetSlot = -1;
                    ship.AiState = ShipAiState.Idle;
                }
                if (GameData.Ships[ship.TargetSlot].IsActive == 0 ||
                    ShipDerivedStats.IsDyingOrDestroyed(ShipTable.Ships[ship.TargetSlot]))
                {
                    ship.TargetSlot = -1;
                    ship.ProvokedFlag = 0;
                    ship.AiState = ShipAiState.Idle;
                }
                else
                {
                    if (ShipStatConstants.AiEngageDistance < EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.TargetSlot].PosX) ||
                        ShipStatConstants.AiEngageDistance < EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.TargetSlot].PosY))
                    {
                        if (!ShouldAttackTarget.Run(ship))
                        {
                            if (ship.AiManeuverState != ShipManeuverState.Afterburner)
                            {
                                ship.AiManeuverState = ShipManeuverState.MissileAttack;
                            }
                        }
                        else if (ship.AiBehaviorType < ShipAiType.Warship)
                        {
                            ship.AiState = ShipAiState.DefendRetreat;
                            ship.AiManeuverState = ShipManeuverState.RunAway;
                        }
                        else
                        {
                            ship.AiManeuverState = ShipManeuverState.HoldAndFire;
                        }
                    }
                    else if (ship.AiManeuverState != ShipManeuverState.VeerOff)
                    {
                        ship.AiManeuverState = ShipManeuverState.TurnAndFire;
                    }
                    if (!ShipDerivedStats.IsDisabled(ship))
                    {
                        AutoFireSpecialAtTarget.Run(ship);
                    }
                }
            }
        }

        // Plunder: engage-or-board (same escort-chain checks as AttackShip; a disabled,
        // unclaimed target gets a boarding approach instead of fire).
        if (ship.AiState == ShipAiState.Plunder)
        {
            ship.JumpWindupTimer = 0;
            if (ship.TargetSlot == -1 || 0 < ship.AiActionTimer)
            {
                ship.AiState = ShipAiState.Idle;
            }
            else
            {
                if (TargetIsInOwnEscortChain(ship)) return;

                if (ship.OwnerSlot == ship.TargetSlot && ship.DefendedSpobIndex == -1)
                {
                    ship.ProvokedFlag = 0;
                    ship.TargetSlot = -1;
                    ship.AiState = ShipAiState.Idle;
                }
                if (GameData.Ships[ship.TargetSlot].IsActive == 0)
                {
                    ship.TargetSlot = -1;
                    ship.NavTargetSpob = -1;
                    ship.ProvokedFlag = 0;
                    ship.AiState = ShipAiState.Idle;
                }
                else if (!ShipDerivedStats.IsDisabled(ShipTable.Ships[ship.TargetSlot]))
                {
                    ship.NavTargetSpob = -1;
                    if (ShipStatConstants.AiEngageDistance < EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.TargetSlot].PosX) ||
                        ShipStatConstants.AiEngageDistance < EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.TargetSlot].PosY))
                    {
                        if (!ShouldAttackTarget.Run(ship))
                        {
                            if (ship.AiManeuverState != ShipManeuverState.Afterburner)
                            {
                                ship.AiManeuverState = ShipManeuverState.MissileAttack;
                            }
                        }
                        else if (ship.AiBehaviorType < ShipAiType.Warship)
                        {
                            ship.AiState = ShipAiState.DefendRetreat;
                            ship.AiManeuverState = ShipManeuverState.RunAway;
                        }
                        else
                        {
                            ship.AiManeuverState = ShipManeuverState.HoldAndFire;
                        }
                    }
                    else if (ship.AiManeuverState != ShipManeuverState.VeerOff && ship.AiManeuverState != ShipManeuverState.Afterburner)
                    {
                        ship.AiManeuverState = ShipManeuverState.TurnAndFire;
                    }
                }
                else if (GameData.Ships[ship.TargetSlot].SalvageClaimed == 0)
                {
                    ship.NavTargetSpob = ship.TargetSlot;
                    short approachRadius = (short)((10 - GameData.ShipClasses[ship.ShipClass].Maneuver) * 30);
                    double dxAbsT = EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.TargetSlot].PosX);
                    double dyAbsT = EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.TargetSlot].PosY);
                    if (dxAbsT <= approachRadius && dyAbsT <= approachRadius)
                    {
                        ship.AiManeuverState = ShipManeuverState.Board;   // docked-range: match velocity
                    }
                    else if (dxAbsT <= (approachRadius << 1) && dyAbsT <= (approachRadius << 1))
                    {
                        ship.AiManeuverState = ShipManeuverState.ChaseSlow;   // close: intercept
                    }
                    else
                    {
                        ship.AiManeuverState = ShipManeuverState.Chase;     // far: pursue
                    }
                }
                else
                {
                    ship.AiState = ShipAiState.Idle;
                    ship.AiManeuverState = ShipManeuverState.None;
                    ship.TargetSlot = -1;
                    ship.NavTargetSpob = -1;
                }
            }
        }

        // ReturnToParent: rejoin the parent ship (escort/fighter).
        if (ship.AiState == ShipAiState.ReturnToParent && ship.OwnerSlot != -1 && ship.DefendedSpobIndex == -1)
        {
            ship.NavTargetSpob = ship.OwnerSlot;
            short approachRadius = (short)((10 - GameData.ShipClasses[ship.ShipClass].Maneuver) * 50);
            if (approachRadius < EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.OwnerSlot].PosX) ||
                approachRadius < EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.OwnerSlot].PosY))
            {
                ship.AiManeuverState = ShipManeuverState.ChaseSlow;
            }
            else
            {
                ship.AiManeuverState = ShipManeuverState.DockInCarrier;
            }
            if (ship.OwnerSlot == 0 && WorldState.IsCloaked)
            {
                ship.AiManeuverState = ShipManeuverState.KillSpeed;
            }
        }

        // EscortParent: approach the parent for pickup (carried fighter recall).
        if (ship.AiState == ShipAiState.EscortParent && ship.OwnerSlot != -1 && ship.DefendedSpobIndex == -1)
        {
            ship.NavTargetSpob = ship.OwnerSlot;
            short approachRadius = (short)((10 - GameData.ShipClasses[ship.ShipClass].Maneuver) * 30);
            if (ship.OwnerSlot == 0 && WorldState.IsCloaked)
            {
                ship.AiManeuverState = ShipManeuverState.KillSpeed;
            }
            else
            {
                double dxAbsP = EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.OwnerSlot].PosX);
                double dyAbsP = EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.OwnerSlot].PosY);
                if (dxAbsP <= approachRadius && dyAbsP <= approachRadius)
                {
                    ship.AiManeuverState = ShipManeuverState.FormationFly;
                }
                else if (dxAbsP <= (approachRadius << 1) && dyAbsP <= (approachRadius << 1))
                {
                    ship.AiManeuverState = ShipManeuverState.ChaseSlow;
                }
                else
                {
                    ship.AiManeuverState = ShipManeuverState.Chase;
                }
            }
        }

        // GuardPlayer: fly to the player (offer/beg encounters).
        if (ship.AiState == ShipAiState.GuardPlayer)
        {
            ship.JumpWindupTimer = 0;
            ship.NavTargetSpob = 0;
            ship.TargetSlot = -1;
            short approachRadius = (short)((10 - GameData.ShipClasses[ship.ShipClass].Maneuver) * 30);
            if (GameData.Ships[0].JumpWindupTimer < 1)
            {
                if (!WorldState.IsCloaked)
                {
                    double dxAbs0 = EvMath.FloatAbs(ship.PosX - GameData.Ships[0].PosX);
                    double dyAbs0 = EvMath.FloatAbs(ship.PosY - GameData.Ships[0].PosY);
                    short wideApproachRadius = (short)((10 - GameData.ShipClasses[ship.ShipClass].Maneuver) * 60);
                    if (dxAbs0 <= approachRadius && dyAbs0 <= approachRadius)
                    {
                        ship.AiManeuverState = ShipManeuverState.FormationFly;
                    }
                    else if (dxAbs0 <= wideApproachRadius && dyAbs0 <= wideApproachRadius)
                    {
                        ship.AiManeuverState = ShipManeuverState.ChaseSlow;
                    }
                    else
                    {
                        ship.AiManeuverState = ShipManeuverState.Chase;
                    }
                }
                else
                {
                    ship.AiManeuverState = ShipManeuverState.KillSpeed;
                }
            }
            else
            {
                ship.AiState = ShipAiState.HyperWithParent;
            }
        }
        if (ship.AiState == ShipAiState.Wait)
        {
            ship.JumpWindupTimer = 0;
            ship.AiManeuverState = ShipManeuverState.KillSpeed;
        }

        // Inspect: approach + scan the target (contraband scan / pers hail).
        if (ship.AiState == ShipAiState.Inspect)
        {
            if (ship.TargetSlot == -1 || 0 < ship.AiActionTimer)
            {
                ship.AiState = ShipAiState.Idle;
            }
            else if (GameData.Ships[ship.TargetSlot].IsActive == 0)
            {
                ship.TargetSlot = -1;
                ship.AiState = ShipAiState.Idle;
            }
            else
            {
                if (ship.TargetSlot == 0)
                {
                    WorldState.NpcScanningPlayer = 1;   // write-only; never read elsewhere (faithful)
                }
                if (ShipStatConstants.AiScanApproachDistance < EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.TargetSlot].PosX) ||
                    ShipStatConstants.AiScanApproachDistance < EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.TargetSlot].PosY))
                {
                    ship.AiManeuverState = ShipManeuverState.Chase;
                }
                else if (ship.TargetSlot == 0)
                {
                    float relVelX = ship.VelX - GameData.Ships[0].VelX;
                    float relVelY = ship.VelY - GameData.Ships[0].VelY;
                    if (ship.PersIndex == ShipRecord.KamikazePersIndex)
                    {
                        relVelY = 0f;   // the nag pers ignores relative velocity
                        relVelX = 0f;
                    }
                    if (ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(relVelX) ||
                        ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(relVelY))
                    {
                        ship.NavTargetSpob = 0;
                        ship.AiManeuverState = ShipManeuverState.FormationFly;
                    }
                    else if (ship.PersIndex == ShipRecord.KamikazePersIndex)
                    {
                        // The registration-nag pers (Cap'n Hector, slot 0x1ff): STR# 30000 lines 1..3
                        // (fresh install) or 4..6 (31+ days).
                        ship.LastVictimSlot = 0;
                        if (WorldState.InstallDays < 31)
                        {
                            TriggerSoundPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128);
                            int roll = (int)SeedEvoRng.Run(3);
                            EnqueueChatterEvent.Run(MacToolbox.GetIndString(30000, (short)(roll + 1)), 360, 0, 12, UiColors.ChatterText, 0, 0);
                            ShipAi.SetStateInert(ship);
                        }
                        else
                        {
                            TriggerSoundPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 128);
                            int roll = (int)SeedEvoRng.Run(3);
                            EnqueueChatterEvent.Run(MacToolbox.GetIndString(30000, (short)(roll + 4)), 360, 0, 12, UiColors.ChatterText, 0, 0);
                            ship.AiState = ShipAiState.AttackShip;
                            ship.TargetSlot = 0;
                            ship.NavTargetSpob = -1;
                            ship.ProvokedFlag = 1;
                        }
                    }
                    else
                    {
                        ship.NavTargetSpob = -1;
                        ship.TargetSlot = -1;
                        ship.AiState = ShipAiState.Idle;
                        ship.AiManeuverState = ShipManeuverState.None;
                        CheckContrabandScan.Run(ship);
                    }
                }
                else
                {
                    ship.AiState = ShipAiState.Idle;
                    ship.TargetSlot = -1;
                }
            }
        }
        if (ship.AiState == ShipAiState.HyperIn)
        {
            ship.JumpWindupTimer = -999;   // 0xfc19 — the hyperspace-arrival sentinel
            ship.AiManeuverState = ShipManeuverState.HyperArriveZoom;
        }

        // Refuel: refuel the target (the fuel-tanker encounter).
        if (ship.AiState == ShipAiState.Refuel && ship.TargetSlot != -1)
        {
            // AiManeuverState is set to 11 unconditionally here, then possibly
            // re-set to 11 again below — faithful to the decompile's redundant
            // pre-set.
            ship.AiManeuverState = ShipManeuverState.ChaseSlow;
            if (ShipStatConstants.AiScanApproachDistance < EvMath.FloatAbs(ship.PosX - GameData.Ships[ship.TargetSlot].PosX) ||
                ShipStatConstants.AiScanApproachDistance < EvMath.FloatAbs(ship.PosY - GameData.Ships[ship.TargetSlot].PosY))
            {
                ship.AiManeuverState = ShipManeuverState.ChaseSlow;
            }
            else if (ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelX) ||
                     ShipStatConstants.AiVelSettleThreshold <= EvMath.FloatAbs(ship.VelY))
            {
                ship.AiManeuverState = ShipManeuverState.KillSpeed;
            }
            else
            {
                ship.VelY = 0f;
                ship.VelX = 0f;
                if (ShipStatConstants.RefuelFullThreshold <= GameData.Ships[ship.TargetSlot].Fuel)
                {
                    if (ship.TargetSlot == 0)
                    {
                        TriggerSoundPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128);
                        string fuelMsg = Trunc(GameData.ShipClasses[ship.ShipClass].Name, 62)
                            + ": Fuel transfer complete, "
                            + Trunc(PilotIdentity.ShipName, 62)
                            + ".";
                        EnqueueChatterEvent.Run(fuelMsg, 240, 0, 12, UiColors.ChatterText, 0, 0);
                    }
                    ship.AiState = ShipAiState.Idle;
                    ship.AiManeuverState = ShipManeuverState.None;
                    ship.TargetSlot = -1;
                    ship.NavTargetSpob = -1;
                }
                else
                {
                    GameData.Ships[ship.TargetSlot].Fuel =
                        GameData.Ships[ship.TargetSlot].Fuel + ShipStatConstants.RefuelStepFuel;
                    if (ship.TargetSlot == 0)
                    {
                        WorldState.ShieldEnergyBarDirty = 1;
                    }
                }
            }
        }
        if (ship.AiState == ShipAiState.Idle)
        {
            ship.AiManeuverState = ShipManeuverState.None;
        }
    }

    // Escort-chain sanity check shared verbatim by AttackShip (engage) and Plunder
    // (engage-or-board): a ship must not chase its own owner, an escort sibling, or
    // a ship two hops up its own escort chain. Drops the target and resets to
    // Idle when triggered; the caller must return immediately to match the
    // decompile's bare `return`. FUN_10001d64 2186-2227 / 2281-2322 (byte-identical
    // in both callers).
    private static bool TargetIsInOwnEscortChain(ShipRec ship)
    {
        if (ship.OwnerSlot == -1) return false;
        if (ship.TargetSlot == ship.OwnerSlot ||
            ship.OwnerSlot == GameData.Ships[ship.TargetSlot].OwnerSlot)
        {
            ResetAiStateToIdle(ship);
            return true;
        }
        if (GameData.Ships[ship.TargetSlot].OwnerSlot != -1 &&
            GameData.Ships[ShipTable.Ships[ship.TargetSlot].OwnerSlot].OwnerSlot != -1)
        {
            if (GameData.Ships[ship.TargetSlot].OwnerSlot ==
                GameData.Ships[ShipTable.Ships[ship.TargetSlot].OwnerSlot].OwnerSlot)
            {
                ResetAiStateToIdle(ship);
                return true;
            }
            if (GameData.Ships[ship.OwnerSlot].OwnerSlot != -1 &&
                GameData.Ships[ShipTable.Ships[ship.OwnerSlot].OwnerSlot].OwnerSlot ==
                GameData.Ships[ShipTable.Ships[ship.TargetSlot].OwnerSlot].OwnerSlot)
            {
                ResetAiStateToIdle(ship);
                return true;
            }
        }
        return false;
    }

    private static void ResetAiStateToIdle(ShipRec ship)
    {
        ship.AiState = ShipAiState.Idle;
        ship.AiManeuverState = ShipManeuverState.None;
        ship.TargetSlot = -1;
    }
}
