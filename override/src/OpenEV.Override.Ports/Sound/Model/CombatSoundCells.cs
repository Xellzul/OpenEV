namespace OpenEV.Override.Ports.Sound.Model;

// MANAGED home for the combat/UI decoded-sound pointer cells (loaded by
// LoadAllUiSoundEffects from 'snd ' resources via FUN_10075450). Each field holds
// the decoded-sound-buffer pointer that the original kept in a 4-byte BSS cell;
// the old cell band [0x1008a5b4, 0x1008a700) is retired (see
// Misc/OriginalGameStateTotalBytes).
// The rest of the original band (BoardingChimeSnd, UiChimeSnd, DynamicSoundBuffer,
// etc.) lives on SoundResourceCells — see Model/SoundSubsystemCells.cs.
public static class CombatSoundCells
{
    public static int AlarmSnd;       // snd 0x172, low-armor/alarm chime (was 0x1008a5bc)
    public static int ScanSweepSnd;   // snd 0x168 (was 0x1008a6f0)

    // UiSoundBankA: snd 150..154 (was 0x1008a5c0 stride 4). [4] is REPLACED by the
    // prefs volume-test snd 0x9a while the prefs dialog runs (PrefsMemory).
    public static readonly int[] UiSoundBankA = new int[5];

    // Per-weapon fire sounds, snd 200..263 (was 0x1008a5d4 stride 4); indexed by
    // weapon.FireSound (Combat.Model.WeaponRecord +0x0e — a resource-authored id,
    // NOT the same count as Combat.Model.WeaponTable.Count, which coincidentally
    // is also 64; LoadAllUiSoundEffects' own fill loop still hardcodes this count
    // independently rather than referencing this const).
    public const int FireSoundSlotCount = 64;
    public static readonly int[] WeaponSoundTable = new int[FireSoundSlotCount];

    // Weapon-impact sounds, snd 300..303 (was 0x1008a6d4 stride 4).
    // [2] = ship break-up crackle, [3] = ship death boom (UpdateShipSlotTick);
    // [0] is also the sys-beep snd (DisposeSoundFileChannel).
    public static readonly int[] WeaponHitSnd = new int[4];
}
