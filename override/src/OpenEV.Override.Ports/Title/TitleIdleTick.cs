using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_10044130 (EV Override-11.c lines 28243-28297): the title screen's per-frame idle
// tick. Shows the cursor if the game had force-hidden it, auto-hides/restores the Mac menu bar
// as the mouse crosses the top strip of the window, polls the 3-key cheat chord, pumps the
// hover-orb animation, and restores the colour depth while in the foreground.
public static class TitleIdleTick
{
    public static void Run()
    {
        if (WorldState.IsCursorHiddenByGame)
        {
            MacToolbox.ShowCursor();
            WorldState.IsCursorHiddenByGame = false;
        }

        // Probe Rect = the top 20-pixel strip of the render-context port rect:
        // {top, left, top+20, right}.
        short[] probeRect = new short[4];
        MacToolbox.SetRect(probeRect, GlobalState.PortLeft, GlobalState.PortTop,
            GlobalState.PortRight, (short)(GlobalState.PortTop + 20));

        int mousePoint = MacToolbox.GetMouse();
        bool inRect = MacToolbox.PtInRect(mousePoint, probeRect);

        if (!inRect)
        {
            // Mouse left the strip: hide the menu bar (once) while in the foreground.
            if (WorldState.MenuBarHidden == 0 && !TitleScreenGlobals.InBackground)
            {
                MacToolbox.InvalRect(probeRect);
                HideMacMenuBar.Run();
                WorldState.MenuBarHidden = 1;
            }
        }
        else if (WorldState.MenuBarHidden != 0)
        {
            // Mouse re-entered the strip: restore the menu bar.
            RestoreMacMenuBar.Run();
            WorldState.MenuBarHidden = 0;
        }

        // Cheat chord is S + O + L ("SOL" — the EV series' home system), not V/D5/N.
        // TestLiveKeymapBit takes a REAL keycode, not the decompile's raw
        // FUN_1005f964 literal (9/0x17/0x2d) — see its "Caller keycode space" note.
        if (Keymap.TestLiveKeymapBit(MacKeycode.S) != 0 &&
            Keymap.TestLiveKeymapBit(MacKeycode.O) != 0 &&
            Keymap.TestLiveKeymapBit(MacKeycode.L) != 0)
        {
            if (!TitleScreenGlobals.CheatSoundPlayed)
            {
                SndPlay.Run(CombatSoundCells.UiSoundBankA[0], 1, 128, 128);
            }
            TitleScreenGlobals.CheatSoundPlayed = true;
        }

        HoverOrbDrawErase.Run();

        if (!TitleScreenGlobals.InBackground)
        {
            ValidateAndRestoreDepth.Run();
        }
    }
}
