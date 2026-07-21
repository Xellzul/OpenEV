using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_100753e4 (EV Override-11.c lines 48995-49011).
// Counts the mixer voices matching the query — voice id, snd handle, or 0 =
// count all active voices.
public static class CountMatchingSoundVoices
{
    public static int Run(int matchQuery)
    {
        // Double-buffer pump (1 of exactly 2 pump sites — the other is
        // TickSoundSubsystem): title/dialog wait loops spin polling this
        // function, so the Mac interrupt-time doubleback cadence is replayed
        // from here. See SoundMixer.PumpDoubleBuffer.
        SoundMixer.PumpDoubleBuffer();

        int matchCount = 0;
        if (EvoGlobals.IsSoundSubsystemBooted)
        {
            VoiceState[] voices = SoundMixer.Voices;
            for (short i = 0; i < SoundMixer.ActiveVoiceCount; i++)
            {
                if (matchQuery == voices[i].Id ||
                    voices[i].SoundHandle == matchQuery ||
                    matchQuery == 0)
                {
                    matchCount++;
                }
            }
        }
        return matchCount;
    }
}
