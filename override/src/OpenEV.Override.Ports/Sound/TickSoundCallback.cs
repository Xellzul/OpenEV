using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49109-49161.
// The Sound Manager double-buffer DOUBLEBACK: refills the handed-back buffer
// through the software mixer, fires a kind-3 (mixing tick) notify for every
// voice that has a completion proc, retires voices whose BlocksRemaining hits 0
// (kind-1 notify), then flags the buffer ready. Signature matches
// SndDoubleBackProc; BootSoundSubsystem (B4) wires it into
// SoundMixer.Header.DoubleBackProc — see SoundMixer for the cell mapping.
public static class TickSoundCallback
{
    public static void Run(int channel, SoundMixer.SndDoubleBuffer buffer)
    {
        SoundMixer.CurrentMixData = buffer.Data;
        // ORIGINAL QUIRK (kept): the doubleback RE-SELECTS the fill routine from
        // the stereo flag on EVERY call, ignoring the descriptor that
        // InitSoundMixerState built (SoundMixer.FillProc).
        if (!SoundMixer.StereoEnabled)
            MixSoftwareSounds.Run();
        else
            MixSoftwareSoundsStereo.Run();

        VoiceState[] voices = SoundMixer.Voices;
        SoundPlayRequest scratch = SoundMixer.CallbackScratch;
        for (int i = 0; i < SoundMixer.ActiveVoiceCount; i++)
        {
            VoiceState voice = voices[i];
            int voiceId = voice.Id;
            SoundCompletionProc? completion = voice.CompletionProc;
            if (completion != null)
            {
                scratch.SndHandle = voice.SoundHandle;
                scratch.Priority = voice.Priority;
                scratch.LeftVolume = voice.LeftVolume;
                scratch.RightVolume = voice.RightVolume;
                scratch.RateFixed = 0x10000;
                scratch.CompletionProc = voice.CompletionProc;
                scratch.Refcon = voice.Refcon;
                scratch.Id = voiceId;
                completion(3, voiceId, scratch);
            }
            voice.BlocksRemaining--;
            if (voice.BlocksRemaining == 0)
            {
                RemoveMixerVoiceAt.Run((short)i);
                i--;
                if (completion != null)
                    completion(1, voiceId, scratch);
            }
        }
        buffer.Flags = 1;
    }
}
