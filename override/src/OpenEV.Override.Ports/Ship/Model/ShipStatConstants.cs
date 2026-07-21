namespace OpenEV.Override.Ports.Ship.Model;

// Ship physics / damage / spawn / search constants from the PEF data segment,
// now MANAGED C# literals. Every value was dumped from the original data seg
// (tools/dump_dataseg.py) and its WIDTH verified against the decompile read
// (`(double)_DAT_x` = float promoted, bare `_DAT_x` in double arith = double).
// The old address bands, now retired:
//   [0x10081d48,0x10081e34)  AI-frame physics / damage-formula band
//   [0x10082220,0x10082288)  skill/reinforce/impact band
//   [0x100822c8,0x100822e0)  nearest-search band (+ the 0x100822d8 i2d bias)
//   [0x10082300,0x100823f0)  capture/retreat/particle/spawn/fleet/damage band
// The i2d magic biases that lived inside these bands (0x10081dd8, 0x100822d8,
// 0x100823c8 = 2^52+2^31) have NO consts here — the `PpcMagic.IntToDouble(x) - bias`
// idiom collapses to a plain `(double)x` cast at every site.
public static class ShipStatConstants
{
    // ---- AI-frame physics band [0x10081d48,0x10081e34) ----
    // Weapon-reload skill scales (UpdateShipAiFrame). FLOATS — a double-width
    // read here picks up raw bit patterns (~1e9), not the intended value.
    public const float ReloadSkillScale1 = 1.1f;   // 0x10081d50
    public const float ReloadSkillScale2 = 1.25f;  // 0x10081d54
    public const float ReloadSkillScale3 = 1.5f;   // 0x10081d58
    public const float ReloadSkillScale4 = 1.75f;  // 0x10081d5c
    public const double AiFrameScale4p6 = 4.6;    // 0x10081d60
    public const float VelocityDampingD = 0.95f;  // 0x10081d68
    public const float HalfAngleWrap = 180f;   // 0x10081d6c
    public const float AngleWrapPeriod = 360f;   // 0x10081d70
    public const float VelocityDampingFactor = 0.995f; // 0x10081d74
    public const float OneFloat = 1f;     // 0x10081d78
    public const double ProjectionScale = 1000.0; // 0x10081d80
    public const float RotationMatrixB = 5f;     // 0x10081d88
    public const float RotationMatrixA = 95f;    // 0x10081d8c
    public const double ProjectionOuterScale = 0.01;   // 0x10081d90
    public const double AimDistanceScale = -1.0;   // 0x10081d98
    public const double AimDistanceMax = 0.0;    // 0x10081da0
    public const double CollisionCrossThreshold = 15.0;   // 0x10081da8
    public const double CollisionSlopeFactor = 1.5;    // 0x10081db0
    public const double AiFrameScale2p0 = 2.0;    // 0x10081db8
    public const double AiFrameScale0p3 = 0.3;    // 0x10081dc0
    public const float SubmunitionCountScale = 8f;     // 0x10081dc8 (UpdateProjectilePositions burst-2 count)
    public const float BlastJitterBase = -25f;   // 0x10081dcc (impact-point jitter: -25 + rand(50))
    public const double AiFrameScale0p66 = 0.66;   // 0x10081dd0
    // 0x10081dd8 = i2d bias (idiom collapses; no const)
    public const float ArmorTier8Threshold = 60f;    // 0x10081de0
    public const float ArmorTier4Threshold = 40f;    // 0x10081de4
    public const float ArmorTier2Threshold = 20f;    // 0x10081de8
    public const float DamageScaleC = 10f;    // 0x10081dec
    public const double DamageRandScale = 0.5;    // 0x10081df0
    public const double DamageSpreadScale = 0.075;  // 0x10081df8
    public const double DamageBaseB = 25.0;   // 0x10081e00
    public const double DamageScaleB = 0.15;   // 0x10081e08
    public const double DamageDivisor = 50.0;   // 0x10081e10
    public const float ArmorMidThreshold = 2f;     // 0x10081e18
    public const float ArmorDamageScale = 3f;     // 0x10081e1c
    public const float ZeroFloat = 0f;     // 0x10081e20
    public const double SpriteBoundsScale = 0.25;   // 0x10081e28
    public const float MinMoveThreshold = 32700f; // 0x10081e30 (world-bound magnitude gate)

    // ---- skill / reinforce / impact band 0x100822xx ----
    public const double SkillVariationScale = 0.01;   // 0x10082220
    public const double ReinforceShieldScale = 10.0;   // 0x10082228
    public const double ReinforceBaseOffset = 500.0;  // 0x10082230
    public const float ReinforceSpreadStep = 1.165f; // 0x10082238
    public const float ReinforceSpreadStart = 50f;    // 0x1008223c
    // Ship-disabled armor thresholds (ShipDerivedStats): disabled when
    // shield×100 < scale×maxArmor (shield is negative once armor is taken).
    public const double DisableArmorScaleStd = -33.333; // 0x100822b8 (class flag 0x10 clear)
    public const double DisableArmorScaleTough = -90.0;   // 0x100822c0 (class flag 0x10 set)
    // Nearest-search: best-distance starts at -1 ("none found yet"); the accept test is
    // `dist < best || best < 0` — NearestSearchMaxDist is the init sentinel, NearestSearchEpsilon
    // the none-found comparand. NearestSearchEpsilon (the pooled 0f at 0x100822cc) also serves
    // IsDyingOrDestroyed's DeathTimer>0 gate and PropagateSystemKillImpact's zero assignment.
    public const float NearestSearchDist1000 = 1000f;  // 0x100822c8
    public const float NearestSearchEpsilon = 0f;     // 0x100822cc
    public const float NearestSearchMaxDist = -1f;    // 0x100822d0
    // 0x100822d8 = i2d bias (idiom collapses; no const)

    // ---- capture / retreat / particle / spawn band [0x10082300,0x100823f0) ----
    public const double KillImpactSeedScale = 0.2;    // 0x10082300
    public const float CaptureOffsetN150 = -150f;  // 0x10082308
    // FLOAT — a double-width read here spans this value plus the next word,
    // producing a ~6.0e12 distance (captured ships would spawn absurdly far away).
    public const float CaptureSpawnDist = 75f;    // 0x1008230c
    public const double RetreatScaleArmorDamaged = -1.05; // 0x10082310 (value < 0: armor gone)
    public const double RetreatScaleShieldUp = 1.05;   // 0x10082318 (value >= 0: shield intact)
    public const double DeathParticleScale0 = 0.16;   // 0x10082320 (HandleProjectileDeath type-0 spark scale)
    public const double DeathParticleScale1 = 0.04;   // 0x10082328 (type-1)
    // 0x10082330/38/40 dust/escort-wage scales live in EvoMath.MathConstants (Dust*, OnePercent).
    public const double EscortShotLifeScale = 1.5;    // 0x10082348 (escort-fired projectile lifetime)
    public const float VelocityFieldScale = 0.8f;   // 0x10082350 (projectile velocity damp)
    public const double EscapePodSpeedDivisor = 100.0;  // 0x10082358
    public const double PostMortemWageDivisor = 5.0;    // 0x10082360
    public const float RespawnSpeedStep = 45f;    // 0x10082368
    public const float NoFleetSentinel = -1f;    // 0x1008236c
    public const float FleetScatterBase = -256f;  // 0x10082370
    public const float SpawnBaseOffset = 1000f;  // 0x10082374
    public const float SpawnSpreadStep = 1.165f; // 0x10082378
    public const float SpawnSpreadStart = 50f;    // 0x1008237c
    // 1.75× multiplier on a ship class's DefaultWeaponAmmo when spawning an NPC
    // (the player branch uses the 0.5 Half).
    public const double NpcWeaponAmmoScale = 1.75;   // 0x10082380
    public const float ArrivalSpeed = 100f;   // 0x10082388 (FLOAT, despite the double-typed neighbors)
    public const double ZeroDouble = 0.0;    // 0x10082390 (generic zero comparand shared by two unrelated gates — don't rename for either site)
    public const double SpawnArmorScale = -0.6;   // 0x10082398 (×BaseArmor → pre-damaged spawn)
    public const double SpawnArmorScaleTough = -0.85;  // 0x100823a0 (DisabledAt10PctArmor class)
    public const float SplashRangeMax = 320f;   // 0x100823a8
    public const double ArmorLossScaleX = 0.6;    // 0x100823b0
    public const double ArmorLossScaleY = 0.85;   // 0x100823b8
    public const double DamageScaleX = 0.25;   // 0x100823c0
    // 0x100823c8 = i2d bias (idiom collapses; no const)
    public const float BlastParticleScale0 = 8f;     // 0x100823d0 (RunWeaponHitDispatcher type-0 ring)
    public const double Half = 0.5;    // 0x100823d8 (generic ½ multiplier; many unrelated call sites — don't rename for one)
    public const double CoordNormalizeDivisor = 50.0;   // 0x100823e0
    public const float BlastParticleScale1 = 2f;     // 0x100823e8 (type-1 ring)
    public const float SpawnZeroDefault = 0f;     // 0x100823ec

    // The TickShipAI player-flight constant band 0x10081E68..0x10081F44 (the
    // f-row floats — cooldown step 1.0, hyper fuel/windup/entry, heading wraps,
    // world bounds ±20256, afterburner cost — plus the toc-0x19xx tuning doubles
    // and the 2^52 / 2^52+2^31 i2d biases) was migrated to inline C# literals /
    // PpcMagic.U2dBias/I2dBias in TickShipAI + WeaponSlotTick.

    // ---- NPC-AI steering/physics constant band — MANAGED LITERALS ----
    // Data-seg band [0x10081ad0, 0x10081bcc); every reader uses these literals
    // now. The 2^52 i2d biases inside the band
    // (0x10081ae8 / 0x10081ba0) are PpcMagic.U2dBias/I2dBias — their idioms collapse
    // to (double)(int)x / UIntToDouble-bias at the sites.
    public const double AiProximityScale = 0.8;       // 0x10081ad0 (ShouldAttackTarget)
    public const double AiRangeScaleA = 2.5;       // 0x10081ad8 (PickForwardWeaponForTarget)
    public const double AiBeamRangeSquaredScale = 0.95;      // 0x10081ae0 (PickHomingWeaponForTarget)
    public const float AiIdleAccel = -1.165f;   // 0x10081af0 (steering substate 10)
    public const float AiIdleMaxSpeed = -50f;      // 0x10081af4 (steering substate 10)
    public const double AiVelMatchAccelScale = 0.66;      // 0x10081af8 (substate 0xc)
    public const float AiFarAimDistance = 360f;      // 0x10081b00 (substates 9/0xb: beyond this, aim directly)
    public const double AiNudgeSpeedScale = 0.1;       // 0x10081b08 (substate 0xf formation nudge)
    public const float AiFormationGap = 3f;        // 0x10081b10 (substate 0xf)
    public const double AiVelMatchThreshold = 0.525;     // 0x10081b18 (substates 0xc/0xf)
    public const double AiBreakoffSpeedScale = 1.8;       // 0x10081b20 (substate 0x11)
    public const double AiBreakoffAccelScale = 2.75;      // 0x10081b28 (substate 0x11)
    public const double AiStrafeAccelScale = 1.5;       // 0x10081b30 (substate 0x10)
    public const float AiCarriedMaxSpeed = -1f;       // 0x10081b38 (substate 0xd)
    public const float AiJumpWindupTicks = 466f;      // 0x10081b3c (substate 4; / SpriteScale)
    public const float VelocityDampingB = 0.95f;     // 0x10081b40
    public const float VelocityDampingC = 0.94f;     // 0x10081b44
    public const double AiSettleAccelScale = 0.5;       // 0x10081b48 (substates 1/0xb slow-approach: KillSpeed and ChaseSlow)
    public const double AiVelSnapThreshold = 1.75;      // 0x10081b50
    public const double AiVelHeadingScale = 100.0;     // 0x10081b58 (velocity->heading scale)
    // 0x10081b60 = 0.0 (double) and 0x10081b88 = 0.0f — plain 0 literals at the sites, no consts here.
    public const float RefuelStepFuel = 1f;        // 0x10081b68 (objective state 9 fuel/tick)
    public const float RefuelFullThreshold = 100f;      // 0x10081b6c
    public const float AiEngageDistance = 165f;      // 0x10081b70
    public const double AiDriftVelThreshold = 0.7;       // 0x10081b78
    public const double AiHomeDistanceSquared = 1000000.0; // 0x10081b80 (vs DistanceSquared)
    public const double AiVelSettleThreshold = 0.35;      // 0x10081b90
    public const float MaxEngageRange = 7200000f;  // 0x10081b98 (FLOAT — an int-width read here yields raw bit patterns, not the value)
    public const float AiScanApproachDistance = 180f;      // 0x10081bc8 (FLOAT — a double-width read here spans garbage)

    // AI shield-fraction / speed scales — MANAGED literals (values dumped from the PEF
    // data segment; source doubles 0x10081ba8..0x10081bc0). Consumed by
    // TickDefenderAi (FUN_100007d4) — see
    // its own header for the surrender/flee decision (DefendRetreat) this drives.
    public const double DefenderFleeShieldFraction = 0.25; // was 0x10081bc0 — pers flag 0x100 path; ALSO UpdateShipAiSteering (FUN_1000366c)'s quarter-speed scale
    public const double DefenderShieldFractionType1 = 0.3;  // was 0x10081bb8 — ship +0xa78 == 1
    public const double DefenderShieldFractionType2 = 0.15; // was 0x10081bb0 — ship +0xa78 == 2
    public const double DefenderMissionShieldFraction = 0.01; // was 0x10081ba8 — mission-linked (+0xa7c) path, × mission +0x08 short

    // Retreat / morale armor-threshold scales (ArmorBelowRetreatThreshold): see
    // RetreatScaleArmorDamaged / RetreatScaleShieldUp above.

    // The PEF data-seg ship-physics scale/divisor constants (0x10082288..0x100822b0,
    // EffectiveSpeed/Accel/ShieldRecharge/Maneuver) were migrated to managed C# literals
    // (Ship.Model.ShipPhysicsConstants).
}
