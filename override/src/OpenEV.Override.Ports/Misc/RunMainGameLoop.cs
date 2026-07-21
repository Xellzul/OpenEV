using System;
using System.Threading;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Systems;

namespace OpenEV.Override.Ports.Misc;

// FUN_100621a8 — the in-game frame loop: scrolls the starfield, ticks the world
// and sprites, processes input, draws the HUD/trails, and caps the frame rate,
// until the quit or game-over flag is set.
// Decompile: EV Override-11.c lines 41164-41296.
public static class RunMainGameLoop
{
    public static void Run()
    {
        short[] sectResultRect = new short[4];   // SectRect out-param
        short[] scrollRect = GlobalState.StarfieldScrollRect;
        short[] clipRect = GlobalState.HudPlayAreaClipRect;

        bool noScroll = BugBits.IsSet(BugBit.NoStarfieldScroll);
        bool noLayout = BugBits.IsSet(BugBit.Bit0xC);   // Bit0xC: disable window-region relayout

        var bootDate = DateTime.Now;
        GWorldPort.SetActivePortScratch();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        GWorldPort.SetActivePortSecondaryGame();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        SetGamePortAndDevice.Run();
        GWorldPort.PaintLetterboxAndBlitInner();
        RefreshStatusPanel.Run();
        // DEVIATION (faithful): RefreshStatusPanel paints the panel art but not the radar
        // dish, which the per-frame scheduler first draws ~15 ticks in (no ASM call here
        // originally). Paint it once now so the radar box isn't a black hole once the host
        // starts flushing the offscreen buffer -- the async host can present a frame the
        // original's synchronous QuickDraw model never could.
        DrawRadarHud.Run(0);
        // Full HUD is in the offscreen buffer now — open the host present gate that
        // RunGameSessionLauncher held closed through the Enter-Ship build gap.
        MacToolbox.GameSceneReady = true;
        DispatchPendingChatter.Run(0);
        RecomputeWorldVisibility.Run();
        Palette.FadeOut(16);
        WorldState.MainLoopLastTick = (int)MacToolbox.TickCount();

        while (!EvoGlobals.QuitRequested && !EvoGlobals.PlayerDead)
        {
            int frameStartTick = (int)MacToolbox.TickCount();
            CopyCpuSpeedScaleToTimeScale.Run();

            // Tick-atomic draw batch: one tick's complete visual output enqueued as a unit
            // so the host's independently-clocked drain can't present the play area
            // mid-erase (ships would flicker out for one host frame).
            MacToolbox.BeginDrawBatch();

            if (!noScroll)
            {
                scrollRect[2] = (short)(scrollRect[0] + 64);   // bottom = top + band height
                scrollRect[1] = GlobalState.WindowBoundsLeft;
                scrollRect[3] = GlobalState.WindowBoundsRight;
                GWorldPort.SetActivePortScratch();
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(scrollRect);
                if (MacToolbox.SectRect(scrollRect, clipRect, sectResultRect))
                {
                    DispatchPendingChatter.Run(1);
                }
                EnqueueDirtyRect.Run(scrollRect);
                SetGamePortAndDevice.Run();
                MacToolbox.OffsetRect(scrollRect, 0, 64);
                if (scrollRect[2] < GlobalState.WindowBoundsTop ||
                    GlobalState.WindowBoundsBottom < scrollRect[0])
                {
                    scrollRect[0] = GlobalState.WindowBoundsTop;
                }
                // Every third frame refresh the streak timestamp; otherwise clear streaks.
                if (WorldState.GameFrameTickCounter % 3 == 0)
                {
                    WorldFlags.StreaksActiveFlag = 1;
                    WorldState.MainLoopLastTick = (int)MacToolbox.TickCount();
                }
                else
                {
                    WorldFlags.StreaksActiveFlag = 0;
                }
                Tick.Run(1);
            }

            if (!noLayout)
            {
                UpdateWindowRegionLayout.Run(GamePrefs.UseQuickdraw == 0);
            }

            DrawLaserTrails.Run();
            DrawHyperspaceLanes.Run();

            MacToolbox.EndDrawBatch();

            // Escape key held, ship still valid, death timer clear, not hyperspacing, world
            // settled -> set the game-over flag.
            if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action26)) != 0
                && GameData.Player.JumpWindupTimer < 1
                && GameData.Player.ShipClass != ShipRecord.EmptyShipClass
                && GameData.Player.DeathTimer <= ShipStatConstants.SpawnZeroDefault
                && GameData.Player.HasWorldSpriteNode != 0
                && WorldState.WorldCountdown < 1)
            {
                EvoGlobals.PlayerDead = true;
            }

            // 2x game speed: while DoubleSpeedActive is latched on (Caps Lock, set below),
            // run an extra world tick + sprite update so the world advances at ~2x. Its own
            // draw batch: TickShipAI can enqueue several screen-targeted draws on rare state
            // transitions that must land together, same as the per-tick batch above.
            if (WorldState.DoubleSpeedActive != 0 && !noScroll)
            {
                MacToolbox.BeginDrawBatch();
                Tick.Run(0);
                TickSpriteSystem.Run();
                MacToolbox.EndDrawBatch();
            }

            if (EvoGlobals.QuitRequested)
            {
                break;
            }

            WorldState.GameFrameTickCounter += 1;
            // Wrap the frame counter at 1024; on wrap, fire the June-30 anniversary chatter.
            if (WorldState.GameFrameTickCounter > 1024)
            {
                WorldState.GameFrameTickCounter = 0;
                // June 30 (Matt Burch's birthday).
                if (bootDate.Day == 30 && bootDate.Month == 6
                    && WorldState.FlashChatterCountdown < 1)
                {
                    // "Happy birthday Matt" = Pascal string @0x10084b8b (GameToc-0x3ad5).
                    EnqueueChatterEvent.Run("Happy birthday Matt", 5, 3, 9, UiColors.Frame, 0, 0);
                }
            }

            WorldState.ClearShotsFlag = 0;
            WorldState.ClearCarriedSpritesFlag = 0;
            WorldState.ClearExplosionsFlag = 0;
            WorldState.ClearStreaksFlag = 0;
            WorldState.NoAsteroidsFlag = 0;

            // 2x-speed toggle key (Caps Lock): while it's down, enable the extra tick above —
            // unless the ship is barely turning AND a boarding chime is still playing, so that
            // cutscene isn't fast-forwarded.
            if (Keymap.TestCachedKeymapBit(MacKeycode.CapsLock) == 0)
            {
                WorldState.DoubleSpeedActive = 0;
            }
            else
            {
                int headingDelta = GameData.Player.Heading - GameData.Player.HeadingPrev;
                if (Math.Abs(headingDelta) < 11
                    && CountMatchingSoundVoices.Run(SoundResourceCells.BoardingChimeSnd) != 0)
                {
                    WorldState.DoubleSpeedActive = 0;
                }
                else
                {
                    WorldState.DoubleSpeedActive = 1;
                }
            }

            // Frame-rate cap: wait until at least one tick (1/60 s) has elapsed since frame
            // start. The decompile busy-spun; here the game runs on its own thread while a
            // separate host thread composites, so yield the core (Thread.Sleep(1)) instead of
            // pegging it — same cap, so game speed is unchanged. Unsigned compare per ASM
            // cmplw — don't simplify.
            int nowTick = (int)MacToolbox.TickCount();
            while (nowTick - 1U < frameStartTick)
            {
                Thread.Sleep(1);
                nowTick = (int)MacToolbox.TickCount();
            }
        }

        Palette.RestoreGWorldPalette(0);
        // Fade the game scene to the screen-fade cell (the original never writes it -> black)
        // on exit; RunGameSessionLauncher's ClearScreenFade then reveals the title after.
        Palette.FadeIn(16, Palette.ScreenFadeCTab);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
    }
}
