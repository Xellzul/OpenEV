using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 15349-15370.
public static class StopAmbientSoundChannel
{
    public static void Run()
    {
        if (SoundResourceCells.DynamicSoundBuffer != 0)
        {
            FlushMixQueueEntries.Run(SoundResourceCells.DynamicSoundBuffer);
            CountMatchingSoundVoices.Run(SoundResourceCells.DynamicSoundBuffer);
            MacToolbox.DisposePtr(SoundResourceCells.DynamicSoundBuffer);
            SoundResourceCells.DynamicSoundBuffer = 0;
        }
        // Decompile `*(short *)ppuVar1[-0x1ddf] = -1` writes -1 THROUGH the
        // queued-ambient-bank pointer (toc[-0x1ddf] = cell 0x10080ee4) — the
        // managed SoundFilePlayState.QueuedAmbientBank now. The
        // decompile's post-dispose base swap to a fresh local is a
        // TOC-reload rendering artifact, not a real second pointer — both
        // paths resolve to the same cell.
        SoundFilePlayState.QueuedAmbientBank = -1;
        return;
    }
}
