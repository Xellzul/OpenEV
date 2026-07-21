using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Title.Model;

namespace OpenEV.Override.Ports.Title;

// Managed-state singleton (not a FUN_xxx port). One-time boot seed: stamps the initial values the
// title's managed globals need before its first frame paints.
public static class TitleMemory
{
    public static void Init(int virtualWidth, int virtualHeight)
    {
        // Managed MacGDevices handle from the host display size, so SystemVersionCheck's gdRect read resolves.
        GameWindowGlobals.ScreenGDeviceHandle = MacToolbox.InitMainScreenDevice(virtualWidth, virtualHeight);

        // EvoGlobals.Reset() doesn't clear this — Init is the reset point for a repeat boot in the same process.
        EvoGlobals.QuitRequested = false;

        // Full virtual screen (DrawPicture PICT-8000 dst), seeded early so the boot splash can paint
        // before InitTitleBackdrop recomputes it.
        TitleScreenGlobals.BackdropRect[0] = 0;
        TitleScreenGlobals.BackdropRect[1] = 0;
        TitleScreenGlobals.BackdropRect[2] = (short)virtualHeight;
        TitleScreenGlobals.BackdropRect[3] = (short)virtualWidth;

        TitleScreenGlobals.ButtonRevealPulse = true;

        // portRect must span the full screen or CreditsScroller's pre-scroll clear is a 0x0 no-op.
        RenderGlobals.BackdropPort.SetPortRectPacked(
            0, (virtualHeight << 16) | (virtualWidth & 0xffff));

        // ActivePortPixmap is the on-screen port's CopyBits key source; every title CopyBits site computes
        // it as ReadInt(ActivePortPixmap)+2, so this sentinel + 2 must equal the host's registered screen
        // pixmap key.
        GlobalState.ActivePortPixmap = MacToolbox.ScreenPixmapSentinel;
        GlobalState.PortTop = 0;
        GlobalState.PortLeft = 0;
        GlobalState.PortBottom = (short)virtualHeight;
        GlobalState.PortRight = (short)virtualWidth;

        // AnimScratchPort is deliberately NOT seeded here: a fake slot address would make the boot's
        // dispose-if-existing branch walk garbage memory. Boot sets it to a real offscreen GWorld later;
        // until then the title consumers fall back to the host ANIM render target via GWorldPort.ScratchPort.

        // HUD palette colours DrawPilotInfo reads via RGBForeColor. The real writer is Palette.InitHudColors,
        // which runs on the boot thread AFTER this Init — but the title can render first, so pre-stamp the
        // SAME authentic values InitHudColors writes (no invented brightening).
        UiColors.Friendly = UiColorConstants.HudColorFriendlySeed;   // label colour
        UiColors.Neutral = UiColorConstants.HudColorNeutralSeed;   // value colour

        // Normally written by the prefs load; the port defers prefs, so pre-set the canonical default (sound ON).
        GamePrefs.IntroMusicEnabled = 1;
    }
}
