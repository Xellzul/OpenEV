using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075598 (EV Override-11.c lines 49075-49103).
// Decrements the voice count, compacts the voice array down over the removed
// index, and runs the 13-field zero list on the freed tail slot.
public static class RemoveMixerVoiceAt
{
    public static void Run(short voiceIndex)
    {
        if (EvoGlobals.IsSoundSubsystemBooted)
        {
            VoiceState[] voices = SoundMixer.Voices;
            SoundMixer.ActiveVoiceCount--;
            int newCount = SoundMixer.ActiveVoiceCount;
            // BlockMoveData compaction DOWN: records voiceIndex+1..newCount shift
            // to voiceIndex..newCount-1.
            for (int i = voiceIndex; i < newCount; i++)
                voices[i].CopyFrom(voices[i + 1]);
            voices[newCount].Clear();   // the 13-field zero list — see VoiceState.Clear
        }
    }
}
