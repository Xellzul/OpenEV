using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Title.Model;
using System;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10042f9c (EV Override-11.c lines 27747-27861), disassembly sub_42F9C — the title
// screen's main event loop. The original runs one loop until the quit flag; the port runs
// on a background title thread as a re-entrant Run(): RunSetupOnce paints the screen once,
// then each Run() call advances one event and returns, with V2TitleAdapter re-entering
// until quit.
public static class TitleMainLoop
{
    private static bool _setupDone;

    // EventRecord.message bits for an osEvt (Inside Macintosh: Toolbox Essentials).
    [Flags]
    private enum OsEvtMessageFlags
    {
        Resume = 0x00000001,  // 1 = resume (foreground), 0 = suspend
        SuspendResumeMessage = 0x01000000,  // set when this osEvt is a suspend/resume transition
    }

    public static void Run()
    {
        if (!_setupDone)
        {
            _setupDone = true;
            RunSetupOnce();
        }

        // Event-wait spin (27844-27856): loop until one event arrives, then dispatch it.
        // The quit + pilot-info-dirty bytes are re-read every iteration — don't hoist
        // them out of the loop.
        ushort eventCode;
        do
        {
            while (true)
            {
                if (EvoGlobals.QuitRequested)
                {
                    _setupDone = false;   // re-run setup on re-entry
                    return;
                }
                if (TitleScreenGlobals.PilotInfoDirty)
                {
                    // FAITHFULLY DEAD branch: the Mac gating byte is BSS-zero and never
                    // written anywhere in the binary (see TitleScreenGlobals.PilotInfoDirty),
                    // so this never fires — the panel repaints via updateEvt below instead.
                    DrawPilotInfo.Run(1);
                }
                bool gotEvent = MacToolbox.WaitNextEvent(0xffff, out eventCode, 60, 0);   // everyEvent, 60-tick sleep budget
                if (gotEvent || eventCode != 0) break;
                TitleIdleTick.Run();
            }
        } while (eventCode > (ushort)MacEventType.OsEvt);   // kHighLevelEvent etc. aren't dispatched — re-loop

        DispatchEvent(eventCode);
    }

    // Event jumptable (ASM jpt_4330C, indexed by EventRecord 'what'; the decompile dropped the
    // table). updateEvt is LIVE — WaitNextEvent synthesizes it from the InvalRect
    // update-region model, and it's what paints the pilot-info panel (the Mac path).
    // diskEvt/osEvt can't fire in the port but are ported for fidelity.
    private static void DispatchEvent(ushort eventCode)
    {
        switch ((MacEventType)eventCode)
        {
            case MacEventType.MouseDown:
                // DEVIATION (faithful): the ASM passes the live EventRecord pointer; the
                // port passes a synthesized 'where'. sub_43458 consumes ONLY the 'where' field (its
                // 'when'/'modifiers' loads are dead, 'what'/'message' untouched). Bug fix: this
                // used to read live MacToolbox.FrameMouse (wherever the cursor is NOW, at dispatch
                // time) instead of MacToolbox.FrameEventWhere (the point WaitNextEvent latched at
                // the instant it detected the click) — matching decompile FUN_10043458's
                // `*(param_1 + 10)` read of the (interrupt-time-stamped) EventRecord.where. A stray
                // or delayed edge would hit-test whatever button the mouse happened to be hovering
                // instead of where the click actually occurred.
                DispatchTitleEvent.Run(PackPoint(MacToolbox.FrameEventWhere));
                break;

            case MacEventType.KeyDown:
            case MacEventType.AutoKey:
                {
                    // DEVIATION (faithful): the ASM passes EventRecord.modifiers (word_E0F72)
                    // as the 3rd arg; the port passes 0. sub_43900 forwards modifiers into a synthetic
                    // EventRecord whose modifiers field sub_43458 loads but never reads (dead) — so the
                    // value is unobservable either way.
                    int message = MacToolbox.FrameEventMessage;
                    TitleKeyToButton.Run((byte)(message & 0xff), message, 0);
                    break;
                }

            case MacEventType.UpdateEvt:
                SetGamePortAndDevice.Run();
                DrawPilotInfo.Run(1);
                break;

            case MacEventType.DiskEvt:
                {
                    // Bad-disk mount: pop the Disk Init dialog at (100,100) when the message
                    // high word (drive error) is non-zero. NO-OP: DI* are no-op stubs in the port —
                    // diskEvt can't actually fire (no removable-media host), so this is unreachable
                    // today, but the call sequence is ported for fidelity.
                    int message = MacToolbox.FrameEventMessage;
                    if ((short)(message >> 16) != 0)
                    {
                        MacToolbox.DILoad();
                        MacToolbox.DIBadMount((100 << 16) | 100, message);
                        MacToolbox.DIUnload();
                    }
                    break;
                }

            case MacEventType.OsEvt:
                {
                    // Suspend/resume: SuspendResumeMessage marks the transition; Resume says
                    // which way (set = resume/foreground, clear = suspend).
                    int message = MacToolbox.FrameEventMessage;
                    if (((OsEvtMessageFlags)message & OsEvtMessageFlags.SuspendResumeMessage) != 0)
                    {
                        if (((OsEvtMessageFlags)message & OsEvtMessageFlags.Resume) != 0)
                        {
                            GWorldPort.SetGameWindowForeground(true);
                        }
                        else
                        {
                            GWorldPort.SetGameWindowForeground(false);
                            MacToolbox.InitCursor();
                        }
                    }
                    break;
                }
        }
    }

    // One-time title setup (27775-27843): fade to black, (re)spawn the loaded pilot's
    // world, repaint the menu-bar strip, install the palette, draw the title art + closed
    // buttons, run the shareware check, reveal the button rows, drain the held mouse press.
    private static void RunSetupOnce()
    {
        // sub_5D148(0x10, …) — fade to the never-seeded screen-fade cell (→ black; the
        // composite FadeLevel ramp is the visible step). Revealed by the button-row draw.
        Palette.FadeIn(16, Palette.ScreenFadeCTab);

        if (WorldState.PilotLoaded)
        {
            short homeSyst = GameData.Player.CurrentSystem;
            CleanupSystNpcs.Run(0);
            RunFleetSpawner.Run(homeSyst);
            Asteroids.Init();
            MarkGalaxyMapClustersForSyst.Run(homeSyst);
            RecomputeWorldVisibility.Run();
            GalaxyMapState.TradeKeyLock = 0;
            WorldState.NoAsteroidsFlag = 1;
            WorldState.ClearStreaksFlag = 1;
            WorldState.ClearExplosionsFlag = 1;
            WorldState.ClearCarriedSpritesFlag = 1;
            WorldState.ClearShotsFlag = 1;
        }

        RepaintMenuBarStripIfClose();

        InitShareWareRegistrationSession.Run(1, 2);
        // The ASM double-dereferences this slot (slot → ptr → CTabHandle) — don't collapse
        // to the intermediate pointer.
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
        DrawTitleSecondaryPict.Run();
        // The ASM clears this byte through the TOC pointer — don't clobber the pointer
        // slot itself.
        TitleScreenGlobals.InBackground = false;
        if (WorldState.IsCursorHiddenByGame)
        {
            WorldState.IsCursorHiddenByGame = false;
            MacToolbox.ShowCursor();
        }

        // snd 600 = the button-click chime, installed in sound channel 1.
        SndChannelTable.SetHandle(1, LoadSndResource.Run(600));

        // Title PICT dst rect = a copy of the backdrop rect; the final InvalRect reuses it.
        short[] titleRect = (short[])TitleScreenGlobals.BackdropRect.Clone();
        // Title art draws into the backdrop GWorld, then blits (not directly to screen).
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.DrawPicture(TitleScreenGlobals.Pict8000Handle, titleRect);
        SetGamePortAndDevice.Run();
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);   // flush all but disk-inserted
        Palette.InstallScreenPalette(Palette.ScreenPaletteCTab, 0);
        DrawClosedButtons.Run();

        // Shareware check (27823-27827): seed the match byte from CheckShareWareRegistrationMatch
        // (loads the on-disk record, recomputes the expected code, compares — real, not stubbed;
        // the register app writes the same record file). If it's 0 (no match / no session open)
        // show the nag with forceShow=true (bypassing the 7-day gate), then re-seed. The nag's
        // ModalDialog blocks the title thread until dismissed — faithful to the original.
        CheckShareWareRegistrationMatch.Run(out byte regMatch);
        WorldState.SharewareRegisteredMatch = regMatch;
        if (regMatch == 0)
        {
            ShowSharewareNagDialog.Run(true);
        }
        CheckShareWareRegistrationMatch.Run(out regMatch);
        WorldState.SharewareRegisteredMatch = regMatch;

        GetInstallHours.Run(out uint installHours);
        WorldState.InstallDays = InstallHoursToDays(installHours);

        AnimateRowReveal.Run();
        DrawTitleSecondaryPict.Run();
        InitTitleRects.Run();
        SetGamePortAndDevice.Run();
        MacToolbox.InvalRect(titleRect);

        // Drain a held mouse press (ASM loc_4327C: do { StillDown() } while still down). The CPU
        // pacing that stops this spin from flooding the title thread lives inside StillDown
        // (PollMouseButton sleeps ~8 ms while the button is down), same as every other Track*
        // drain loop — so no cap is needed here.
        while (MacToolbox.StillDown()) { }
        // BUG (OGB-42): raw event-code ordinal used as mask (ORIGINAL_GAME_BUGS.md) — flushes
        // queued mouseDown/null after the drained click, but never keyDown/keyUp/autoKey.
        MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
    }

    // Pack a QuickDraw Point as (v << 16) | h — the 'where' Point DispatchTitleEvent reads.
    private static int PackPoint(MPoint where) => (where.V & 0xffff) << 16 | where.H & 0xffff;

    // Install-hours → "install days" (27828-27834): divide by 5; the PPC int→double
    // magic-bias idiom already collapses to a plain (double)(int) cast.
    private static short InstallHoursToDays(uint installHours)
        => (short)(int)((double)(int)installHours / 5.0);

    // Repaint the menu-bar strip ({PortTop-20, left, bottom, right}) when the play window
    // top sits within 20 px of the screen top — i.e. paint over where the 20 px menu bar
    // would be (decompile loc_43060).
    private static void RepaintMenuBarStripIfClose()
    {
        MacToolbox.GetMainDeviceBounds(out short gdRectTop, out _, out _, out _);   // main device gdRect.top
        if (Math.Abs(GlobalState.PortTop - gdRectTop) >= 21) return;

        SetGamePortAndDevice.Run();
        short[] menuRect = new short[4];
        MacToolbox.SetRect(menuRect, GlobalState.PortLeft,
                           (short)(GlobalState.PortTop - 20),
                           GlobalState.PortRight, GlobalState.PortBottom);
        // NO-OP: ActivePortPixmap is the port's screen-pixmap SENTINEL (no real CGrafPort), so the
        // clip/vis-rgn accessors return 0 and these RectRgn calls no-op. Harmless today — the
        // port has no menu bar and the software renderer models no clip regions, and the
        // PaintRect below is self-bounded to menuRect. DEFERRED: if QuickDraw regions / a real
        // menu bar are ever added, restore this clip-to-strip (else later drawing to the window
        // won't be constrained to the menu-bar strip).
        int activePort = GlobalState.ActivePortPixmap;
        MacToolbox.RectRgn(MacToolbox.GetPortClipRgn(activePort), menuRect);
        MacToolbox.RectRgn(MacToolbox.GetPortVisRgn(activePort), menuRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(menuRect);
        MacToolbox.InvalRect(menuRect);
    }
}
