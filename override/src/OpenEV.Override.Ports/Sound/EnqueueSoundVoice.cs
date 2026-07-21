using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 48839-48935.
// Inserts a play request into the 16-slot software-mixer voice array
// (SoundMixer.Voices), which is kept sorted by priority then combined volume.
// Returns the assigned mixer voice id, 0 when rejected (null handle, subsystem
// not booted, or all slots out-prioritise the request).
public static class EnqueueSoundVoice
{
    public static int Run(SoundPlayRequest request)
    {
        int result = 0;
        bool foundSlot = false;
        if (request.SndHandle != 0 && EvoGlobals.IsSoundSubsystemBooted)
        {
            if (request.Priority == 0)
                request.Priority = 1;
            // ORIGINAL ORDER (kept): RightVolume is clamped BEFORE LeftVolume.
            if (request.RightVolume > 128)
                request.RightVolume = 128;
            if (request.LeftVolume > 128)
                request.LeftVolume = 128;
            // The decompile passes the clamped RightVolume into FUN_10075f08 — an
            // ABI artifact (stale r3); the interrupt-glue UPP takes no arguments.
            int interruptMask = SoundProcs.SaveInterruptMask();

            VoiceState[] voices = SoundMixer.Voices;
            short slot = 0;
            // Original condition order is (voice.SoundHandle != 0 && slot < 16 &&
            // !found) — it reads one record PAST the array when every slot is busy
            // (benign garbage read on the Mac). A managed array can't, so the
            // bounds test runs first; the loop exits identically either way.
            while (slot < voices.Length && voices[slot].SoundHandle != 0 && !foundSlot)
            {
                if (request.Priority < voices[slot].Priority ||
                    request.RightVolume + request.LeftVolume <
                    voices[slot].LeftVolume + voices[slot].RightVolume)
                {
                    slot++;
                }
                else
                {
                    foundSlot = true;
                }
            }
            if (slot < voices.Length)
            {
                if (SoundMixer.ActiveVoiceCount > voices.Length - 1)
                    SoundMixer.ActiveVoiceCount = voices.Length - 1;
                int count = SoundMixer.ActiveVoiceCount;
                // ORIGINAL SILENT DROP (kept): BlockMoveData shifts records
                // slot..count-1 UP one slot; when all slots were busy (count
                // clamped above) the live voice in the last slot is overwritten
                // without any notify.
                // PORT ADDITION (documented deviation): stop that dropped voice's
                // host one-shot so its audio doesn't keep playing unowned.
                if (count == voices.Length - 1 && voices[voices.Length - 1].SoundHandle != 0)
                    MacToolbox.StopSndForHandle(voices[voices.Length - 1].SoundHandle);
                for (int i = count; i > slot; i--)
                    voices[i].CopyFrom(voices[i - 1]);

                // Rate maths. The original derefs the 'asnd' block (+4 block count,
                // +8 sample rate) with NO validity check — a garbage handle reads
                // garbage. The port mirrors the control flow (no rejection path): an
                // unregistered handle resolves to rate 0 / block count 0, and the
                // Fix maths run on those zeros (FixDiv-by-0 saturates per the trap).
                bool registered = SndResourceRegistry.TryGet(request.SndHandle, out SndResourceRegistry.DecodedSnd decoded);
                int sndRateFixed = registered ? decoded.SampleRateFixed : 0;
                int blockCount = registered ? decoded.BlockCount : 0;
                int rateRatioFixed = MacToolbox.FixDiv(
                    (int)((uint)sndRateFixed >> 8),
                    (int)((uint)SoundMixer.HardwareSampleRateFixed >> 8));
                int stepFixed = MacToolbox.FixMul(rateRatioFixed, request.RateFixed);
                // Unsigned >> 0x10 of the FixDiv result, exactly as the decompile
                // (uVar7 is uint there).
                uint blocks = (uint)MacToolbox.FixDiv(blockCount << 0x10, stepFixed) >> 0x10;
                if (blocks == 0)
                    blocks = 1;

                VoiceState voice = voices[slot];
                voice.SoundHandle = request.SndHandle;
                voice.Samples = registered ? decoded.Samples : null;
                voice.CurSampleIndex = 0;
                voice.PrevSampleIndex = -1;
                voice.BlocksRemaining = (int)blocks;
                voice.RateRatioFixed = rateRatioFixed;
                // Original recomputes FixMul from the stored RateRatioFixed for the
                // stored step (same operands as `stepFixed` above) — transcribed.
                voice.StepFixed = MacToolbox.FixMul(voice.RateRatioFixed, request.RateFixed);
                voice.PhaseAccum = 0;
                voice.CompletionProc = request.CompletionProc;
                voice.Priority = request.Priority;
                voice.LeftVolume = request.LeftVolume;
                voice.RightVolume = request.RightVolume;
                voice.Refcon = request.Refcon;
                if (request.Id == 0)
                {
                    voice.Id = SoundMixer.NextVoiceId;
                    SoundMixer.NextVoiceId += 2;
                }
                else
                {
                    voice.Id = request.Id;
                }
                result = voice.Id;
                // ORIGINAL MONO QUIRK (kept): without stereo, a louder RIGHT side
                // is folded into LeftVolume (the mono mixer reads only LeftVolume).
                if (!SoundMixer.StereoEnabled && request.LeftVolume < request.RightVolume)
                    voice.LeftVolume = request.RightVolume;
                SoundMixer.ActiveVoiceCount++;

                // PORT HOST BRIDGE (deliberate, documented): the software mixer's PCM
                // output is bookkeeping-only — the audio comes from the host engine.
                // This is the ONE bridge call site for mixer one-shots, so the
                // original priority/slot/rejection semantics gate what is heard.
                if (MacToolbox.TryGetSndId(request.SndHandle, out int sndId))
                    MacToolbox.SndPlay(sndId, (short)request.Priority, (short)voice.LeftVolume, (short)voice.RightVolume);

                SoundProcs.RestoreInterruptMask(interruptMask);
            }
            else
            {
                SoundProcs.RestoreInterruptMask(interruptMask);
                result = 0;
            }
        }
        return result;
    }
}
