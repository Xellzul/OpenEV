using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 27308-27328.
//
// Boot step 22: start the looping title-music stream (snd 30000). The original
// allocated a Mac SndChannel through the pointer cell *0x10081088 (BSS
// 0x100e0e88 — now SoundFilePlayState.FileMusicChannel, B3 managed) and called
// SndStartFilePlay(chan, 0, 30000, 0xfffe, 0,0,0, async 1), which the port's shim
// bridges to MacToolbox.FileMusicPlayer. No flags are set here — the decompile
// touches only the channel cell.
public static class StartSoundFilePlay
{
    public static void Run()
    {
        if (GamePrefs.IntroMusicEnabled != 0)
        {
            SoundFilePlayState.FileMusicChannel = 0;
            // SndNewChannel(piVar1, sampledSynth 5, initMono 0x80, no userProc):
            // the port's shim hands back the 'Schn' sentinel and reports noErr.
            short newChannelErr = MacToolbox.SndNewChannel(
                out SoundFilePlayState.FileMusicChannel, 5, 0x80, 0);
            if (SoundFilePlayState.FileMusicChannel != 0 && newChannelErr == 0)
            {
                MacToolbox.SndStartFilePlay(SoundFilePlayState.FileMusicChannel,
                    0, 30000, 0xfffe, 0, 0, 0, 1);
            }
        }
    }
}
