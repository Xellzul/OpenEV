namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 40191-40207.
// Public one-shot entry point: averages the left/right volumes and forwards to
// TriggerSoundPlay (which puts the average on both sides of the request). The
// single host-bridge call site lives inside EnqueueSoundVoice, behind the
// real gate/slot logic.
public static class SndPlay
{
    public static void Run(int sndHandle, short priority, short leftVolume, short rightVolume)
    {
        // Decompile's sign-corrected >>1 of the sum ≡ truncating /2.
        TriggerSoundPlay.Run(sndHandle, priority, (short)((leftVolume + rightVolume) / 2));
    }
}
