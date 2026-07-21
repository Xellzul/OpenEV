using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// FUN_1005484c — EV Override-11.c lines 34582-34593. Defaults the game-prefs scalar band.
// ORIGINAL QUIRK (kept): of the prefs band, only these five cells are defaulted — UseQuickdraw
// (a554) and ProjectileStreaksDisabled (a555) are deliberately NOT touched.
public static class DefaultGamePrefs
{
    public static void Run()
    {
        GamePrefs.IntroMusicEnabled = 1;
        GamePrefs.PrefByte551 = 1;
        GamePrefs.MasterVolume = 3;
        GamePrefs.GfxDetailFlag = 0;
        GamePrefs.QuickTimeMoviesDisabled = 0;
        SystemGlobals.OldOsWarningAcknowledged = true;

        // NO-OP: the original's trailing CStringCopy (FUN_1007615c(&DAT_1009030c, 0x1008210c))
        // is not ported — nothing in the decompile ever reads DAT_1009030c, so the clear is a
        // verified zero-effect no-op (DEV_DEBUG_CODE.md DDC-12).
    }
}
