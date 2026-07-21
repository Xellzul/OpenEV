namespace OpenEV.Override.Ports.Core.Model;

// Typed managed home for the scalar world-state that InitGameWorldState
// (FUN_10053ab0) reads and writes by raw data-segment / BSS address. Each value
// now lives in EXACTLY ONE place — a real C# field here — instead of a hex-keyed
// EvoMemory slot.
//
// Three sub-groups:
//   * Spawn-default constants — read-only floats from the PEF data segment,
//     SEEDED once at boot (OriginalGameStateTotalBytes).
//   * Mutable world flags — set by InitGameWorldState; default 0/false.
//   * Galaxy-map view-centre — the current system's coords cached for the map.
public static class WorldState
{
    // ── Spawn-default constants (read-only PEF data-seg floats, seeded at boot) ──
    // Five contiguous data-seg floats reused by the two world-reset functions. Names
    // follow the player-init path (FUN_10053ab0); 0x10082144 doubles as the per-ship
    // +0x1c / weapon-slot-angle default in the all-ships reset (FUN_10052fa4).
    public static float SpawnPosDefault;     // _DAT_1008213c — player PosX / PosY
    public static float SpawnVelDefault;     // _DAT_10082144 — player VelX / VelY; ship +0x1c + weapon angle
    public static float SpawnField1cDefault; // _DAT_10082138 — player +0x1c
    public static float SpawnField20Default; // _DAT_10082140 — ship +0x20
    public static float SpawnFuelDefault;    // _DAT_10082148 — ship +0x18 (Fuel)

    // ── Mutable world flags / counters owned by InitGameWorldState ──
    // _DAT_1008f774 (toc + 0x1c45*4) — player landing/docking sequence state, the temporal
    // partner of LandingTargetSpob (always reset together): -1 idle, 0 request-sent / denied,
    // 749 cleared-but-still-out-of-range, 750 cleared-to-land (final-approach trigger); then
    // ramps 750→2047 each tick on final approach and resets to -1 once past 2047. The
    // spaceport bribe path pre-loads 751 — one past the trigger — to skip the request handshake.
    public static short LandingApproachState;
    // _DAT_1008f776 — spob index the entity is landing on / launching from (-1 = none).
    // Set from NavTargetSpob during descent; read for the landed spob's props and the
    // "Leaving <spob>" chatter; cleared to -1 when not landing.
    public static short LandingTargetSpob;

    // "An NPC scanner is approaching the player" latch (DAT_10085d4c — the old
    // "ResourceSubsystemReadyFlagSlot" name was a misname). Set by the NPC objective
    // AI (scan state), cleared by the per-frame Tick; NEVER READ anywhere (faithful
    // original quirk).
    public static byte NpcScanningPlayer;

    // The shareware registration-match byte (was 0x1008f5cc, toc+0x6f6c).
    // TitleMainLoop seeds it from CheckShareWareRegistrationMatch (currently inert);
    // 0 = unregistered: SpawnFleetShips runs its reinforcement path and
    // BankRobberyNewsEvent can rob rich pilots.
    public static byte SharewareRegisteredMatch;

    // Player combat rating (kills measured in crew). Was the int behind the
    // 0x10080d0c pointer cell (the old "PlayerScoreRecordSlot"/"PlayerDayCounter"
    // names were both misnames); pilot-file round-trips at record offset 0x26ea.
    public static int PlayerCombatRating;

    // (0x10086ad0 / 0x10086ae0 — the bar-greeting variant seed and the bribe-willingness
    // roll — are NOT duplicated here: they are the spaceport-comm dialog's own cells,
    // owned by Dialog.Model.DialogScratch.SpaceportSelCellA / SpaceportBribeRoll. The world-init
    // and jump-arrival reseed / invalidate writes target those fields directly.)

    // ── Star-jitter pair (starfield twinkle origin, one short per axis) ──
    // Was the BSS short[2] behind the pointer cell PTR_DAT_10080ddc
    // (Pilot.Model.PilotSaveSources.StarJitterSlot). ReseedStarJitter
    // writes rand(0x15)+0x5a per axis, InitGameWorldState resets both to 100,
    // and pilot files round-trip the pair at aux-record offset 0x22fa
    // (SavePilotFile / LoadPluginPilotData).
    public static readonly short[] StarJitter = new short[2];

    // ── Star-drift pair (starfield drift origin, one short per axis) ──
    // Was the BSS short[2] behind the pointer cell PTR_DAT_10080de0 (the old
    // PilotSaveSources "GalaxyStateSlot" misname). TickStarJitter
    // random-walks each axis ±1 per tick clamped to [0x55, 0x73], InitGameWorldState
    // resets both to 100, and pilot files round-trip the pair at aux-record offset
    // 0x22f6 (PilotAuxRec.GalaxyState).
    public static readonly short[] StarDrift = new short[2];

    // ── HUD redraw dirty-flag byte cluster 0x1008f3b0..f3b6 (toc+0x6d50..56) ──
    // Set to 1 by combat/dialog/spawn events; TickHudRedrawScheduler tests each,
    // repaints the matching HUD region, and clears it. All BYTE-width in the
    // decompile (DAT/cRam/uRam1008f3bx) — SpawnFromShip's WriteShort on f3b6 was
    // an early transcription width bug. Names kept from the old Core.Model.WorldFlags slot consts.
    public static byte SpawnPulseDirty;         // DAT_1008f3b0
    public static byte PlayerShieldBarDirty;    // 1008f3b1 — forces a player-shield-bar redraw (DrawPlayerShieldBar); was "JammingDirty1"
    public static byte HudStatusPanelDirty;     // 1008f3b2 — forces a HUD status-panel redraw (RedrawHudStatusPanel, via TickHudRedrawScheduler); was "JammingDirty2", a leftover misname
    public static byte HudWeaponPanelDirty;     // 1008f3b3 — forces a HUD weapon-panel redraw (RedrawHudWeaponPanel); was "JammingDirty3"
    public static byte ShieldEnergyBarDirty;    // 1008f3b4 — forces a shield/energy-bar redraw (DrawShieldEnergyBar); was "JammingDirty4"
    public static byte RadarRedrawDirty;        // uRam/cRam1008f3b5 (HUD radar redraw-dirty)
    public static byte WeaponSlotDirty;         // uRam/cRam1008f3b6

    // toc+0x6d58 — write-only: zeroed at boot beside PilotLoaded (0x6d59), never read
    // anywhere (confirmed: decompile, disassembly single xref, port). Vestigial companion
    // flag; original meaning unrecoverable. Kept for bug-for-bug parity with the boot clear.
    public static byte UnreadBootFlag6d58;
    // DAT_1008f3b9 / toc+0x6d59 — "a pilot is loaded / active" flag. Written by
    // New Pilot, Open Pilot, and hyperspace arrival; read by DrawPilotInfo (panel
    // gate), the title main loop, and the New-Pilot/Enter-Ship dispatch branches.
    // (Was EvoGlobals.PilotLoaded.)
    public static bool PilotLoaded;
    // DAT_1008f3c0 — "cursor hidden by game" flag. HideCursorOnce sets it; title
    // idle + gameplay show/hide-cursor paths read it. (Was EvoGlobals.IsCursorHiddenByGame.)
    public static bool IsCursorHiddenByGame;

    // The main-loop last-tick timestamp (was the BSS int behind ptr cell
    // 0x10081234; RunMainGameLoop owns it).
    public static int MainLoopLastTick;
    public static byte MenuBarHidden;           // cRam1008f3c1 — menu-bar-currently-hidden flag
    // DAT_1008f3c8 — the Caps-Lock 2x game-speed flag (NOT a pause; the old
    // "CinematicPaused" name was a misnomer). RunMainGameLoop sets it each frame
    // while Caps Lock (EVO keycode 0x31 = modern Caps Lock) is latched on; when set,
    // the loop runs an EXTRA game tick (FUN_10062638(0) + TickSpriteSystem) per frame,
    // so the game advances at ~2x. Suppressed mid boarding-chime so that cutscene
    // isn't fast-forwarded.
    public static byte DoubleSpeedActive;       // DAT_1008f3c8
    // DAT_1008f3c9 — once-per-session "first-entry intro cutscene shown" flag.
    // (Was EvoGlobals.FirstEntryCutsceneShown.)
    public static bool FirstEntryCutsceneShown;

    // ── Frame/tick counters + UI state (0x1008f72c cluster) ──
    // sRam1008f72c — cadence clock: TickShipAI increments it each frame, wraps at
    // 0x400; consumers gate periodic work via % 3 / % 5 / % 0x3c / % 200 tests.
    public static short GameFrameTickCounter;
    // 0x1008f72e (toc+0x70ce) — set 1 at new-pilot world init (FUN_10054b44); never read
    // anywhere in the recovered binary, so its meaning is unknown (faithful write-only quirk,
    // kept as an honest placeholder).
    public static short FlagF72e;
    // 0x1008f730 (toc+0x70d0, *(short*)(local_cc + 0x1c34)) — days since install,
    // cached by the title main loop from GetInstallHours; news/reinforcement code
    // tiers behaviour on < 15/31/61-day thresholds. The old WorldFlags const
    // called this "ReinforcementTier" — a misname. (The port's TitleMainLoop writer had
    // a dropped-x4 + _toc-vs-GameToc bug, so the real cell was never written.)
    public static short InstallDays;
    // sRam1008f734 — Commodity Exchange dialog's selected row (0-5 cargo commodities,
    // 6-7 junk/outfit slots); read/written only by the commodity-trade FUNs (FUN_10034c20/
    // FUN_1003579c). Not the player-info dialog's PlayerInfoPage below — different dialog.
    public static short TradeCurrentTab;

    // Player-info dialog PAGE (1=pilot/ship stats, 2=cargo, 3=extras; the capture
    // dialog reuses page 4). Was the short behind the 0x10080fd4 pointer cell
    // (Tab-key cycled in PlayerInfoDialogFilter).
    public static short PlayerInfoPage;

    // ── One-frame "purge transient sprites" flags (0x1008f3bb..bf) ──
    // Set to 1 together on every world reset (title / new-pilot / the in-flight ship event
    // in TickShipAI); RunMainGameLoop clears all of them back to 0 once per frame. While a
    // flag is non-zero, the next tick of that sprite class terminates its live sprites — a
    // single-frame purge. (NoAsteroidsFlag is the odd member: it rides the same set/clear
    // blocks but its real consumer is the asteroid spawner, not a per-sprite destroy gate.)
    public static byte ClearShotsFlag;          // DAT_1008f3bb — kills all in-flight shots/beams next tick
    public static byte ClearCarriedSpritesFlag; // DAT_1008f3bc — frees deployed/carried sprites (mines/decoys/pods)
    public static byte ClearExplosionsFlag;     // DAT_1008f3bd — frees all active explosion sprites
    public static byte NoAsteroidsFlag;         // DAT_1008f3be — "this system has no asteroids" (set by asteroid Init when count<1)
    public static byte ClearStreaksFlag;        // DAT_1008f3bf — frees all active streak/trail sprites
    public static byte StrictPlay;              // DAT_1008f3c2 (toc+0x6d62) — new-pilot "strict play" checkbox (NewPilotDialog item 2); saved per pilot, gates the harsher on-death path (decompile 16914). Was EvoGlobals.GameFlagF3c2.
    // DAT_1008f3c3 / DAT_1008f3c4 — cleared at world init (f3c3 also on hyperspace arrival).
    // Write-only in the recovered binary: no reader survives in the decompile, so the exact
    // meaning is unknown (the consumer lives in unrecovered code). Honest placeholders.
    public static byte FlagF3c3;                // DAT_1008f3c3
    public static byte FlagF3c4;                // DAT_1008f3c4
    // DAT_1008f3c6 / uRam1008f3c7 — paired UI-suppression gates, always tested together:
    // while EITHER is non-zero the HUD-redraw scheduler, the Player-Info dialog, and the
    // galaxy-map key are all suppressed. Only the =0 clears survive in the recovered binary;
    // the code that sets them lives elsewhere, so the distinct condition each represents is
    // unknown — hence the neutral A/B names.
    public static byte UiSuppressGateA;         // DAT_1008f3c6
    public static byte UiSuppressGateB;         // uRam1008f3c7
    public static byte AiTickFlagCa;            // DAT_1008f3ca (WorldFlags AiTickFlagCa)
    public static byte AiTickFlagCb;            // DAT_1008f3cb (WorldFlags AiTickFlagCb)

    // Cloak-engaged flag (EvoGlobals called this IsCommJammed; the cloaking device was misread as a "comms jammer" pre-rename).
    public static bool IsCloaked;            // DAT_1008f3c5

    // Flash/chatter countdown: 0xffff (=-1) inactive sentinel, ticked toward 0
    // (EvoGlobals called this FlashChatterCountdown). Exposed int / stored short so
    // the -1 sentinel and `<1` / `0<` tests stay correct without per-caller casts.
    private static short _flashChatterCountdown; // UNK_1008f778
    public static int FlashChatterCountdown
    { get => _flashChatterCountdown; set => _flashChatterCountdown = (short)value; }

    // ── Galaxy-map view-centre system-coords (signed short) ──
    public static short MapViewCentreX;          // _DAT_100901fa
    public static short MapViewCentreY;          // _DAT_100901fc

    // ── Player-flight ptr-cell targets (TickShipAI de-unmanage) ──
    // BSS cells that were only reachable through boot-relocated PTR_DAT pointer
    // slots (deref pattern ReadX(ReadInt(slot))).
    // All zero/false at boot; InitGameWorldState sets the -1 sentinels.

    // Per-frame physics time scale ("AngleScaleDivisor"): pos += vel * TimeScale.
    // CopyCpuSpeedScaleToTimeScale copies CpuSpeedScale here every frame.
    public static double TimeScale;             // *0x10080f18 -> 0x100e01f8
    // The game-speed / time-scale master cell. Seeded by the CPU benchmark
    // (RunCpuSpeedBenchmark) on the no-saved-prefs boot path, then OVERRIDDEN by
    // the saved/dialog game-speed pref — this is the SAME cell as
    // PrefsDialogState.GameSpeed (ex **(double**)(toc-0x785c) = *_DAT_10080e04).
    // CopyCpuSpeedScaleToTimeScale copies it into TimeScale (0x100e01f8) every
    // frame, so it drives ship motion directly.
    public static double CpuSpeedScale;         // *0x10080e04 -> 0x100e0200

    // Launch/landing input-lock countdown and the hyperspace self-destruct countdown:
    // TWO SEPARATE shorts. The original Mac cells are 0x10080dd4 and 0x10080f30 — 0x15c
    // (348) bytes apart, NOT adjacent — and the decompile reads/writes/decrements each
    // ALONE as a single short (WorldCountdown via short* _DAT_10080dd4 / *(short*)(toc-0x1e23*4)
    // at 16936/16942/16948/16949/19128; HyperCountdown via PTR_DAT_10080f30 at 19182-19218).
    // There is no combined int access. (An earlier port packed them into a fake
    // "WorldCountdownPair" 32-bit value, which made the launch/landing countdown never
    // reach 0 — frozen control after takeoff/landing and the arrival fade never fired.)
    public static short WorldCountdown;         // *0x10080dd4 -> 0x100dfd58 (toc-0x1e23*4 in TickShipAI)
    public static short HyperCountdown;         // *0x10080f30 -> 0x100dfd5a (reset -1 at world init)

    public static byte CheatShowAll;            // *0x10080f84 -> 0x100e021d — show-all/cheat flag (missions/outfits/abs-shield HUD)
    public static byte AutopilotFlag;           // *0x10080edc -> 0x100e0220 — toggled around hyper/autopilot transitions
    public static byte AiBehaviorFlagA;         // *0x10080f64 -> 0x100e0221 — AI-behaviour flag pair [0]
    public static byte AiBehaviorFlagB;         //                0x100e0222 — AI-behaviour flag pair [1]
    public static short TutorialHintPhase;      // *0x10080f50 -> 0x100e0224 — new-player tutorial hint sequence: -3 welcome/land, -1 hyperspace, 0/1/2 range hints, 0x7fff = inactive
    public static short HudBlinkCountdown;      // *0x10080efc -> 0x100e01a4 — HUD blink-orb countdown
    public static short RespawnCounter;         // *0x10080f4c -> 0x100e01a6 — reset -1 at world init
    public static short CurrentTargetShipId;    // *0x10080b50 -> 0x10100fd0 — hail/pers target ship id (-1 none)

    // Live |velocity| caps the afterburner raises and decay restores (float VALUE
    // cells, not ptr slots).
    public static float PlayerSpeedCapX;        // 0x10090204
    public static float PlayerSpeedCapY;        // 0x10090208
}
