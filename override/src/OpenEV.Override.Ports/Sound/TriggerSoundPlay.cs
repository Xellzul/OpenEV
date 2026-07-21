using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 48811-48833.
// Builds a default play request on the stack — rate 1.0, no completion, the
// single volume on BOTH sides — and enqueues it on the software mixer.
public static class TriggerSoundPlay
{
    public static int Run(int sndHandle, short priority, short volume)
    {
        // The decompile typed FUN_10074ec0 void, but it ends on the FUN_10074f10 call —
        // the voice id rides back in r3, so the managed port returns it.
        SoundPlayRequest request = new SoundPlayRequest
        {
            SndHandle = sndHandle,
            Id = 0,
            RateFixed = 0x10000, // Fixed 1.0
            CompletionProc = null,
            Refcon = 0,
            Priority = (ushort)priority,
            LeftVolume = (ushort)volume,
            RightVolume = (ushort)volume,
        };
        return EnqueueSoundVoice.Run(request);
    }
}
