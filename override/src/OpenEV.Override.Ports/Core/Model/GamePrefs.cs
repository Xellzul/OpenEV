namespace OpenEV.Override.Ports.Core.Model;

// Typed managed home for the GAME PREFS scalar band — the head of the Mac
// PrefsRecord, BSS 0x1008a550..0x1008a557 (GameToc+0x1ef0..0x1ef7). Written by
// DefaultGamePrefs (FUN_1005484c), the boot prefs-load (FUN_10019f88 /
// ApplyDefaultPrefsToMemory, wired at GameBootSequence step 7) and the Set Prefs
// dialog OK branch (FUN_10044480 / PrefsDialogInit); persisted by
// WritePrefsToDisk (FUN_1001a3b8) into the 'Mp¨Ä' id-0x80 resource blob. Each
// flag byte widens to a big-endian short in the blob — the ON-DISK layout is
// unchanged by this migration, only the in-memory home moved.
//
// Cell map (old address ↔ field ↔ blob offset ↔ meaning/evidence):
//   0x1008a550  IntroMusicEnabled          blob+0x02  "Intro Music" DLOG 4001 item 0x28; gates
//                                                     StartSoundFilePlay (title music),
//                                                     LoadAndStartSoundPair (credits music),
//                                                     StopAndDisposeSoundPair and the
//                                                     DisposeSoundFileChannel beep. Default 1.
//   0x1008a551  PrefByte551                blob+0x04  default 1; loaded (FUN_10019f88) and saved
//                                                     (WritePrefsToDisk) as a 0/1 byte, and
//                                                     snapshot→restored UNCHANGED across the Set
//                                                     Prefs dialog (FUN_10044480, read at 28412 /
//                                                     write-back at 28487). No dialog item toggles
//                                                     it and NOTHING reads it to gate behavior — a
//                                                     vestigial/reserved pref slot whose original
//                                                     meaning is unrecoverable from this binary.
//                                                     Faithfully kept as a raw 0/1 byte.
//   0x1008a552  GfxDetailFlag           (not in blob)  zeroed by DefaultGamePrefs and by every
//                                                     prefs load (FUN_10019f88 clears it before
//                                                     the version check); read `!= 0` by the 8
//                                                     sprite-tick detail gates (UpdateShipSlot-
//                                                     Tick, TickCarriedSprite, TickExplosion-
//                                                     Sprite, TickStreakSprite, TickBackground-
//                                                     NebulaSprite, TickDockingRing, TickEscort-
//                                                     Tractor, TickProjectile → FUN_10060094).
//   0x1008a553  QuickTimeMoviesDisabled    blob+0x62  "QuickTime Movies" item 0x29, INVERTED
//                                                     checkbox (byte 0 = movies play);
//                                                     PlayQuickTimeMovie runs only when 0.
//                                                     Default 0.
//   0x1008a554  UseQuickdraw               blob+0x64  "Use Quickdraw" item 2; RunMainGameLoop
//                                                     passes `== 0` into
//                                                     UpdateWindowRegionLayout. Default 0.
//   0x1008a555  ProjectileStreaksDisabled  blob+0x66  dead toggle — FUN_10044480 flips it on
//                                                     item 0x2b (= DITL item 43, which DITL
//                                                     4001 does not contain); gate read by
//                                                     SpawnProjectileStreak. Default 0.
//   0x1008a556  MasterVolume (short)       blob+0x06  0..8; hardware volume = MasterVolume << 5
//                                                     (SetMasterVolume / FUN_10074ddc, in
//                                                     GWorldPort foregrounding + RunGameSession-
//                                                     Launcher ×2 + the dialog test-beep path);
//                                                     TickShipAI's combat alarm adds a HUD
//                                                     blink when < 2. Default 3.
//
// Mac semantics kept: the flags are 0/1 BYTES (the blob write widens them to
// shorts on save and the load narrows back), not C# bools — width-faithful to
// the decompile's char cells.
public static class GamePrefs
{
    public static byte IntroMusicEnabled;          // DAT_1008a550
    public static byte PrefByte551;                // cRam/uRam1008a551
    public static byte GfxDetailFlag;              // cRam/uRam1008a552
    public static byte QuickTimeMoviesDisabled;    // cRam/uRam1008a553
    public static byte UseQuickdraw;               // cRam/uRam1008a554
    public static byte ProjectileStreaksDisabled;  // cRam/uRam1008a555
    public static short MasterVolume;               // sRam/uRam1008a556

    // The Set Prefs dialog's WORKING master-volume copy. In the binary this is
    // a BSS short reached two ways: `psVar5 = _DAT_100810d8` (PEF pointer cell,
    // FUN_10044480's only use) and `**(short**)(toc-0x7588)` in the redraw
    // FUN_10044ef4 — the same target. The port had backed it with the title-scratch
    // short 0x1020205a (old PrefsMemory.VolumeCell); the pointer cell, the toc
    // slot and the scratch short are all gone now. Dialog open copies
    // MasterVolume in; the OK branch copies it back.
    public static short DialogWorkingVolume;
}
