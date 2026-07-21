namespace OpenEV.Override.Ports.Sound.Model;

// /Sound de-raw -- the sound subsystem's remaining cell consts.
//
// The rest of the original sound-subsystem cell bands (mixer/channel/queue/file-music
// pointers, the interrupt-mask and error-handler cells, the file-music swap-request
// record, the intro-music prefs byte) migrated to SoundMixer / SoundChannels /
// SoundQueueRing / SoundProcs / SoundFilePlayState / Core.Model.GamePrefs
// .IntroMusicEnabled respectively — see each class's own cell-map comment.
//
// The old "SndSampleFormatWord0-3" cells (0x1008123c..48) were MISNAMED sprite-node UPP
// token cells — live SPRITE data, now Misc.SpriteNodeUppCells.

// MANAGED decoded-sound pointer cells (the non-combat half of the band; loaded by
// LoadAllUiSoundEffects). Old cell addresses in comments — see CombatSoundCells for
// the full original cell map.
public static class SoundResourceCells
{
    public static int BoardingChimeSnd;     // snd 0x80 (was 0x1008a5b4) — hyperspace-windup chime loop
    public static int UiChimeSnd;           // snd 0x82 (was 0x1008a5b8)
    public static int BoardingDialogChimeSnd; // snd 0x186 (was 0x1008a6e8) — boarding chime
    // snd 0x15e (was 0x1008a6ec) — the ship self-destruct countdown loop (TickShipAI,
    // gated on DeathTimer). Also reused, once, as PlayVictoryAnimation's opening cue —
    // an unrelated ending/easter-egg trigger, not tied to any ship's death; the name
    // reflects the dominant combat use only.
    public static int DeathCountdownSnd;
    public static int CloakDisengageSnd;    // snd 0x154 (was 0x1008a6f4) — cloak disengage
    public static int CloakEngageSnd;       // snd 0x155 (was 0x1008a6f8) — cloak engage
    public static int DynamicSoundBuffer;   // (was 0x1008a6fc) ambient channel decoded buffer, 0 = none

    // The sys-beep snd (DisposeSoundFileChannel, GameToc+0x2074 = 0x1008a6d4) is the
    // SAME cell as WeaponHitSnd[0] — alias, single source of truth.
    public static int BeepSnd => CombatSoundCells.WeaponHitSnd[0];

    // Per-bank loaded-sound counts for the 4 ambient sfx banks (was &DAT_1008f76a short[4]).
    public static readonly short[] UiSfxBankLoadedCount = new short[4];

    // The boarding-chime request record, queued-ambient-bank index, speech-channel
    // handle, and speech-available flag now live on SoundMixer.BoardingChimeRequest and
    // SoundFilePlayState.QueuedAmbientBank / .SpeechChannelHandle / .SpeechAvailable
    // respectively.
    //
    // The old SoundSpawnGWorldSlot (0x1008cca8) / SoundSpawnPixMapSlot (0x1008cc58) were
    // MISNAMED sprite-frame cells — now Combat.Model.SpriteFrameTables.Spin801Frames[0] /
    // .Spin800Frames[0].
}
