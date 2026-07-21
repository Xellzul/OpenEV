using System;
using System.Collections.Generic;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Managed registry of DECODED 'snd ' resources — the port's replacement for the
// 'asnd' heap blocks FUN_10075450 built with NewPtrClear. A loaded snd is keyed
// by its id; the public handle stays the established 0x5D?????? sentinel
// (MacToolbox.MakeSndHandle), which the mixer voices carry in SoundHandle and
// the host bridge decodes for playback. The sentinel is NEVER dereferenced —
// this registry is the only lookup.
//
// 'asnd' block layout being replaced (FUN_10075450, EV Override-11.c 49015-49069):
//   +0x00 'asnd' magic
//   +0x04 BlockCount       — max(1, ceil(sampleBytes / 0x400)) 1024-frame pages
//   +0x08 SampleRateFixed  — SoundHeader+8 of the resource
//   +0x0c int16 samples    — each 8-bit unsigned source byte biased to signed
//                            (b - 0x80); trailing space zero (NewPtrClear)
public static class SndResourceRegistry
{
    public sealed class DecodedSnd
    {
        public int SndId;
        public int BlockCount;
        public int SampleRateFixed;
        // Decoded int16 samples, zero-padded to BlockCount*0x400 + 0x200 entries
        // exactly like the original's NewPtrClear slack, so the mixer's phase
        // cursor can run past the audio tail into silence.
        public short[] Samples = Array.Empty<short>();
        // GetHandleSize of the source resource — PlaySoundOnChannel's expiry
        // maths use the RESOURCE size, not the decoded size.
        public int ResourceLength;
        // Offset of the SoundHeader inside the resource (FUN_10075d1c result).
        public int SoundHeaderOffset;
        // SoundHeader encode byte (header+0x14): 0x00 stdSH, 0xFF extSH,
        // 0xFE cmpSH. PlaySoundOnChannel branches its expiry maths on it.
        public byte EncodeByte;
        // CmpSoundHeader compressionID (header+0x38) — only meaningful when
        // EncodeByte is 0xFE (cmpSH): 3 = MACE 3:1, 6 = MACE 6:1. For std/ext
        // headers the original never reads it; stored unconditionally here
        // (bounds-checked, 0 when the resource is too short).
        public short CompressionId;
    }

    private static readonly Dictionary<int, DecodedSnd> _byId = new();

    /// FUN_10075450: GetResource('snd ', id) -> locate the SoundHeader -> decode
    /// the 8-bit samples to int16. Returns the snd-handle sentinel, or 0 when the
    /// resource doesn't exist / has no playable sound command (the original
    /// returned a null Ptr — LoadAllUiSoundEffects' bank-probe loop NEEDS the 0
    /// to terminate).
    public static int LoadAndRegister(int sndId)
    {
        int resHandle = MacToolbox.GetResource(MacResType.Snd, sndId);
        if (resHandle == 0) return 0;
        byte[]? res = MacToolbox.ResourceBytes(resHandle);
        if (res is null || res.Length < 4) return 0;

        // FUN_10075d1c — the command-list walk lives in FindSoundCommandData (B3).
        int headerOffset = FindSoundCommandData.Run(res);
        if (headerOffset <= 0) return 0;

        int sampleRateFixed = BigEndian.ReadInt32OrZero(res, headerOffset + 8);
        // FUN_10075450 also computes FixDiv(sampleRateFixed>>8, HardwareSampleRateFixed>>8)
        // here and discards the result (decompile line 49039) — EnqueueSoundChannel
        // recomputes the same ratio when a voice actually starts playing. FixDiv is
        // pure (MacToolbox.MathTraps.cs), so the discarded call is behaviorally inert;
        // kept as a bare call for call-graph fidelity.
        MacToolbox.FixDiv((int)((uint)sampleRateFixed >> 8), (int)((uint)SoundMixer.HardwareSampleRateFixed >> 8));

        // ORIGINAL QUIRK (kept): the standard SoundHeader is 0x16 bytes but the
        // copy starts at +0x17 — the first sample byte is skipped.
        int sampleBytes = res.Length - (headerOffset + 0x17);
        // The decompile renders this as a signed-truncating divide (srawi+addze)
        // that differs from a plain shift only when sampleBytes is very negative
        // (a corrupt/truncated resource) — and there, both forms are already < 1
        // and get clamped below, so the plain shift is behaviorally identical.
        int blockCount = (sampleBytes + 0x3ff) >> 10;
        if (blockCount < 1) blockCount = 1;
        // NOT FAITHFUL for a corrupt/near-empty resource (sampleBytes in [-1023, 0]):
        // the original allocates its 'asnd' buffer from the UNCLAMPED block count
        // (0 blocks there -> a 512-sample payload) while this always allocates from
        // the CLAMPED >=1 blockCount (a 1536-sample payload) — a managed-array-safe
        // over-allocation, never smaller. Unreachable with any real 'snd ' resource
        // (real assets carry far more than 1024 bytes of sample data).

        var decoded = new DecodedSnd
        {
            SndId = sndId,
            BlockCount = blockCount,
            SampleRateFixed = sampleRateFixed,
            Samples = new short[blockCount * 0x400 + 0x200],
            ResourceLength = res.Length,
            SoundHeaderOffset = headerOffset,
            EncodeByte = (uint)(headerOffset + 0x14) < (uint)res.Length ? res[headerOffset + 0x14] : (byte)0,
            CompressionId = BigEndian.ReadInt16OrZero(res, headerOffset + 0x38),
        };
        // UNPRESERVABLE for negative sampleBytes (corrupt resource, header past the
        // buffer end): the decompile's pretest-then-decrement loop would run until
        // the counter underflows back to 0 — a multi-gigabyte overrun on real Mac
        // heap memory. A managed `for` can't replicate that; it correctly runs zero
        // iterations instead. Unreachable with any real 'snd ' resource.
        for (int i = 0; i < sampleBytes; i++)
            decoded.Samples[i] = (short)(res[headerOffset + 0x17 + i] - 0x80);

        _byId[sndId] = decoded;
        return MacToolbox.MakeSndHandle(sndId);
    }

    /// Resolve a 0x5D snd-handle sentinel to its decoded sound. False for 0,
    /// non-sentinel values, and ids never loaded.
    public static bool TryGet(int handle, out DecodedSnd decoded)
    {
        if (MacToolbox.TryGetSndId(handle, out int sndId) &&
            _byId.TryGetValue(sndId, out var found))
        {
            decoded = found;
            return true;
        }
        decoded = null!;
        return false;
    }
}
