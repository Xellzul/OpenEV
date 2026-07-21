using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 48626-48731.
// Boots the software-mixer half of the sound subsystem: Gestalt-checks 'snd ',
// picks the hardware output sample rate, sizes the mixer (mono/stereo + voice
// count), allocates the Sound Manager double-buffer pair, opens the mixer
// SndChannel and starts double-buffer playback. Fully managed (B4): all state
// lives in SoundMixer; the Mac SndPlayDoubleBuffer maps to the SoundMixer pump
// (both buffers in flight, the doubleback on the 2.7676-tick block cadence).
// GameBootSequence step 19 is the only caller — Run(8, true, 0).
//
// Param 3 (hardwareRateFixedOverride) is stored straight into
// SoundMixer.HardwareSampleRateFixed; Boot passes 0 = "ask the hardware" on
// Sound Manager 3.1+.
public static class BootSoundSubsystem
{
    // The classic Mac 22 kHz output rate as a 16.16 Fixed: 0x56ee8ba3 =
    // 22254.5454 Hz. Seeded when the rate override is 0 on SM 3.1+, then offered
    // to the hardware probe (whose port shim leaves the seed in place).
    private const int DefaultHardwareRateFixed = 0x56ee8ba3;

    public static int Run(uint requestedChannels, bool stereoEnabled, int hardwareRateFixedOverride)
    {
        if (EvoGlobals.IsSoundSubsystemBooted)
            return -1001;   // 0xfffffc17 — app sound-init error: already booted

        // .glue::Gestalt('snd ' 0x736e6420, local_34) — gestaltSoundAttr. Honest port
        // shim (bit0 = gestaltStereoCapability SET, noErr); InitSoundSubsystem's
        // stereo probe routes through the same source.
        short gestaltErr = MacToolbox.GestaltSoundAttrs(out uint soundAttrs);
        if (gestaltErr != 0)
            return -1300;   // 0xfffffaec — app sound-init error: no 'snd ' Gestalt

        SoundMixer.HardwareSampleRateFixed = hardwareRateFixedOverride;
        if (IsSoundManagerV3_1Plus.Run() && hardwareRateFixedOverride == 0)
        {
            SoundMixer.HardwareSampleRateFixed = DefaultHardwareRateFixed;
            // FUN_10075dbc(piVar5): probe the hardware rate through SndGetInfo('srat')'s
            // out-pointer (piVar5 = &HardwareSampleRateFixed). The port's SndGetInfo shim
            // reports noErr and never writes, so the 22254.5454 Hz seed survives — see
            // ProbeSoundChannelSampleRate.
            ProbeSoundChannelSampleRate.Run(ref SoundMixer.HardwareSampleRateFixed);
        }

        if ((soundAttrs & (uint)SoundGestaltAttrs.StereoCapability) == 0)   // no stereo hardware -> force mono
            stereoEnabled = false;
        SoundMixer.StereoEnabled = stereoEnabled;
        SndChannelInitFlags initFlags;
        if (!stereoEnabled)
        {
            initFlags = SndChannelInitFlags.Mono;
            SoundMixer.OutputChannelCount = 1;
        }
        else
        {
            initFlags = SndChannelInitFlags.Stereo;
            SoundMixer.OutputChannelCount = 2;
        }
        SoundMixer.MaxVoices = (int)(requestedChannels & 0xffff);
        if (SoundMixer.MaxVoices > SoundMixer.Voices.Length)   // voice-count cap
            SoundMixer.MaxVoices = SoundMixer.Voices.Length;
        InitSoundMixerState.Run();

        // SndDoubleBufferHeader fill.
        SoundMixer.SndDoubleBufferHeader header = SoundMixer.Header;
        header.NumChannels = SoundMixer.OutputChannelCount;
        header.SampleSize = 8;
        header.CompressionId = 0;
        header.PacketSize = 0;
        header.SampleRateFixed = SoundMixer.HardwareSampleRateFixed;   // the probed rate
        header.Buffers[0] = AllocSoundBuffer.Run();
        if (header.Buffers[0] is null)
            return -1000;   // 0xfffffc18 — app sound-init error: NewPtr returned null

        header.Buffers[1] = AllocSoundBuffer.Run();
        // ORIGINAL-BUG: checks buffer[0] again, not the just-allocated buffer[1] — a
        // failed second alloc goes undetected. Preserved.
        if (header.Buffers[0] is null)
        {
            header.Buffers[0] = null;   // DisposePtr(header.Buffers[0])
            return -1000;   // 0xfffffc18
        }

        // NewRoutineDescriptor(TVector FUN_100756e8, 0x3c0, 1) -> the double-back,
        // by method group.
        header.DoubleBackProc = TickSoundCallback.Run;
        // FUN_10075c14 — the NewPtrClear(0x424) SndChannel record ('Schn' sentinel in the port).
        SoundMixer.MixerChannelHandle = AllocSoundChannelControlBlock.Run();
        // SndNewChannel(&channel, sampledSynth 5, initMono/initStereo, no userRoutine).
        short newChannelErr = MacToolbox.SndNewChannel(SoundMixer.MixerChannelHandle, 5, (byte)initFlags, 0);
        if (newChannelErr != 0)
        {
            header.Buffers[0] = null;   // DisposePtr(header.Buffers[0])
            header.Buffers[1] = null;   // DisposePtr(header.Buffers[1])
            return -1000;   // 0xfffffc18
        }

        // SndPlayDoubleBuffer(*channel, header) — the port's mapping: both buffers go
        // "in flight" and the doubleback fires on the 1024-frame block cadence
        // (~2.7676 ticks at 22254.5454 Hz), replayed by the SoundMixer pump from
        // TickSoundSubsystem / CountMatchingSoundVoices. The managed pump cannot
        // fail, so playErr stays noErr; the error branch keeps the decompile shape.
        SoundMixer.StartPump();
        short playErr = 0;
        if (playErr != 0)
        {
            header.Buffers[0] = null;   // DisposePtr(header.Buffers[0])
            header.Buffers[1] = null;   // DisposePtr(header.Buffers[1])
            MacToolbox.SndDisposeChannel(SoundMixer.MixerChannelHandle, true);
            SoundMixer.StopPump();      // port-only: undo the StartPump above
            return -1000;   // 0xfffffc18
        }

        EvoGlobals.IsSoundSubsystemBooted = true;
        FlushMixQueueEntries.Run(0);
        if (SoundMixer.UseHardwareVolume)
        {
            // GetDefaultOutputVolume(): the original Mac trap took an out-pointer
            // and wrote the current hardware volume through it; here it just
            // returns the value.
            SoundMixer.SavedHardwareVolume = MacToolbox.GetDefaultOutputVolume();
        }
        // The decompile dropped the r3 chain at lines 48705-48706: FUN_10074e44's
        // return value IS FUN_10074ddc's argument — re-apply the current master
        // volume (the initial transcription called both with the value discarded / a params-absorber).
        SetMasterVolume.Run((ushort)GetMasterVolume.Run());
        return 0;   // noErr — uVar10 stayed 0
    }
}
