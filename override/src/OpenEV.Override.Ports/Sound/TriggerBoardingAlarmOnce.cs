using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// The 3 SoundCompletionProc `kind` values the mixer dispatches (see
// SoundPlayRequest.cs's own doc comment; corroborated by TickSoundCallback.cs
// and FlushMixQueueEntries.cs). TriggerBoardingAlarmOnce.Run only acts on
// VoiceCompleted, faithfully no-oping on the other two. Sibling completion
// functions (SoundCallback.Run, TickSoundCallback, FlushMixQueueEntries) still
// take the raw int `kind` — each is its own separate conversion.
public enum SoundCompletionKind
{
    VoiceCompleted = 1,
    Flushed = 2,
    MixTick = 3,
}

// Decompile: EV Override-11.c lines 15371-15385.
// Plays UiChimeSnd (resource fork name 'Warp Out') — a DIFFERENT sound than the
// BoardingChimeSnd ('Warp Up') whose voice-completion is what normally drives
// this (see LoadAllUiSoundEffects.cs); TickShipAI.cs also calls this directly
// (Run(SoundCompletionKind.VoiceCompleted)) after flushing that voice early
// when a specific keycode is held (TickBoardingAlarmAudio — role of that key
// not investigated here). AutopilotFlag latches so this fires once.
public static class TriggerBoardingAlarmOnce
{
    public static void Run(SoundCompletionKind notifyKind)
    {
        if (notifyKind == SoundCompletionKind.VoiceCompleted && WorldState.AutopilotFlag == 0)
        {
            WorldState.AutopilotFlag = 1;
            SndPlay.Run(SoundResourceCells.UiChimeSnd, 50, 128, 128);
        }
    }

    // SoundCompletionProc adapter — this is the TVector behind *0x10081174 that
    // LoadAllUiSoundEffects wires into the boarding-chime request's +0xc; the
    // mixer glue passes (kind, voiceId, scratch) and the original reads only the
    // kind (fires the alarm on kind 1 = voice completed).
    public static void Completion(int kind, int voiceId, SoundPlayRequest request) => Run((SoundCompletionKind)kind);
}
