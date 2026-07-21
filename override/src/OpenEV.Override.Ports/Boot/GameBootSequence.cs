using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Pilot;
using OpenEV.Override.Ports.Title;
using OpenEV.Override.Ports.EvoMath;

namespace OpenEV.Override.Ports.Boot;

// FUN_10061bb0 (EV Override-11.c 41007-41054) — the pre-title boot orchestrator: the
// Mac runs its subsystem-init calls in strict order, blocks in the title loop
// (FUN_10042f9c), then ExitToShell on quit. The port keeps the exact order + FUN map; the
// Mac-only or error-exit steps are substituted or no-op'd, as noted on each step.
public static class GameBootSequence
{
    // Full Mac order. The port's host calls RunPreTitle() and RunTitleLoop() separately on its
    // own thread (OpenEV.Override.Game.TitleAdapter); Run() mirrors the Mac sequence.
    public static void Run()
    {
        RunPreTitle();
        RunTitleLoop();
    }

    // Steps 46-48: the Mac blocks in the title loop until a menu action, restores the cursor,
    // then GracefulExit's out. TitleMainLoop.Run() is re-entrant (one event per call), so this
    // loops it until quit — the port's stand-in for FUN_10042f9c's own blocking do/while.
    public static void RunTitleLoop()
    {
        // 46. FUN_10042f9c — Mac blocks in the title loop until a menu action; the port ticks
        //    TitleMainLoop from the title background thread instead.
        while (!EvoGlobals.QuitRequested)
        {
            TitleMainLoop.Run();
        }
        // 47. .ShowCursor — restore the cursor.
        MacToolbox.ShowCursor();
        // 48. FUN_1005296c GracefulExit — fade-out + subsystem teardown + ExitToShell (never
        //    returns). The same FUN the in-game Quit runs (Combat.TickShipAI; decompile 17045).
        //    It ends the process here, so .start's trailing PanicExit is faithfully-kept dead
        //    code (decompile 49850, unreachable after the non-returning FUN_10061bb0).
        GracefulExit.Run();
    }

    // Every step up to (not incl.) the blocking title loop, in FUN_10061bb0 order.
    public static void RunPreTitle()
    {
        // --- Phase 1: toolbox + version + trig + palette ---
        // 1. FUN_10051fbc InitToolboxBootSequence — InitGraf/Fonts/Windows/...,
        //    the host's own startup does the equivalent.
        InitToolboxBootSequence.Run();
        // 2. FUN_10054a30 — open the 640×480 full-screen offscreen world + set the
        //    GDevice (shows a "Monitor Tool" error + ExitToShell only on failure).
        InitFullScreenOffscreenWorld.Run();
        // 3. FUN_1005466c SystemVersionCheck — Gestalt OS/QuickTime check; Windows passes.
        SystemVersionCheck.Run();
        // 4. FUN_10058064 InitTrigTables — fills the sin/cos/tan/atan heap tables that
        //    Sin360/Cos360/AccelerateAlongHeading/OffsetByHeading read. NOT a noop: empty
        //    tables → every trig result 0 (zero thrust, zero AI-aim accel).
        EvMath.InitTrigTables();
        // 5. FUN_10052a3c InitHudPaletteRgbTriples — the port stamps the HUD palette in
        //    TitleMemory.Init; running this would overwrite it and re-break pilot-info text.
        Palette.InitHudColors();
        // 6. FUN_10015abc InitPrefsPathAndBugBits — FindFolder(prefs) + 'ëbug' fork bits.
        InitPrefsPathAndBugBits.Run();

        // --- Phase 2: prefs load / OS warning / prefs write ---
        // 7. FUN_10019f88 LoadGamePrefsFromDisk — reads the 'Mp¨Ä' prefs fork (id-0x80 blob)
        //    via the port's File/Resource bridge; on a missing file installs the default prefs +
        //    keymap and runs the CPU benchmark (FUN_10054db0). OWNS the game-speed/time-scale
        //    cell (0x100e0200 = WorldState.CpuSpeedScale) on every path — the benchmark on the
        //    no-prefs fallback, the saved speed on the happy path.
        ApplyDefaultPrefsToMemory.Run();
        // 8. FUN_100600fc ShowOldOsWarningIfNeeded — blocking ModalDialog.
        ShowOldOsWarningIfNeeded.Run();
        // 9. FUN_1001a3b8 WriteGamePrefsToFile — writes the 'Mp¨Ä' id-0x80 fork back, so a
        //    fresh install persists its defaults and later boots take the happy load path.
        WritePrefsToDisk.Run();
        // 10. FUN_10015b4c OpenResourceFiles — OpenResFile ×6.
        OpenPluginResourceFiles.Run();

        // --- Phase 3: fade-in + cursor-hidden flag ---
        // 11. FUN_1005d148(0x10, _DAT_10080e00) — fade to the screen fade-colour. The CLUT-ramp
        //    half is inert on the true-colour renderer; the host ScreenFade is the visible step.
        Palette.FadeIn(16, Palette.ScreenFadeCTab);   // cell 0x10080e00 the original never writes → fade to black
        // 12. mem DAT_1008f3c0 (byte_8F3C0 = r2+0x6D60) = 1 — "cursor hidden by game". Harmless:
        //    TitleIdleTick clears it on the first idle tick; Show/HideCursor are no-op shims.
        WorldState.IsCursorHiddenByGame = true;

        // --- Phase 4: menu bar + game window + cursor hide ---
        // 13. FUN_100433dc InitMenuBar — classic Mac menu bar.
        InitMenuBar.Run();
        // 14. FUN_1005eff0 BuildMenuBarGrayRegion — menu-bar QD region.
        BuildMenuBarGrayRegion.Run();
        // 15. FUN_100600ec — empty stub (nullsub_4).
        junkcode.FUN_100600ec();
        // 16. FUN_1005206c — NewCWindow game window; the port owns its window. The game-window
        //    bounds Rect (_DAT_100811bc / GameWindowGlobals.GameWindowBounds) is already seeded
        //    from the host main device by step 3 (SystemVersionCheck), faithfully matching the
        //    decompile's own gdRect copy (FUN_1005466c 34540-34543) — nothing left to do here.
        InitGalaxyMapWindow.Run();
        // 17. FUN_10052224 ShowGameWindow — Mac window show.
        GWorldPort.ShowGameWindow();
        // 18. .HideCursor — the host owns the cursor.
        MacToolbox.HideCursor();

        // --- Phase 5: sound subsystem + master volume ---
        // 19. FUN_10074af0(8,1,0) BootSoundSubsystem — managed 8-voice mixer init; sets
        //    IsSoundSubsystemBooted (the gate that lets EnqueueSoundVoice play SFX).
        BootSoundSubsystem.Run(8, true, 0);
        // 20-21. FUN_10074e44 GetMasterVolume + store **(toc-0x7410) — the stored value is unread in the port.
        GetMasterVolume.Run();

        // --- Phase 6: offscreen buffers + intro audio + Ambrosia splash ---
        // 22. FUN_100526cc InitGameOffscreenBuffers — Mac GWorlds → the port's RenderTarget2D.
        GWorldPort.InitGameOffscreenBuffers();
        // 23. FUN_1004227c StartSoundFilePlay — starts the looping title music (snd 30000) via
        //    the port's sound bridge. (About-EVÉ later tears it down before the credits.)
        StartSoundFilePlay.Run();
        // 24. FUN_1004165c DrawTransitionSplashPict — draw the Ambrosia logo (PICT 8100)
        //    centred and reveal it (FUN_1005d17c). The logo stays up through steps 25-31 and is
        //    faded back out at step 32. (The Override-ships CREDITS splash is the separate step
        //    34, drawn just before the loading bar so the bar lands on it, not on black.)
        DrawTransitionSplashPict.Run();
        // 25. FUN_10052d68 LoadAllUiSoundEffects — decodes the UI/combat 'snd ' banks into the
        //    SoundResourceCells / CombatSoundCells registries.
        LoadAllUiSoundEffects.Run();
        // 26. FUN_10019880 InitResourceNameStrings — plugin/resource unpack.
        InitResourceNameStrings.Run();

        // --- Phase 7: state buffers + memory check + per-pilot world ---
        // 27. FUN_1005232c OriginalGameStateBufferSizes — NewPtrs the ~360KB game-state buffers
        //    at toc+offset, aliasing the world-state globals (e.g. toc+0x1e98 == _DAT_1008a4f8 =
        //    the 0x24-ship array). Without it every in-game ship access hits low memory. (The
        //    data-seg FP consts it used to stamp now live as C# literals in their readers. The
        //    CPU benchmark is NOT a step here — it lives inside the step-7 prefs loader.)
        OriginalGameStateTotalBytes.Run();
        // 28. FUN_10054734 MemoryCheckOnStartup — Windows has the RAM (raw body ExitToShells if <~9MB).
        MemoryCheckOnStartup.Run();
        // 29. FUN_10052fa4 ResetWorldStateForNewPilot — blanks the large tables, reseeds the
        //    commodity base prices + RandomOddsTable, restarts the clock. Must run BEFORE the
        //    universe loader (step 30).
        ResetWorldStateForNewPilot.Run();
        // 30. FUN_10015e70 LoadSpobAndStellarResources — loads the universe tables from the
        //    'shïp'/'spöb'/'sÿst'/'wëap'/'oütf' resources into _DAT_1008a4fc/500/508/510/518.
        //    Without it the universe is empty.
        LoadSpobAndStellarResources.Run();
        // 31. FUN_100473e8 LoadBarPersonResources — resets the MissionState/ControlBits/BarPerson
        //    tables then loads the 0x200 bar-person resources.
        LoadBarPersonResources.Run();

        // --- Phase 8: fade-out + credits splash + loading bar ---
        // 32. FUN_100416f4 ResetFadeAndClearRegion — palette fade + screen clear; inert on LCD.
        Palette.ResetFadeAndClearRegion();
        // 33. FUN_1005d17c PaletteFadeOut(0x10) — Mac palette fade.
        Palette.FadeOut(16);
        // 34. FUN_100414a0 ApplyCreditsScreenFade — the Override-ships CREDITS splash (PICT 0x83);
        //    the loading bar (step 35) draws ON it. Snap the (revealed-black) composite to black,
        //    draw the splash, fade it in (the port's equivalent of the inert palette transition).
        MacToolbox.ScreenFadeToColor(8, 0, 0, 0);
        ApplyCreditsScreenFade.Run();
        MacToolbox.ScreenFadeToImage(12);
        // 35. FUN_1004173c AnimateBootProgressBar — the loading bar, drawn on the credits splash;
        //    the palette-swap / credits-bar hand-off are guarded while their toc records stay
        //    unwired, so it degrades to the box animation rather than NaN-filling.
        AnimateBootProgressBar.Run();
        // 36. FUN_1001d634 LoadSpriteSheetsAndGWorlds / 37. FUN_100415cc ApplyCreditsScreenFadeOut.
        LoadSpriteSheetsAndGWorlds.Run();
        ApplyCreditsScreenFadeOut.Run();
        // Seed the per-type node-update UPP cells with InvokeNodeUpdateUpp's dispatchable sentinels BEFORE any
        // spawner — every spawner copies these into node+0x1a and InvokeNodeUpdateUpp dispatches on that. The
        // Mac cells are PEF-relocated TVectors valid at CFM load; the port substitutes sentinels, so this must
        // precede SpawnHudOverlayNodes (38) AND SpawnBackgroundNebulaSprites (39), else those boot-spawned
        // nodes route to the no-op and never update/draw (invisible target brackets; black starfield).
        // BuildShipSpriteTable re-seeds at Enter Ship (idempotent).
        SpriteNodeUppCells.SeedDispatchTokens();
        // 38. FUN_10052b38 SpawnHudOverlayNodes — persistent target-bracket/docking-ring/HUD-overlay nodes.
        SpawnHudOverlayNodes.Run();

        // --- Phase 9: title layout + nebulae + UI enable + last-pilot ---
        // 39. FUN_10054240 SpawnBackgroundNebulaSprites — the 26 background star/scenery nodes.
        SpawnBackgroundNebulaSprites.Run();
        // 40. FUN_10053ab0(1) / 41. FUN_10054158(1) — gameplay state init.
        InitGameWorldState.Run(1);
        ResetCommodityPriceLimits.Run(1);
        // 42. mem (toc+0x6d59 = DAT_1008f3b9) = 0 — PilotLoaded.
        WorldState.PilotLoaded = false;
        // 43. mem (toc+0x6d58 = DAT_1008f3b8) = 0 — write-only flag, never read (vestigial; see WorldState).
        WorldState.UnreadBootFlag6d58 = 0;
        // 44. FUN_10042e50 InitTitleScreenLayout = InitTitleBackdrop — loads PICT 8000, the
        //    two region handles, and InnerArenaRect; without it the menu buttons land at top-left.
        InitTitleBackdrop.Run();
        // 45. FUN_1001b56c — LAST-PILOT AUTO-LOAD (NOT a credits sequencer): draws the loading
        //    screen, probes the "Last Pilot" pointer and resumes it via FUN_1001b758. Belongs at
        //    boot (decompile 41051); do NOT move it to the game loop.
        AutoLoadLastPilotAtBoot.Run();
    }
}
