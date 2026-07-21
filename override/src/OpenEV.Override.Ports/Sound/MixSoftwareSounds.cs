using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_1007594c (EV Override-11.c lines 49231-49280) — the MONO fill
// routine (MixSoftwareSoundsStereo / FUN_10075830 is the stereo one).
// Mixes SoundMixer.FramesPerBlock 8-bit frames into SoundMixer.CurrentMixData;
// the mono path reads ONLY LeftVolume. NOTE: in the port the PCM output is
// bookkeeping-only (the host engine produces the audio) — but the phase/cursor
// math drives voice lifetime (BlocksRemaining via the doubleback), so it is
// transcribed exactly.
public static class MixSoftwareSounds
{
    public static void Run()
    {
        VoiceState[] voices = SoundMixer.Voices;
        byte[]? output = SoundMixer.CurrentMixData;
        int outOffset = 0;
        for (int framesLeft = SoundMixer.FramesPerBlock; framesLeft != 0; framesLeft--)
        {
            int accum = 0;
            for (int i = 0; i < SoundMixer.ActiveVoiceCount; i++)
            {
                VoiceState voice = voices[i];
                // Only voices below MaxVoices are audible (8 audible vs 16 slots).
                if (i < SoundMixer.MaxVoices)
                {
                    int cur = voice.CurSampleIndex;
                    int sample = SampleAt(voice.Samples, cur);
                    // Phase stalled inside one sample (Cur == Prev): average
                    // s[cur], s[cur+1].
                    if (cur == voice.PrevSampleIndex)
                        sample = (sample + SampleAt(voice.Samples, cur + 1)) >> 1;
                    voice.PrevSampleIndex = cur;
                    accum += (sample * voice.LeftVolume) >> 7;
                }
                // ORIGINAL QUIRK (kept): the phase advance sits OUTSIDE the
                // audibility check — voices at index >= MaxVoices still advance.
                voice.PhaseAccum += (uint)voice.StepFixed >> 8;
                // Byte cursor `base + (Phase>>7 & ~1)` ≡ sample index Phase>>8.
                voice.CurSampleIndex = (int)(voice.PhaseAccum >> 8);
            }
            if (accum > 127) accum = 127;
            if (accum < -127) accum = -127;
            // Decompile stores `(char)accum + -0x80` ≡ accum + 128 mod 256
            // (signed-to-biased 8-bit conversion).
            // PORT ADDITION: bounds guard with no ASM counterpart (the original
            // writes blindly for exactly FramesPerBlock iterations) — should
            // never trip since CurrentMixData is always sized to match.
            if (output != null && outOffset < output.Length)
                output[outOffset] = (byte)(accum + 128);
            outOffset++;
        }
    }

    // Sample fetch with the original's NewPtrClear-slack semantics: the phase
    // cursor may run past the audio tail into the decoded buffer's zeroed slack,
    // and a garbage handle read whatever memory held. The port returns 0 (silence,
    // like the slack) for null buffers / out-of-range indices.
    internal static int SampleAt(short[]? samples, int index)
        => samples != null && (uint)index < (uint)samples.Length ? samples[index] : 0;
}
