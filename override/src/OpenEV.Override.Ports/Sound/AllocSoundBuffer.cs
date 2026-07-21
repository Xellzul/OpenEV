namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075b80 (EV Override-11.c lines 49345-49368).
// Allocates one Sound Manager double buffer (see SoundMixer.SndDoubleBuffer
// for the field layout) and fills its PCM area with 8-bit silence.
// The decompile dropped the return value (it shows void, and the initial
// transcription returned 0 — which made BootSoundSubsystem's null check always trip);
// the original returns the Ptr for the header's buffer slots.
public static class AllocSoundBuffer
{
    public static SoundMixer.SndDoubleBuffer? Run()
    {
        // Managed allocation cannot fail, so the original NewPtr-null branch is
        // unreachable; the nullable return keeps the caller's failure checks
        // shaped like the decompile.
        SoundMixer.SndDoubleBuffer buffer = new SoundMixer.SndDoubleBuffer
        {
            NumFrames = SoundMixer.FramesPerBlock,
            UserInfo = 0,
            Data = new byte[SoundMixer.OutputChannelCount * SoundMixer.FramesPerBlock],
        };
        for (int i = 0; i < buffer.Data.Length; i++)
            buffer.Data[i] = 128; // 8-bit silence (the 0x8080 short fill)
        buffer.Flags = 1;
        return buffer;
    }
}
