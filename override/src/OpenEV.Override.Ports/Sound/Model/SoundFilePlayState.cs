namespace OpenEV.Override.Ports.Sound.Model;

// Managed home for the FILE-MUSIC (SndStartFilePlay) state plus the speech and
// ambient-chatter scratch — was the pointer cells 0x10081070..0x10081088 (each
// holding a PEF-relocated pointer to a BSS cell near 0x100e0e6c..0x100e0e94) and
// the standalone cells 0x1008221c / 0x10080ee4. The BSS targets in the cell band
// [0x10081070, 0x1008108c) interleave with the boot-
// progress-bar buffers (cell 0x10081090 -> rect 0x100e0e6e..75, cells
// 0x100810a0/a4 -> the BootProgress doubles at 0x100e0e78/80 — verified against
// the raw data segment, tools/dump_dataseg.py). The bar side is ALSO managed
// (Graphics.Model.BootProgress.BarRect); the only bytes in this interleaved
// region left unaccounted for are the 2 unidentified ones at 0x100e0e76/77.
//
// Cell map (cell -> BSS target -> field here), verified against the FUNs:
//   0x10081088 -> 0x100e0e88  FileMusicChannel ('Schn' sentinel in the port)
//   0x10081080 -> 0x100e0e94  PairPrimaryHandle — the loaded snd 30001 (the
//                             audible credits track; a full buffer Ptr, not a
//                             one-byte "Active" flag — don't shrink the field)
//   0x10081084 -> 0x100e0e6c  PairMusicStarted — LoadAndStartSoundPair writes
//                             int 1. ORIGINAL QUIRKS: no reader exists in the
//                             binary, AND the 4-byte write's upper half lands
//                             on the boot-bar rect's .top at 0x100e0e6e (the
//                             ORIGINAL PEF layout aliases them; the port's managed
//                             field doesn't replicate the clobber)
//   0x1008107c -> 0x100e0e8c  FileMusicSwapHandle — the loaded snd 30002 the
//                             loop re-arms with (the snd queued to follow)
//   0x10081070 -> 0x100e0e90  FileMusicSpareBuffer (third disposable buffer;
//                             StopAndDisposeSoundPair frees it. ORIGINAL QUIRK:
//                             no writer in the binary — stays 0 forever, the
//                             dispose branch is dead)
//   0x10081074 -> TVector FUN_100425b0 — the file-play completion that re-arms
//                 the SwapRequest with ITSELF as completion (the music LOOP);
//                 the port: SoundCallback.Completion wired as the delegate, no field.
//   0x10081078 -> 0x1008a71c  unused pointer to the SwapRequest record (no
//                             reader in the binary)
//   0x1008221c             SpeechChannelHandle (SpeakText / SpeakPersHailLine)
//   0x10081204 -> byte*    SpeechAvailable (DetectSpeechSupport writes 0 then 1
//                          when Gestalt reports a TTS Manager with voices; the
//                          second write went through GameToc-0x745c = the SAME
//                          cell. SpeakText gates on it. Both consumers managed)
//   0x10080ee4 -> short*   QueuedAmbientBank (-1 = none; SetActiveChatterSpeaker
//                          writes, TickAmbientSoundChannel consumes; toc[-0x1ddf];
//                          all four consumers managed)
public static class SoundFilePlayState
{
    public static int FileMusicChannel;
    public static int PairPrimaryHandle;
    public static int PairMusicStarted;
    public static int FileMusicSwapHandle;
    public static int FileMusicSpareBuffer;

    // The file-music swap play request — the static record at 0x1008a71c..0x1008a736.
    // SoundCallback (FUN_100425b0) fills it {SwapHandle, rate 1.0, completion =
    // itself, priority 0x32, volumes 0x80} and enqueues it on each loop pass.
    public static readonly SoundPlayRequest SwapRequest = new();

    // ORIGINAL QUIRK: nothing in the binary ever writes the speech-channel cell
    // nonzero — SpeakText goes through SpeakString, never NewSpeechChannel — so
    // the Dispose branch that reads it is dead code; kept faithful.
    public static int SpeechChannelHandle;
    public static byte SpeechAvailable;      // 0 until DetectSpeechSupport finds a TTS Manager
    public static short QueuedAmbientBank;   // LoadAllUiSoundEffects inits to -1
}
