using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10074d48 (EV Override-11.c lines 48737-48758).
// Tears the booted mixer back down: flush every voice, restore the hardware
// volume the boot saved, dispose the mixer channel + both double buffers,
// clear the booted flag.
public static class TeardownSoundSubsystem
{
    public static void Run()
    {
        // The original reads the SndDoubleBufferHeader pointer BEFORE the booted
        // gate check; with the record managed that ordering is moot.
        if (EvoGlobals.IsSoundSubsystemBooted)
        {
            FlushMixQueueEntries.Run(0);
            // The hw-volume branch in the decompile only re-points the (same) TOC base, so
            // the disposed channel is the same cell regardless — collapsed.
            if (SoundMixer.UseHardwareVolume)
                MacToolbox.SetDefaultOutputVolume(SoundMixer.SavedHardwareVolume);
            MacToolbox.SndDisposeChannel(SoundMixer.MixerChannelHandle, true);
            SoundMixer.StopPump();   // Port pump analog: SndDisposeChannel ends double-buffer playback
            SoundMixer.Header.Buffers[0] = null;
            SoundMixer.Header.Buffers[1] = null;
            EvoGlobals.IsSoundSubsystemBooted = false;
        }
    }
}
