using System;

namespace OpenEV.Platform.Toolbox;

// Sound bridge — the host (OverrideGameV2 / V2TitleAdapter) wires these
// delegates to its MonoGame SoundEngineV2. The Mac Sound Manager double-
// buffer + the game's own software mixer (EnqueueSoundVoice etc.) are
// not portable, so the high-level sound entry points are bridged the same
// way QuickDraw's CopyBits/DrawString are: forward to a host engine.
//
// Bridge points (all faithful to the decompile call graph):
//   • SndPlay (FUN_10060288)        → SndPlayer (already on MacToolbox.cs;
//                                      the graduated SndPlay decodes the
//                                      snd-handle sentinel and calls it).
//   • SndStartFilePlay trap         → FileMusicPlayer  (title music 30000)
//   • SndStopFilePlay  trap         → FileMusicStopper
//   • LoadAndStartSoundPair body    → PairMusicPlayer  (credits 30001)
//   • StopAndDisposeSoundPair body  → PairMusicStopper
public static partial class MacToolbox
{
    /// Looping title-music stream. Bridged from SndStartFilePlay
    /// (FUN_1004227c plays snd 30000). Arg = snd resource id.
    public static Action<int>? FileMusicPlayer;
    /// Stop the title-music stream. Bridged from SndStopFilePlay
    /// (DisposeSoundFileChannel / FUN_10042320).
    public static Action? FileMusicStopper;
    /// Looping credits music. Bridged from LoadAndStartSoundPair
    /// (FUN_100423f4). Args = (primaryId, secondaryId).
    public static Action<int, int>? PairMusicPlayer;
    /// Stop the credits music. Bridged from StopAndDisposeSoundPair
    /// (FUN_100424f8).
    public static Action? PairMusicStopper;
    /// Stop the one-shot SFX still playing for a snd id. Bridged from
    /// FlushMixQueueEntries (FUN_1007520c) — e.g. the row-reveal cuts snd
    /// 601 before it finishes.
    public static Action<int>? SfxStopper;
    /// Stop ALL queued one-shots — FlushMixQueueEntries(0).
    public static Action? SfxStopAll;

    /// Master output volume (0..1). Bridged from SetMasterVolume
    /// (FUN_10074ddc) — the prefs Sound Volume slider. The Mac software mixer
    /// isn't booted in the game, so the trap's mixer path is inert; this forwards the
    /// level to the host SoundEngine instead.
    public static Action<float>? MasterVolumeSetter;

    /// Mac GetDefaultOutputVolume(long *level) — reads the current hardware
    /// output volume in the Sound Manager's L&lt;&lt;16|R format (unsigned fixed,
    /// 0x100 = unity per channel). The Mac trap wrote through an out-pointer
    /// (BootSoundSubsystem passed the SavedHardwareVolume BSS cell); the game returns
    /// the value and the caller stores it in SoundMixer.SavedHardwareVolume.
    /// HONEST shim: there is no Mac hardware mixer here — report full volume on
    /// both channels, (0x100 &lt;&lt; 16) | 0x100 = 0x01000100.
    public static int GetDefaultOutputVolume() => (0x100 << 16) | 0x100;

    /// Gestalt('snd ' 0x736e6420) — the gestaltSoundAttr selector. HONEST shim
    /// for the subsystem's TWO callers (BootSoundSubsystem FUN_10074af0 and
    /// InitSoundSubsystem FUN_10076ae0, both of which only test bit0): the host
    /// MonoGame engine plays stereo, so bit0 (gestaltStereoCapability) is SET,
    /// and the selector exists, so the return is noErr. Needed as a specific
    /// override — the generic Gestalt absorber returns attrs=0, which would
    /// force-disable stereo in BootSoundSubsystem and InitSoundSubsystem.
    public static short GestaltSoundAttrs(out uint soundAttrs)
    {
        soundAttrs = 1;   // bit0 = gestaltStereoCapability
        return 0;         // noErr
    }

    /// FlushMixQueueEntries(handle) bridge: a 0 handle means "flush all"
    /// (stop every one-shot); otherwise decode the snd-handle sentinel and
    /// stop that one-shot. Non-sentinel / music handles fall through to a
    /// no-op (music is torn down via its own stoppers).
    public static void StopSndForHandle(int handle)
    {
        if (handle == 0) { SfxStopAll?.Invoke(); return; }
        if (TryGetSndId(handle, out int sndId)) SfxStopper?.Invoke(sndId);
    }

    // snd-handle sentinel.
    //
    // The real game's FUN_10075450 loads a 'snd ' resource and returns a Mac
    // Handle passed opaquely to SndPlay (FUN_10060288). The game has no Sound
    // Manager, so FUN_10075450 instead returns a SENTINEL encoding the snd id:
    // 0x5D??_???? where the low 16 bits are the id. SndPlay recovers the id and
    // plays via the host engine. The high byte 0x5D keeps the value clear of the
    // arena's address range (0x10??????, used by NewPtr/NewHandle).
    private const int SndHandleTag = 0x5D000000;

    public static int MakeSndHandle(int sndId) => SndHandleTag | (sndId & 0xFFFF);

    public static bool TryGetSndId(int handle, out int sndId)
    {
        if ((handle & unchecked((int)0xFFFF0000)) == SndHandleTag)
        {
            sndId = handle & 0xFFFF;
            return true;
        }
        sndId = 0;
        return false;
    }

    /// Sentinel a "sound channel" allocation writes into its caller's
    /// channel-pointer slot, so the `if (channel != 0)` guards in
    /// StartSoundFilePlay / DisposeSoundFileChannel pass. The value is
    /// never dereferenced — SndStartFilePlay/SndStopFilePlay are bridged.
    internal const int SoundChannelHandle = 0x5D63686E; // 'Schn'

    /// Public accessor for the 'Schn' sentinel — AllocSoundChannelControlBlock
    /// (FUN_10075c14, the mixer's NewPtrClear(0x424) SndChannel record) returns
    /// this instead of a raw Ptr.
    public static int MakeSoundChannelHandle() => SoundChannelHandle;
}
