using System;
using System.Buffers.Binary;

namespace OpenEV.Platform.Toolbox;

// Mac snd resource decoder. Inside Macintosh: Sound, ch. 2.
//
// Format 1 layout we care about (the common case used by EVO sounds):
//   uint16 format        (= 1)
//   uint16 numDataFormats(= 1)
//   uint16 dataFormatId  (= 5 for sampledSnd)
//   uint32 initOption    (initStereo / initMono flags)
//   uint16 numCmds       (= 1 typical)
//   uint16 cmd           (= 0x8051 — bufferCmd with "param2 = offset" bit set)
//   uint16 param1        (= 0)
//   uint32 param2        (= offset from snd start to the sound header)
//   sound header:
//     uint32 samplePtr   (= 0 means samples are inline after the header)
//     uint32 numSamples  (count of 8-bit samples)
//     uint32 sampleRate  (Fixed 16.16)
//     uint32 loopStart, loopEnd
//     uint8  encoding    (0 = stdSH, 0xFE = cmpSH, 0xFF = extSH)
//     uint8  baseFrequency (MIDI note; 60 = middle C)
//   sample data follows.
//
// We only handle stdSH (8-bit unsigned mono). EVO uses this for SFX. The handful
// of compressed/extended snd resources (music, intro) need a heavier decoder.
public static class MacSnd
{
    public sealed record DecodedSound(byte[] Pcm16LE, int SampleRate, int Channels);

    public static DecodedSound? Decode(byte[] resource)
    {
        var s = resource.AsSpan();
        if (s.Length < 22) return null;
        ushort format = BinaryPrimitives.ReadUInt16BigEndian(s);
        int hdrOff;
        if (format == 1)
        {
            // skip format + numDataFormats + (dataFormatId+initOption)*1 + numCmds
            int numDataFormats = BinaryPrimitives.ReadUInt16BigEndian(s.Slice(2));
            int cur = 4 + numDataFormats * 6;
            if (s.Length < cur + 10) return null;   // numCmds(2) + cmd+param1(4) + param2(4) = 10
            int numCmds = BinaryPrimitives.ReadUInt16BigEndian(s.Slice(cur)); cur += 2;
            if (numCmds < 1) return null;
            // first command should be bufferCmd
            cur += 4; // skip cmd + param1
            uint param2 = BinaryPrimitives.ReadUInt32BigEndian(s.Slice(cur)); cur += 4;
            hdrOff = (int)param2;
        }
        else if (format == 2)
        {
            // Per Inside Macintosh: Sound vol II p2-99 — snd fmt 2 layout:
            //   format(2)=2, refCount(2), numCmds(2), commands[numCmds] (8B
            //   each: cmd(2), param1(2), param2(4)). The first command is
            //   typically bufferCmd (0x8051) with param2 = header offset.
            if (s.Length < 14) return null;
            int numCmds = BinaryPrimitives.ReadUInt16BigEndian(s.Slice(4));
            if (numCmds < 1) return null;
            // First command's param2 is at offset 6 + 2 + 2 = 10.
            uint param2 = BinaryPrimitives.ReadUInt32BigEndian(s.Slice(10));
            hdrOff = (int)param2;
        }
        else return null;

        if (hdrOff < 0 || hdrOff + 22 > s.Length) return null;
        var h = s.Slice(hdrOff);
        uint samplePtr = BinaryPrimitives.ReadUInt32BigEndian(h);
        uint numSamples = BinaryPrimitives.ReadUInt32BigEndian(h.Slice(4));
        uint rateFixed = BinaryPrimitives.ReadUInt32BigEndian(h.Slice(8));
        byte encoding = h[20];
        // baseFrequency at h[21] — unused for raw playback

        int sampleRate = (int)(rateFixed >> 16); // truncate Fixed to integer Hz
        if (sampleRate <= 0) sampleRate = 22050;

        int dataOff;
        int frameCount;
        int sampleSizeBits;
        int numChannels;
        if (encoding == 0x00) // stdSH — 8-bit unsigned mono inline
        {
            dataOff = hdrOff + 22;
            frameCount = (int)numSamples;
            sampleSizeBits = 8;
            numChannels = 1;
        }
        else if (encoding == 0xFF) // extSH — extended header
        {
            // Per Inside Macintosh: Sound vol II p2-104, ExtSoundHeader layout
            // after the 22-byte stdSH base (offsets into h):
            //   22: numFrames        (uint32)
            //   26: AIFFSampleRate   (Float80, 10 bytes)
            //   36: markerChunk      (Ptr, 4)
            //   40: instrumentChunks (Ptr, 4)
            //   44: AESRecording     (Ptr, 4)
            //   48: sampleSize       (uint16)  ← bits per sample (8 or 16)
            //   50: futureUse1..4    (4 × uint16 = 8 bytes)
            //   58: sample data starts
            // Also: stdSH at offset 4 has the channel count for fmt-1 snds
            // (samplePtr is reused as numChannels in fmt=1 stdSH/extSH/cmpSH
            // when format == 1, samplePtr is the channel count word per IM).
            // We treat samplePtr as the channel count for extSH/cmpSH; fmt=2
            // single-buffered snds typically encode mono.
            if (h.Length < 60) return null;
            uint numFrames    = BinaryPrimitives.ReadUInt32BigEndian(h.Slice(22));
            ushort sampleSize = BinaryPrimitives.ReadUInt16BigEndian(h.Slice(48));
            dataOff           = hdrOff + 58;
            frameCount        = (int)numFrames;
            sampleSizeBits    = sampleSize;
            // ExtSoundHeader.numChannels is a separate long at offset 4 (IM:Sound),
            // NOT samplePtr@0 (samplePtr is nil for an inline sound). Reading
            // samplePtr only worked for mono by accident (0 -> clamp -> 1); a stereo
            // extSH was mis-read as mono. Read the real field at +4.
            numChannels = (int)BinaryPrimitives.ReadUInt32BigEndian(h.Slice(4));
            if (numChannels < 1 || numChannels > 2) numChannels = 1;
            if (sampleSizeBits != 8 && sampleSizeBits != 16) return null;
        }
        else return null; // cmpSH (compressed) not yet supported

        // samplePtr ≠ 0 in stdSH means out-of-line samples (we don't load
        // those); in extSH samplePtr was repurposed as the channel count
        // and the gate is moot.
        if (encoding == 0x00 && samplePtr != 0) return null;

        int bytesPerSample = sampleSizeBits / 8;
        int totalSampleBytes = frameCount * bytesPerSample * numChannels;
        if (totalSampleBytes <= 0) return null;
        totalSampleBytes = System.Math.Min(totalSampleBytes, s.Length - dataOff);
        if (totalSampleBytes <= 0) return null;

        byte[] pcm;
        if (sampleSizeBits == 16)
        {
            // Big-endian 16-bit signed samples (Mac native order) → little-
            // endian 16-bit signed for MonoGame's SoundEffect.
            pcm = new byte[totalSampleBytes];
            for (int i = 0; i < totalSampleBytes; i += 2)
            {
                if (i + 1 >= totalSampleBytes) break;
                pcm[i + 0] = s[dataOff + i + 1];
                pcm[i + 1] = s[dataOff + i];
            }
        }
        else
        {
            // 8-bit unsigned → 16-bit signed little-endian.
            int frameBytes = totalSampleBytes;
            pcm = new byte[frameBytes * 2];
            for (int i = 0; i < frameBytes; i++)
            {
                int sample = s[dataOff + i] - 128;  // 0..255 → -128..127
                short s16 = (short)(sample * 256);  // expand to 16-bit
                pcm[i * 2 + 0] = (byte)(s16 & 0xFF);
                pcm[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
            }
        }
        return new DecodedSound(pcm, sampleRate, numChannels);
    }
}
