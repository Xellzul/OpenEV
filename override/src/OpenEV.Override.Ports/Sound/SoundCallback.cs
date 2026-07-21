using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 27437-27453.
// The file-music completion proc: on kind 1 (voice completed) it re-arms the
// swap request with ITSELF as completion — the credits-music loop. See
// SoundFilePlayState for the swap-request record / cell mapping.
public static class SoundCallback
{
    public static void Run(int notifyKind)
    {
        if (notifyKind == 1)
        {
            SoundPlayRequest request = SoundFilePlayState.SwapRequest;
            request.SndHandle = SoundFilePlayState.FileMusicSwapHandle;
            request.RateFixed = 0x10000;
            request.CompletionProc = Completion;
            request.Priority = 50;
            request.LeftVolume = 128;
            request.RightVolume = 128;
            EnqueueSoundVoice.Run(request);
        }
    }

    // SoundCompletionProc adapter: the mixer glue passes (kind, voiceId, scratch);
    // the original FUN_100425b0 reads only the kind.
    public static void Completion(int kind, int voiceId, SoundPlayRequest request) => Run(kind);
}
