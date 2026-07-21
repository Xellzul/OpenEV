using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 15305-15348.
// Plays one random sfx from the queued ambient bank (0..3, set through the
// 0x10080ee4 pointer by SetActiveChatterSpeaker) once the previous one finishes.
public static class TickAmbientSoundChannel
{
    public static void Run()
    {
        // The queued-bank short behind *PTR_DAT_10080ee4 (toc[-0x1ddf]) is the
        // managed SoundFilePlayState.QueuedAmbientBank now.
        if (SoundResourceCells.DynamicSoundBuffer != 0)
        {
            var channelState = (short)CountMatchingSoundVoices.Run(SoundResourceCells.DynamicSoundBuffer);
            if (channelState == 0)
            {
                MacToolbox.DisposePtr(SoundResourceCells.DynamicSoundBuffer);
                SoundResourceCells.DynamicSoundBuffer = 0;
            }
        }
        // Decompile note: the TOC-relative base used below (for the bank-count
        // table) gets reassigned to a fresh local after the dispose branch above —
        // a decompile TOC-reload rendering artifact, not a real second pointer; both
        // branches read the same GameToc-relative cells.
        if (SoundFilePlayState.QueuedAmbientBank != -1)
        {
            if (SoundResourceCells.DynamicSoundBuffer == 0)
            {
                // The per-bank loaded-sound count is GameToc+0x710a (0x1008f76a),
                // now the managed SoundResourceCells.UiSfxBankLoadedCount[bank] table.
                if (-1 < SoundFilePlayState.QueuedAmbientBank &&
                    SoundFilePlayState.QueuedAmbientBank < SoundResourceCells.UiSfxBankLoadedCount.Length &&
                    0 < SoundResourceCells.UiSfxBankLoadedCount[SoundFilePlayState.QueuedAmbientBank])
                {
                    int soundIndex = (int)SeedEvoRng.Run(SoundResourceCells.UiSfxBankLoadedCount[SoundFilePlayState.QueuedAmbientBank]);
                    SoundResourceCells.DynamicSoundBuffer = LoadSndResource.Run(SoundFilePlayState.QueuedAmbientBank * 10 + soundIndex + 700);
                    SndPlay.Run(SoundResourceCells.DynamicSoundBuffer, 15, 128, 128);
                }
                SoundFilePlayState.QueuedAmbientBank = -1;
            }
            else
            {
                var channelState = (short)CountMatchingSoundVoices.Run(SoundResourceCells.DynamicSoundBuffer);
                if (channelState == 0)
                {
                    MacToolbox.DisposePtr(SoundResourceCells.DynamicSoundBuffer);
                    SoundResourceCells.DynamicSoundBuffer = 0;
                }
                else
                {
                    FlushMixQueueEntries.Run(SoundResourceCells.DynamicSoundBuffer);
                }
            }
        }
        return;
    }
}
