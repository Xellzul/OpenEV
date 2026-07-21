using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000a6d0 (EV Override-11.c lines 5585-5626) — the bar's NEWS TERMINAL
// dialog ("ShowGameOverDialog" was an early transcription misname): DLOG 0x3f6 (1014) with
// the PICT 9000 terminal art and the two news lines BuildBarNewsText prepared,
// shown until a click or key. Opened from the bar's item 3.
public static class RunBarNewsDialog
{
    public static void Run()
    {
        // SndPlay takes the snd-handle VALUE (UiSoundBankA[1]), not its cell address.
        SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
        DialogScratch.BarNewsPictHandle = MacToolbox.GetPicture(9000);
        DialogScratch.BarNewsDialogWindow = 0;
        DialogScratch.BarNewsDialogWindow = MacToolbox.GetNewDialog(0x3f6, 0, -1);   // behind = (WindowPtr)-1 → in front of all
        if (DialogScratch.BarNewsDialogWindow != 0)
        {
            NewDialogHook.Run(DialogScratch.BarNewsDialogWindow, 0);
            RecenterWindowIntoPlayArea.Run(DialogScratch.BarNewsDialogWindow);
            MacToolbox.ShowWindow(DialogScratch.BarNewsDialogWindow);
            MacToolbox.SelectWindow(DialogScratch.BarNewsDialogWindow);
            MacToolbox.SetPort(DialogScratch.BarNewsDialogWindow);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            RedrawBarNewsDialog.Run();
            while (MacToolbox.StillDown())
            {
            }
            // BUG FIX (Pass-1 mis-rendering, restores ASM fidelity): decompile passes
            // FUN_1005f964(0x2c)/(0x39) — same literal pair, same fix, as
            // ShowIntroCutsceneAndStartMusic.cs: real keys are Return/Space, not
            // Slash/CapsLock (see Keymap.cs's "Caller keycode space" note for why).
            short keyState;
            do
            {
                if (MacToolbox.Button()) break;
                keyState = (short)Keymap.TestLiveKeymapBit(MacKeycode.Return);
                if (keyState != 0) break;
                keyState = (short)Keymap.TestLiveKeymapBit(MacKeycode.Space);
            } while (keyState == 0);
            MacToolbox.HPurge(DialogScratch.BarNewsPictHandle);
            MacToolbox.ReleaseResource(DialogScratch.BarNewsPictHandle);
            MacToolbox.DisposeDialog(DialogScratch.BarNewsDialogWindow);
            // BUG (OGB-42, kept): raw event-code ordinals used as masks (ORIGINAL_GAME_BUGS.md) —
            // only ever flushes mouseDown/mouseUp, never keyDown/keyUp/autoKey.
            MacToolbox.FlushEvents(EventMask.NullEventMask, 0);
            MacToolbox.FlushEvents(EventMask.MouseDownMask, 0);
            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseDownMask, 0);
            MacToolbox.FlushEvents(EventMask.MouseUpMask, 0);
            MacToolbox.FlushEvents(EventMask.NullEventMask | EventMask.MouseUpMask, 0);
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
            RepaintGameWindow.Run();
        }
    }
}
