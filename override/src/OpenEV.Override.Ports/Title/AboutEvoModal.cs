using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10042628 (EV Override-11.c lines 27459-27524). About EVÉ menu
// action: releases the cached PICT 8000 backdrop, tests real ADB
// Option/Command/Shift to pick which STR# (20001/20002/20003) the credits
// scroller shows, checks a hidden F+Control+Command chord for the real
// credits reel, then redraws the title screen.
public static class AboutEvoModal
{
    public static void Run()
    {
        DisposeSoundFileChannel.Run(true);

        // Release the cached PICT 8000 backdrop so the credits/registration
        // overlay can reclaim its memory (re-loaded by the next repaint).
        if (TitleScreenGlobals.Pict8000Handle != 0)
        {
            MacToolbox.HPurge(TitleScreenGlobals.Pict8000Handle);
            MacToolbox.ReleaseResource(TitleScreenGlobals.Pict8000Handle);
            TitleScreenGlobals.Pict8000Handle = 0;
        }

        // BUG FIX (Pass-1 mis-rendering, restores ASM fidelity): these are the real
        // ADB keys per Keymap.TestLiveKeymapBit's "Caller keycode space" note — don't
        // revert to a MacKeycode matching the decompile's raw hex literal's own name.
        var keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.Option);
        if (keyHeld == 0)
        {
            keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.Command);
            if (keyHeld == 0)
            {
                keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.Shift);
                // Speech easter egg: Shift held, not yet spoken this run
                // (SpeechEasterEggFlag re-armed by InitTitleBackdrop). The
                // decompile's ppuVar4/local_3c split across branches is a
                // decompiler rendering of the post-call TOC-register (r2) reload,
                // not a real address change; both paths read the same flag byte.
                if (keyHeld != 0 && TitleScreenGlobals.SpeechEasterEggFlag == 0)
                {
                    DetectSpeechSupport.Run();
                    SpeakText.Run("Greetings professor Follkinn. Would you like to play a game?", 1);
                    TitleScreenGlobals.SpeechEasterEggFlag = 1;
                    return;
                }
                LoadAndStartSoundPair.Run(30001);   // snd 30001 (0x7531)
                CreditsScroller.Run(20001, fadeOutToBlack: true);   // default goodbye dialog
                Palette.FadeIn(16, Palette.ScreenFadeCTab);   // revealed by DrawClosedButtons.FadeOut(16)
                StopAndDisposeSoundPair.Run();
            }
            else
            {
                CreditsScroller.Run(20003, fadeOutToBlack: true);   // already-registered dialog
                Palette.FadeIn(16, Palette.ScreenFadeCTab);
            }
        }
        else
        {
            LoadAndStartSoundPair.Run(30001);   // snd 30001 (0x7531)
            var regDialogConfirmed = (byte)CreditsScroller.Run(20002, fadeOutToBlack: true);   // registration dialog
            Palette.FadeIn(16, Palette.ScreenFadeCTab);
            StopAndDisposeSoundPair.Run();
            // Konami follow-up: dialog confirmed AND F+Control+Command still
            // held → play the secret credits-easter-egg chime.
            if (regDialogConfirmed != 0 &&
                (keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.F)) != 0 &&
                (keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.Control)) != 0 &&
                (keyHeld = (short)Keymap.TestLiveKeymapBit(MacKeycode.Command)) != 0)
            {
                PlayCreditsEasterEggChime.Run();
            }
        }

        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(GlobalState.PortRect);
        MacToolbox.InvalRect(GlobalState.PortRect);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);   // flush all but disk-inserted
        DrawPilotInfo.Run(0);
        DrawClosedButtons.Run();
        AnimateRowReveal.Run();
        DrawTitleSecondaryPict.Run();
        SetGamePortAndDevice.Run();
        MacToolbox.InvalRect(GlobalState.PortRect);
    }
}
