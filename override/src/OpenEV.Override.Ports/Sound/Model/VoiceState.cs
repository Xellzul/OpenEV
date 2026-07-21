namespace OpenEV.Override.Ports.Sound.Model;

// One software-mixer voice — the typed managed home for a 0x34-byte slot of the
// 16-entry voice-state array the original kept behind the pointer cell 0x10081a00
// (BSS array at 0x10089368). Field map for decompile cross-reference
// (EnqueueSoundVoice FUN_10074f10 fills, the mixers FUN_10075830/1007594c read):
//   +0x00 int   Id              — odd-number sequence from the mixer counter, or request.Id
//   +0x04 int   SoundHandle     — decoded-'asnd' ptr (port sentinel); 0 = SLOT FREE
//   +0x08 int   SampleDataStart — handle+0xc (the int16 sample area) -> port `Samples`
//   +0x0c int   BlocksRemaining — FixDiv(blockCount<<16, Step)>>16, min 1; doubleback
//                                 decrements; 0 -> voice retires + kind-1 notify
//   +0x10 int   StepFixed       — FixMul(RateRatioFixed, request.RateFixed)
//   +0x14 int   RateRatioFixed  — FixDiv(sndRate>>8, hardwareRate>>8)
//   +0x18 uint  PhaseAccum      — += StepFixed>>8 per output frame
//   +0x1c int   CompletionProc  — TVector; port delegate
//   +0x20 ptr   CurSamplePtr    — DataStart + (PhaseAccum>>7 & ~1) -> port sample INDEX
//   +0x24 ptr   PrevSamplePtr   — == Cur -> mixer averages s[i],s[i+1] -> port index
//   +0x28 int   Refcon
//   +0x2c short LeftVolume      — 0..0x80 (mono output reads ONLY this side)
//   +0x2e short RightVolume     — 0..0x80
//   +0x30 short Priority        — slot-steal comparison key (array kept sorted)
public sealed class VoiceState
{
    public int Id;
    public int SoundHandle;
    // Managed replacement for the +0x08 raw sample pointer: the decoded snd's
    // int16 samples, resolved from SndResourceRegistry when the voice is filled.
    public short[]? Samples;
    public int BlocksRemaining;
    public int StepFixed;
    public int RateRatioFixed;
    public uint PhaseAccum;
    public SoundCompletionProc? CompletionProc;
    // Sample-index forms of the +0x20/+0x24 cursors. On insert the original sets
    // Cur = DataStart (index 0) and Prev = the literal null pointer — modelled as
    // PrevSampleIndex = -1 so the first mixer pass sees Prev != Cur.
    public int CurSampleIndex;
    public int PrevSampleIndex;
    public int Refcon;
    public ushort LeftVolume;
    public ushort RightVolume;
    public ushort Priority;

    // The exact 13-field zero list shared by InitSoundMixerState (FUN_10075a28),
    // FlushMixQueueEntries (FUN_1007520c) and the voice-evict (FUN_10075598).
    // ORIGINAL QUIRK: +0x24 PrevSamplePtr is NOT in the list — a cleared slot keeps
    // its stale Prev cursor. Preserved (PrevSampleIndex deliberately untouched).
    public void Clear()
    {
        Id = 0;
        SoundHandle = 0;
        Samples = null;          // +0x08 SampleDataStart = 0
        BlocksRemaining = 0;
        StepFixed = 0;
        RateRatioFixed = 0;
        PhaseAccum = 0;
        CompletionProc = null;
        CurSampleIndex = 0;      // +0x20 = 0
        Refcon = 0;
        LeftVolume = 0;
        RightVolume = 0;
        Priority = 0;
    }

    // BlockMoveData(voice[i+1] -> voice[i]) equivalent for the priority-sorted
    // compaction shifts — copies ALL fields including PrevSampleIndex (the
    // original moves the whole 0x34-byte record).
    public void CopyFrom(VoiceState other)
    {
        Id = other.Id;
        SoundHandle = other.SoundHandle;
        Samples = other.Samples;
        BlocksRemaining = other.BlocksRemaining;
        StepFixed = other.StepFixed;
        RateRatioFixed = other.RateRatioFixed;
        PhaseAccum = other.PhaseAccum;
        CompletionProc = other.CompletionProc;
        CurSampleIndex = other.CurSampleIndex;
        PrevSampleIndex = other.PrevSampleIndex;
        Refcon = other.Refcon;
        LeftVolume = other.LeftVolume;
        RightVolume = other.RightVolume;
        Priority = other.Priority;
    }
}
