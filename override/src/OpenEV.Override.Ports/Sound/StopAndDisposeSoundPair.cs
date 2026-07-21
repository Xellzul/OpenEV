using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_100424f8 (EV Override-11.c lines 27403-27431).
// Stop the looping credits music: flush the pair's mixer voices by HANDLE
// VALUE, then DisposePtr the buffers.
public static class StopAndDisposeSoundPair
{
    public static void Run()
    {
        if (GamePrefs.IntroMusicEnabled != 0)
        {
            // Port sound bridge: stop the looping credits music started by
            // LoadAndStartSoundPair.
            MacToolbox.PairMusicStopper?.Invoke();
            // Flush by handle VALUE. ORIGINAL QUIRK (kept): the swap handle is
            // flushed TWICE, the primary once.
            FlushMixQueueEntries.Run(SoundFilePlayState.PairPrimaryHandle);
            FlushMixQueueEntries.Run(SoundFilePlayState.FileMusicSwapHandle);
            FlushMixQueueEntries.Run(SoundFilePlayState.FileMusicSwapHandle);
            if (SoundFilePlayState.PairPrimaryHandle != 0)
            {
                MacToolbox.DisposePtr(SoundFilePlayState.PairPrimaryHandle);
            }
            if (SoundFilePlayState.FileMusicSwapHandle != 0)
            {
                MacToolbox.DisposePtr(SoundFilePlayState.FileMusicSwapHandle);
            }
            // ORIGINAL QUIRK (kept): nothing in the binary ever WRITES the
            // spare-buffer cell, so this dispose branch is dead
            // (FileMusicSpareBuffer stays 0).
            if (SoundFilePlayState.FileMusicSpareBuffer != 0)
            {
                MacToolbox.DisposePtr(SoundFilePlayState.FileMusicSpareBuffer);
            }
            // ORIGINAL QUIRK (kept): the function zeroes NOTHING — the
            // disposed handles stay in the fields until the next
            // LoadAndStartSoundPair overwrites them.
        }
    }
}
