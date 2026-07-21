using System;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Managed home for the SOFTWARE-MIXER (voice) layer of the sound subsystem — the
// state the original kept behind the pointer-cell band 0x100819e4..0x10081a28
// (each cell holds a PEF-relocated pointer to a BSS scalar near 0x1008240c or an
// array near 0x10089368). TOC aliases GameToc-0x6cXX and the decompile's ppuVar[-0x1bXX]
// reach the SAME cells.
//
// LAYOUT NOTE: the 0x10089368..0x1008a03a BSS block every array cell points
// into lies INSIDE the 256-slot mask-GWorld record table (GameToc+0x488 =
// 0x10088ae8 + idx*0x1a; see Graphics.Model.SlotGWorlds) — boot's sprite
// loading wrote mask records from slot 83 upward straight through it in the
// ORIGINAL PEF layout too. The slot tables are now the managed SlotGWorlds
// records, so the whole BSS range (sound alias block included) is managed —
// on top of the pointer cells + the 0x1008240c.. scalar cluster here.
//
// Cell map (cell -> BSS target -> field here):
//   0x10081a00 -> 0x10089368  Voices[16] (0x34-byte stride; see VoiceState)
//   0x100819fc -> 0x10082414  ActiveVoiceCount
//   0x100819f8 -> 0x1008242c  NextVoiceId (init 1, += 2 — odd-id sequence)
//   0x10081a1c -> 0x10082410  MaxVoices (boot clamps to <= 0x10; passes 8)
//   0x10081a20 -> 0x1008240e  OutputChannelCount (1 mono / 2 stereo)
//   0x10081a24 -> 0x10082428  StereoEnabled (byte)
//   0x10081a28 -> 0x10082418  HardwareSampleRateFixed (0x56ee8ba3 = 22254.54 Hz)
//   0x10081a04 -> 0x1008240c  SoftwareVolume (ushort; WRITE-ONLY in the binary)
//   0x10081a08 -> 0x1008241c  SavedHardwareVolume (L<<16|R)
//   0x10081a0c -> 0x10082429  UseHardwareVolume (byte)
//   0x10081a10 -> 0x10082420  MixerChannelHandle ('Schn' sentinel in the port)
//   0x10081a18 -> 0x100896a8  Header (Mac SndDoubleBufferHeader record)
//   0x100819f0 -> 0x10082424  CurrentMixData (mixer output cursor = dblBuffer+0x10)
//   0x100819f4 -> 0x100896d4  CallbackScratch (notify-record the doubleback fills)
//   0x100819ec -> 0x100896c0  the 0x14-byte play-command block {0, 7, 0, fillUPP, 0}
//                             built by InitSoundMixerState — only the UPP mattered;
//                             modelled as FillProc below.
//   0x100819e4 -> TVector FUN_1007594c (MONO fill)    } InitSoundMixerState picks
//   0x100819e8 -> TVector FUN_10075830 (STEREO fill)  } one into FillProc
//   0x10081a14 -> TVector FUN_100756e8 (the doubleback) — DoubleBackProc
public static class SoundMixer
{
    // Fixed voice-slot count (was the BSS array's compile-time size; only the
    // first MaxVoices of these are ever audible — see MaxVoices below).
    public const int VoiceSlotCount = 16;

    public static readonly VoiceState[] Voices = NewVoices();

    private static VoiceState[] NewVoices()
    {
        var v = new VoiceState[VoiceSlotCount];
        for (int i = 0; i < v.Length; i++) v[i] = new VoiceState();
        return v;
    }

    public static int ActiveVoiceCount;
    public static int NextVoiceId;            // InitSoundMixerState sets 1
    public static int MaxVoices;               // only voices < MaxVoices are audible (8 vs 16 slots — faithful)
    public static short OutputChannelCount; // 1 mono / 2 stereo
    public static bool StereoEnabled;
    public static int HardwareSampleRateFixed; // Fixed 16.16
    // ushort: SetMasterVolume stores 0..0x100 (so byte would truncate unity).
    // ORIGINAL QUIRK: no reader exists anywhere in the binary (see SetMasterVolume).
    public static ushort SoftwareVolume;
    public static int SavedHardwareVolume;
    public static bool UseHardwareVolume;
    public static int MixerChannelHandle;

    // Mixer fill routine — InitSoundMixerState assigns MixSoftwareSounds.Run (mono)
    // or MixSoftwareSoundsStereo.Run (stereo) by method group (no lambdas).
    public static Action? FillProc;

    // ── Mac SndDoubleBufferHeader ──
    public sealed class SndDoubleBufferHeader
    {
        public short NumChannels;       // +0  = OutputChannelCount
        public short SampleSize;        // +2  = 8
        public short CompressionId;     // +4  = 0
        public short PacketSize;        // +6  = 0
        public int SampleRateFixed;     // +8  = HardwareSampleRateFixed
        public SndDoubleBuffer?[] Buffers = new SndDoubleBuffer?[2];  // +12/+16
        public SndDoubleBackProc? DoubleBackProc;                     // +20 (routine descriptor)
    }

    // Mac SndDoubleBuffer (was a NewPtr block of OutputChannelCount*0x400+0x12 bytes,
    // AllocSoundBuffer FUN_10075b80): {+0 dbNumFrames, +4 dbFlags (1 = bufferReady),
    // +8 dbUserInfo, +0x10.. sample data (0x80 = 8-bit silence)}.
    public sealed class SndDoubleBuffer
    {
        public int NumFrames;
        public int Flags;
        public int UserInfo;
        public byte[] Data = Array.Empty<byte>();
    }

    public static readonly SndDoubleBufferHeader Header = new();

    // Frames mixed into one SndDoubleBuffer bank per double-buffer block (was
    // the 0x400 NewPtr dbNumFrames / mixer loop-count constant).
    public const int FramesPerBlock = 1024;

    // Where the fill routines write this block's PCM — the doubleback sets it
    // to the current buffer's Data before dispatching.
    public static byte[]? CurrentMixData;

    // Scratch notify record the doubleback/flush fill before invoking a voice's
    // CompletionProc.
    public static readonly SoundPlayRequest CallbackScratch = new();

    // Boarding-chime play request (was the static record 0x1008a700..0x1008a71b,
    // filled by LoadAllUiSoundEffects: snd 0x80, rate 1.0, completion =
    // TriggerBoardingAlarmOnce [FUN_10023210 via TVector *0x10081174],
    // priority 32000, volumes 0x80).
    public static readonly SoundPlayRequest BoardingChimeRequest = new();

    // ── Port double-buffer pump ─────────────────────────────────────────────
    // DELIBERATE PORT DEVIATION (documented): the Mac Sound Manager invoked the
    // doubleback at interrupt time every 1024 output frames at 22254.5454 Hz
    // (~2.7676 ticks). The port has no audio interrupt, so the pump replays that
    // cadence off MacToolbox.TickCount. Called from exactly TWO sound-core
    // sites — TickSoundSubsystem (gameplay) and CountMatchingSoundVoices
    // (the title/dialog wait loops poll it) — never from a third (re-entrancy).
    public const double TicksPerBlock = FramesPerBlock * 60.15 / 22254.5454;

    public static bool PumpStarted;
    private static double _pumpAccumulatorTicks;
    private static int _pumpNextBuffer;
    private static bool _pumping;

    // SndPlayDoubleBuffer (the boot step) maps to this: both buffers are "in
    // flight" from now on and the doubleback fires on the block cadence.
    public static void StartPump()
    {
        PumpStarted = true;
        _pumpAccumulatorTicks = MacToolbox.TickCount();
        _pumpNextBuffer = 0;
    }

    public static void StopPump() => PumpStarted = false;

    public static void PumpDoubleBuffer()
    {
        if (!PumpStarted || _pumping) return;
        _pumping = true;
        uint now = MacToolbox.TickCount();
        while (now - _pumpAccumulatorTicks >= TicksPerBlock)
        {
            _pumpAccumulatorTicks += TicksPerBlock;
            var buffer = Header.Buffers[_pumpNextBuffer];
            _pumpNextBuffer ^= 1;
            if (buffer is null) continue;
            Header.DoubleBackProc?.Invoke(MixerChannelHandle, buffer);
        }
        _pumping = false;
    }
}

// The doubleback signature — FUN_100756e8(channel, SndDoubleBuffer*).
public delegate void SndDoubleBackProc(int channel, SoundMixer.SndDoubleBuffer buffer);
