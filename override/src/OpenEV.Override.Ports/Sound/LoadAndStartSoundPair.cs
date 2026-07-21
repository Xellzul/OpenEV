using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 27361-27402.
//
// Start the looping credits music: load snd `soundId` (falling back to 30001)
// as the audible track and snd 30002 as the swap buffer, then enqueue the
// SwapRequest whose completion (SoundCallback) re-arms itself — the LOOP.
// The buffer Ptrs lived behind the pointer cells *0x10081080 / *0x1008107c
// (BSS 0x100e0e94 / 0x100e0e8c) — now SoundFilePlayState.PairPrimaryHandle /
// .FileMusicSwapHandle (B3 managed).
public static class LoadAndStartSoundPair
{
    public static void Run(short soundId)
    {
        if (GamePrefs.IntroMusicEnabled != 0)
        {
            // ORIGINAL QUIRK (kept): write-only flag, no reader in the binary
            // (see SoundFilePlayState header; in the Mac layout the int write
            // even aliased the boot-bar rect).
            SoundFilePlayState.PairMusicStarted = 1;
            SoundFilePlayState.PairPrimaryHandle = 0;
            SoundFilePlayState.PairPrimaryHandle = LoadSndResource.Run(soundId);
            if (SoundFilePlayState.PairPrimaryHandle == 0)
            {
                SoundFilePlayState.PairPrimaryHandle = LoadSndResource.Run(30001);
            }
            if (SoundFilePlayState.PairPrimaryHandle != 0)
            {
                SoundFilePlayState.FileMusicSwapHandle = LoadSndResource.Run(30002);
                if (SoundFilePlayState.FileMusicSwapHandle == 0)
                {
                    MacToolbox.DisposePtr(SoundFilePlayState.PairPrimaryHandle);
                }
                else
                {
                    // The swap-request record 0x1008a71c..36 is the managed
                    // SoundFilePlayState.SwapRequest. +0x04 Id
                    // and +0x10 Refcon are never written — they stay 0.
                    SoundPlayRequest request = SoundFilePlayState.SwapRequest;
                    request.SndHandle = SoundFilePlayState.PairPrimaryHandle; // the audible track
                    request.RateFixed = 0x10000;                             // Fixed 1.0
                    // The TVector FUN_100425b0: SoundCallback re-arms the loop
                    // on completion.
                    request.CompletionProc = SoundCallback.Completion;
                    request.Priority = 50;
                    request.LeftVolume = 128;
                    request.RightVolume = 128;
                    // Port sound bridge (MacToolbox.Sound.cs): the looping credits
                    // music plays through the host engine directly; soundId is
                    // the audible track, 30002 is the Mac callback's swap buffer.
                    MacToolbox.PairMusicPlayer?.Invoke(soundId, 30002);
                    EnqueueSoundVoice.Run(request);
                }
            }
        }
    }
}
