using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10042320 (EV Override-11.c lines 27329-27355).
// Tear down the title-music (SndStartFilePlay) channel: pause, stop (the port's
// shim bridges SndStopFilePlay to MacToolbox.FileMusicStopper), optionally
// chirp the sys-beep snd, dispose the channel and zero its slot.
public static class DisposeSoundFileChannel
{
    public static void Run(bool playBeep)
    {
        if (SoundFilePlayState.FileMusicChannel != 0)
        {
            // -211 = channelNotBusy — when the pause itself failed because
            // nothing was playing, the original skips the stop + beep but STILL
            // disposes the channel below.
            short pauseResult = (short)MacToolbox.SndPauseFilePlay(SoundFilePlayState.FileMusicChannel);
            if (pauseResult != -211)
            {
                MacToolbox.SndStopFilePlay(SoundFilePlayState.FileMusicChannel, 1);
                if (playBeep && GamePrefs.IntroMusicEnabled != 0)
                {
                    // GetSysBeepVolume reports the Mac OS alert volume; a stock Mac
                    // defaulted non-muted, so the typed overload returns nonzero and this
                    // dispose-time chirp (BeepSnd, aliased to WeaponHitSnd[0]) plays on
                    // About-box open + game entry — faithful to a normal Mac. Only `> 0`
                    // is tested. (User-approved 2026-07-16; was silently dead when the
                    // query stub returned 0.)
                    int[] sysBeepVolume = new int[4];
                    MacToolbox.GetSysBeepVolume(sysBeepVolume);
                    if (0 < sysBeepVolume[0])
                    {
                        SndPlay.Run(SoundResourceCells.BeepSnd, 10, 128, 128);
                    }
                }
            }
            MacToolbox.SndDisposeChannel(SoundFilePlayState.FileMusicChannel, true);
            SoundFilePlayState.FileMusicChannel = 0;
        }
    }
}
