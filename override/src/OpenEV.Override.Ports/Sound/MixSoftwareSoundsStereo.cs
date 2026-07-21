using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075830 (EV Override-11.c lines 49167-49225) — the STEREO fill
// routine (MixSoftwareSounds / FUN_1007594c is the mono one).
// Mixes SoundMixer.FramesPerBlock 16-bit L|R frames into SoundMixer.CurrentMixData.
// The decompile's store is `(R+0x80) | ((L+0x80) * 0x100)` through a big-endian
// 16-bit pointer, i.e. byte 0 = L+0x80, byte 1 = R+0x80 per frame.
// Bookkeeping-only PCM in the port (see MixSoftwareSounds) — phase math is real and
// transcribed exactly.
public static class MixSoftwareSoundsStereo
{
    public static void Run()
    {
        VoiceState[] voices = SoundMixer.Voices;
        byte[]? output = SoundMixer.CurrentMixData;
        int outOffset = 0;
        for (int framesLeft = SoundMixer.FramesPerBlock; framesLeft != 0; framesLeft--)
        {
            int leftAccum = 0;
            int rightAccum = 0;
            for (int i = 0; i < SoundMixer.ActiveVoiceCount; i++)
            {
                VoiceState voice = voices[i];
                // Only voices below MaxVoices are audible (8 audible vs 16 slots).
                if (i < SoundMixer.MaxVoices)
                {
                    int cur = voice.CurSampleIndex;
                    int sample = MixSoftwareSounds.SampleAt(voice.Samples, cur);
                    if (cur == voice.PrevSampleIndex)
                        sample = (sample + MixSoftwareSounds.SampleAt(voice.Samples, cur + 1)) >> 1;
                    voice.PrevSampleIndex = cur;
                    leftAccum += (sample * voice.LeftVolume) >> 7;
                    rightAccum += (sample * voice.RightVolume) >> 7;
                }
                // ORIGINAL QUIRK (kept): phase advance OUTSIDE the audibility check.
                voice.PhaseAccum += (uint)voice.StepFixed >> 8;
                // Byte cursor `base + (Phase>>7 & ~1)` ≡ sample index Phase>>8.
                voice.CurSampleIndex = (int)(voice.PhaseAccum >> 8);
            }
            if (leftAccum > 127) leftAccum = 127;
            if (leftAccum < -127) leftAccum = -127;
            if (rightAccum > 127) rightAccum = 127;
            if (rightAccum < -127) rightAccum = -127;
            // Big-endian 16-bit store: hi byte = LEFT+0x80, lo byte = RIGHT+0x80.
            // PORT ADDITION: bounds guard with no ASM counterpart (the original
            // writes blindly for exactly FramesPerBlock iterations) — should
            // never trip since CurrentMixData is always sized to match.
            if (output != null && outOffset + 1 < output.Length)
            {
                output[outOffset] = (byte)(leftAccum + 128);
                output[outOffset + 1] = (byte)(rightAccum + 128);
            }
            outOffset += 2;
        }
    }
}
