namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49286-49339.
// Resets the software-mixer state for boot: builds the play-command block —
// only the routine-descriptor slot ever mattered, modelled as SoundMixer.FillProc
// — then seeds the voice-id counter, zeroes the count, clears all 16 voices (the
// shared 13-field zero list), and latches the hardware-volume capability.
public static class InitSoundMixerState
{
    public static void Run()
    {
        // NewRoutineDescriptor picks the mono fill routine (FUN_1007594c) when the
        // stereo byte is clear, else the stereo one (FUN_10075830). NOTE the
        // doubleback (TickSoundCallback) re-selects per call and ignores this —
        // original quirk, kept there.
        if (!SoundMixer.StereoEnabled)
            SoundMixer.FillProc = MixSoftwareSounds.Run;
        else
            SoundMixer.FillProc = MixSoftwareSoundsStereo.Run;
        SoundMixer.NextVoiceId = 1;
        SoundMixer.ActiveVoiceCount = 0;
        // The shared 13-field zero list (see VoiceState.Clear).
        foreach (var voice in SoundMixer.Voices)
            voice.Clear();
        SoundMixer.UseHardwareVolume = IsSoundManagerV3Plus.Run();
    }
}
