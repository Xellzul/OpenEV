namespace OpenEV.Override.Ports.Ship.Model;

// Typed managed C# object for ONE ship record (formerly 0xa82 bytes in the
// EvoMemory byte-dictionary). One instance per slot, held in ShipTable.Store[36];
// the ShipRec handle reads/writes these fields now that the old EvoMemory byte
// range was removed (see Misc.OriginalGameStateTotalBytes). Named "ShipRecord"
// (not "Ship") because OpenEV.Override.Ports.Ship is an existing NAMESPACE
// (Ship.FindNextShipSlot, etc.).
//
// Each field carries its byte offset into the original 0xa82-byte record (the
// decompile addresses them as _DAT_1008a4f8 + slot*0xa82 + offset). The names were
// recovered by tracing every read/write across the decompile; the offset stays in
// the comment for cross-referencing.
public sealed class ShipRecord
{
    // +0x00 / +0x04  world position. +0x08 / +0x0c  velocity.
    public float PosX;
    public float PosY;
    public float VelX;
    public float VelY;

    // +0x24 facing; +0x28 mirror; +0x2a nav mode; +0x2c spob; +0x30 target; +0x34 system.
    public short Heading;
    public short HeadingPrev;
    public short NavMode;
    public short NavTargetSpob;
    public short TargetSlot;
    public short CurrentSystem;

    // +0x60 credits; +0x68 current shield (signed: positive = shield strength,
    // negative = armor damage taken once shields are depleted). Max = EffectiveShieldMax.
    public int Credits;
    public float Shield;

    // AI motion command pair (transient scratch, recomputed every frame by the steering
    // handlers and fed to AccelerateAlongHeading/CapVelocity): +0x10 desired acceleration,
    // +0x14 desired/target speed.
    public float DesiredAccel;
    public float DesiredSpeed;
    // +0x18 current fuel/energy (jumps + fuel-using weapons). Max = EffectiveFuelMax;
    // initialized to ShipClass.BaseFuel on spawn, refilled at spaceports.
    public float Fuel;
    // +0x1c death/destruction countdown (the "dc" debug number): <=0 alive (fresh pilot = -1);
    // seeded from ShipClass.DeathDelay once armor is exhausted, decremented one unit per tick,
    // driving the staged burn-off explosions / escape-pod ejection / final removal at <= -240.
    public float DeathTimer;
    // +0x20 per-NPC pilot-skill multiplier (~1.0 with random variance from the class SkillLevel,
    // rolled once at spawn) applied to base accel/speed on the non-player branch of
    // EffectiveAccel/EffectiveSpeed. The player slot writes a default that is never read.
    public float PilotSkillScale;

    // +0x26 AI action/hold countdown: while >0 the ship holds its current maneuver and cannot
    // turn, accelerate, fire, board, or pick a new objective; decremented per frame, re-armed to
    // randomized durations when starting a maneuver, clamped to 0x14 on taking damage. Read <1 to act.
    public short AiActionTimer;
    // +0x2e target-lock-engaged flag: set to 1 on target acquire (and spawn default), read ==1
    // together with TargetSlot to draw target brackets and clear a dead lock. (Never cleared to 0.)
    public short HasTargetLock;
    // +0x32 selected weapon slot (index into the 64 weapon-slot arrays, -1 = none): the player's
    // cycled secondary/special weapon (HUD "No Secondary Weapon" when -1), or the AI-picked weapon.
    public short SelectedWeaponSlot;
    // +0x36 index into the ship-class table (stride 0x196). EmptyShipClass (the all-bits-set
    // 6-bit sentinel) marks an unused/empty ship slot.
    public const short EmptyShipClass = 0x3f;
    public short ShipClass;
    // +0x38 index into the düde-spawn table (DudeSpawnTable, stride 0x20) this ship was spawned
    // from, or -1 (player, wingmen, captured/special-weapon ships); read back for bar/trade/govt.
    public short DudeSpawnIndex;

    // +0x3a..+0x44: the cargo hold — 6 shorts, the quantity of each of the 6 base trade
    // commodities (indexed by commodity type 0..5, named via STR# 0xfa3). Stride 2.
    public const int CargoHoldCount = 6;          // base trade commodities (ASM bound 5, ble)
    public short[] CargoHold = new short[CargoHoldCount];

    public short SlotIndex;             // +0x46 the slot's own index (self-reference, 0..0x23).
    // +0x48 active AI behaviour type selecting the per-frame AI routine (see ShipAiType).
    // Default = ShipClass.InherentAI.
    public ShipAiType AiBehaviorType;
    // +0x4a mission slot (0-7) this NPC was spawned as an aux ship for, or -1; on despawn its
    // spawn budget (RemainingSpawnCount) is refunded to that mission slot (CleanupSystNpcs).
    public short SpawningMissionSlot;
    // +0x4c spob index this ship defends as a tribute/spaceport defender, or -1. While set, the
    // ship can't be a boardable hulk, always engages the player, won't friendly-fire co-defenders,
    // is excluded from combat-rating/grudge, and pays one tribute to that spob on despawn.
    public short DefendedSpobIndex;
    // +0x4e cached target heading for a strafing pass (AI substate 0x10): Heading ± 0x87 (135°,
    // sign by slot parity so neighbours peel opposite ways), wrapped to [0,360).
    public short StrafeHeading;
    // +0x50 interceptor "last victim" slot (a target-of-record distinct from the live TargetSlot):
    // excludes the same ship from immediate re-targeting; cleared to -1 when that slot deactivates.
    public short LastVictimSlot;
    public short DockedSpobIndex;       // +0x52 the spob this ship's jump route is keyed to.
    // +0x54 turret muzzle round-robin (0..3): incremented mod 4 on every turret-type shot and
    // used to index the class's 4 TurretYDisp mounts so consecutive shots stagger their spawn point.
    public short TurretMountCycle;
    // +0x56 the system this ship most recently jumped FROM (to orient its arrival); -1 = none,
    // -2 = materialised at the hyperspace edge / spawned in-place.
    public short PriorSystem;
    // +0x58 hyperspace jump-windup timer: 0 idle (free to fire/retarget/open dialogs), >0 spinning
    // up (frames since the AiTickStamp start), -999 (0xfc19) the jump-armed/just-arrived sentinel.
    // The `>-900 && <1` idiom distinguishes the sentinel from idle. Escorts copy the leader's value.
    public short JumpWindupTimer;
    // +0x5a provoked/under-attack latch: raised (incoming damage summed in, read as >0) when the
    // ship is hit/fired upon, latching the attacker as target; while >0 with a target it retaliates.
    public short ProvokedFlag;
    public short Govt;                  // +0x5c the ship's nominal government index.
    // +0x5e owner/leader ship-table slot: 0 = player-owned, -1 = no owner (independent or
    // abandoned derelict), else the slot of the carrier/leader it escorts. Backbone of
    // IsPlayerOrEscort, escort-follow, and the derelict demotion.
    public short OwnerSlot;

    // +0x64 a raw-int TickCount() stamp marking when the current hyper-windup/settle began;
    // elapsed = now - AiTickStamp. The ASM stores AND reads it as a raw int everywhere (stw/lwz in
    // FUN_10025074 / FUN_1000366c / FUN_10027830). (Earlier the windup writers/readers used a
    // (float)TickCount() bit-reinterpret — a decompile float*-typing artifact, NOT an original
    // convention; a raw stamp read as float collapsed to ~0 -> huge dt -> "ship flies off-screen".)
    public int AiTickStamp;

    // byte region
    // +0x6c the slot has a live in-world sprite/render node (set after node alloc, cleared on
    // teardown) — read everywhere as "this ship exists/is present right now".
    public byte HasWorldSpriteNode;
    public byte IsActive;               // +0x6d the slot is allocated/active (a ship occupies it).
    // +0x6e — one-shot "the disabled hull has been claimed" flag. 0 on every fresh spawn;
    // set to 1 the moment the ship's disabled hulk is consumed and can no longer be boarded:
    // player boards/plunders it (TickShipAI board path, ShowBoardingDialog), the crew
    // abandons it into an owner-less derelict (UpdateShipAiFrame), it is captured/swapped
    // (RunShipCaptureSwap), or it spawns pre-consumed (SpawnGovtDefender, RunFleetSpawner).
    // Read as ==0 to allow boarding, distress calls, and govt boarding-mode behaviour.
    public byte SalvageClaimed;
    // +0x6f a weapon slot (SelectedWeaponSlot) is committed to fire: the AI fires only when this
    // is set AND SelectedWeaponSlot != -1. Set/cleared by the weapon-target-picking routines.
    public byte HasSelectedWeapon;
    // +0x70 caught in a tractor beam this frame (set by a negative-damage hit, cleared each AI
    // frame so it must be re-asserted): drags velocity toward the player and uses the tractored stat scale.
    public byte IsTractored;
    public byte IsCarriedFighter;       // +0x71 the ship is a fighter currently docked in a carrier bay.
    // +0x72 one-shot guard: the ship has already delivered its scripted pers/mission hail quote
    // (barred from repeating when the mission's "say once" flag 0x80 is set).
    public byte HailQuoteSpoken;
    // +0x73 afterburner-enable (shïp 0x40 AfterburnerAlways / 0x20 AfterburnerAdvancedRating / përs 0x2 PodAndAfterburner);
    // also drives the AI re-approach — a flagged ship that drifts >~82 units from its target re-closes.
    public byte HasAfterburner;

    public const int WeaponSlotCount = 64; // weapon slots per ship (ASM bound 0x40)

    // weapon slot arrays — WeaponSlotCount slots × 0x28 bytes each, starting at +0x74.
    // WeaponSlotType[i] = +0x74+i*0x28, WeaponSlotAmmo[i] = +0x7c+i*0x28,
    // WeaponSlotReload[i] = +0x98+i*0x28.
    public short[] WeaponSlotType = new short[WeaponSlotCount];
    public short[] WeaponSlotAmmo = new short[WeaponSlotCount];
    public float[] WeaponSlotReload = new float[WeaponSlotCount];

    // +0xa4c one-shot "credits Easter-egg already shown" flag (player slot, read <1 to play once);
    // +0xa54 the credits scroll speed set by that path.
    public short CreditsEasterEggShown;
    public short CreditsScrollSpeed;

    // tail short cluster
    public short AiState;               // +0xa74 primary AI state (see ShipAiState).
    // +0xa76 secondary AI state / immediate maneuver order (0..0x11): UpdateShipAiObjective picks
    // it from AiState + geometry, UpdateShipAiSteering dispatches one movement handler per value.
    public short AiManeuverState;
    // +0xa78 courage/aggression level: governs flee threshold (1 timid / 2 normal / 4 fearless)
    // and engagement range (value × 600). Seeded from the pers record or randomized at spawn.
    public short AiCourage;
    // +0xa7a per-frame incoming-damage threat accumulator: reset to 0 then summed over every live
    // projectile homing on this ship, compared against remaining shield/armor for the retreat decision.
    public short IncomingDamageThreat;
    // +0xa7c index into the pers table, or -1. The last two pers slots (510/511) are
    // reserved specials outside the normal pers-lookup pool (SpawnPers excludes them from
    // its random pick, FUN_1006c110 line 44517 rolls SeedEvoRng(510)):
    //   EngagePlayerPersIndex (510) — the "call for defenders and engage the player" pers
    //     TickPersNagHook (FUN_1006f150) spawns.
    //   KamikazePersIndex (511) — Cap'n Hector, the Ambrosia-mascot pers LoadPersResources
    //     hand-builds; spawned as the "reinforcement" escort by SpawnReinforcement
    //     (FUN_10060a1c), doubles as the registration-nag speaker in UpdateShipAiObjective
    //     (FUN_10001d64, STR# 30000), and gets stat boosts in ShipDerivedStats.
    public const short EngagePlayerPersIndex = 0x1fe;
    public const short KamikazePersIndex = 0x1ff;
    public short PersIndex;
    // +0xa7e the active-mission slot this ship is grudge-bound to (index into the mission
    // detail/state table), or -1. Gates grudge mode, engage/jump behaviour, and mission cargo.
    public short GrudgeMissionIndex;
    // +0xa80 alternating gun-mount toggle (±1): flips on every shot so WeaponShotSpread mirrors the
    // horizontal muzzle offset, emitting consecutive shots from alternating left/right mounts.
    public short AltFireSide;
}
