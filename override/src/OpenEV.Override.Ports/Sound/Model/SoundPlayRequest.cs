namespace OpenEV.Override.Ports.Sound.Model;

// Completion-notify callback carried by a play request / mixer voice (the value the
// original kept as a TVector code pointer in request +0xc / voice +0x1c and invoked
// through the cross-TOC glue FUN_1008062c).
//
// `kind` values (from the doubleback FUN_100756e8 and flush FUN_1007520c):
//   3 = mixing tick — fired EVERY double-buffer block for a voice that has a proc
//   1 = completed   — voice's BlocksRemaining hit 0 (natural end)
//   2 = flushed     — voice removed by FlushMixQueueEntries
// `voiceId` is the mixer voice id for kinds 3/1, and the *flush query* (handle or id
// or 0) for kind 2 — faithful to FUN_1007520c passing its own param through.
public delegate void SoundCompletionProc(int kind, int voiceId, SoundPlayRequest request);

// The 0x1c-byte sound-play request record (built on the stack by TriggerSoundPlay /
// FUN_10074ec0, and statically at 0x1008a700 for the boarding chime and 0x1008a71c
// for the file-music swap). Original layout for decompile cross-reference:
//   +0x00 int   SndHandle   (decoded-'asnd' ptr; the port: 0x5D?????? snd-handle sentinel)
//   +0x04 int   Id          (0 = auto-assign from the mixer's odd-id counter)
//   +0x08 int   RateFixed   (16.16 playback-rate multiplier, 0x10000 = 1.0)
//   +0x0c int   CompletionProc (TVector; the port: managed delegate)
//   +0x10 int   Refcon
//   +0x14 ushort Priority    (EnqueueSoundVoice clamps 0 -> 1)
//   +0x16 ushort LeftVolume  (0..0x80; the mono mixer reads ONLY this side)
//   +0x18 ushort RightVolume (0..0x80)
public sealed class SoundPlayRequest
{
    public int SndHandle;
    public int Id;
    public int RateFixed;
    public SoundCompletionProc? CompletionProc;
    public int Refcon;
    public ushort Priority;
    public ushort LeftVolume;
    public ushort RightVolume;
}
