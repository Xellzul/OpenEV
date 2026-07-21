using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 34009-34065. Boot step 25 (GameBootSequence).
// Decodes all the UI/combat 'snd ' resources (FUN_10075450 -> decoded-buffer pointer)
// into the MANAGED CombatSoundCells/SoundResourceCells fields, fills the
// boarding-chime play request, and probes the 4 ambient sfx banks for their
// per-bank loaded counts.
public static class LoadAllUiSoundEffects
{
    // Per-bank probe-slot count (snd ids 700..708 relative to each bank's base).
    private const int AmbientBankProbeSlots = 9;

    public static void Run()
    {
        SoundResourceCells.BoardingChimeSnd = LoadSndResource.Run(128);
        SoundResourceCells.UiChimeSnd = LoadSndResource.Run(130);

        // Boarding-chime play request (see SoundMixer.BoardingChimeRequest and
        // SoundPlayRequest for the record layout). Id and Refcon are never
        // written anywhere in the binary; they stay 0.
        SoundPlayRequest chime = SoundMixer.BoardingChimeRequest;
        chime.SndHandle = SoundResourceCells.BoardingChimeSnd;
        chime.RateFixed = 0x10000;                                  // Fixed 1.0
        chime.CompletionProc = TriggerBoardingAlarmOnce.Completion;
        chime.Priority = 32000;
        chime.LeftVolume = 128;
        chime.RightVolume = 128;
        for (int i = 0; i < CombatSoundCells.UiSoundBankA.Length; i++)
        {
            CombatSoundCells.UiSoundBankA[i] = LoadSndResource.Run(i + 150);
        }
        for (int i = 0; i < CombatSoundCells.WeaponSoundTable.Length; i++)
        {
            CombatSoundCells.WeaponSoundTable[i] = LoadSndResource.Run(i + 200);
        }
        for (int i = 0; i < CombatSoundCells.WeaponHitSnd.Length; i++)
        {
            CombatSoundCells.WeaponHitSnd[i] = LoadSndResource.Run(i + 300);
        }
        SoundResourceCells.DeathCountdownSnd = LoadSndResource.Run(350);
        CombatSoundCells.ScanSweepSnd = LoadSndResource.Run(360);
        CombatSoundCells.AlarmSnd = LoadSndResource.Run(370);
        SoundResourceCells.BoardingDialogChimeSnd = LoadSndResource.Run(390);
        SoundResourceCells.CloakDisengageSnd = LoadSndResource.Run(340);
        SoundResourceCells.CloakEngageSnd = LoadSndResource.Run(341);
        SoundResourceCells.DynamicSoundBuffer = 0;
        SoundFilePlayState.QueuedAmbientBank = -1;

        // Probe the 4 ambient sfx banks (snd 700+bank*10+i): count how many decode.
        for (short bank = 0; bank < SoundResourceCells.UiSfxBankLoadedCount.Length; bank = (short)(bank + 1))
        {
            SoundResourceCells.UiSfxBankLoadedCount[bank] = 0;
            for (int i = 0; i < AmbientBankProbeSlots; i++)
            {
                int soundPtr = LoadSndResource.Run(bank * 10 + i + 700);
                if (soundPtr == 0) break;
                MacToolbox.DisposePtr(soundPtr);
                SoundResourceCells.UiSfxBankLoadedCount[bank] = (short)(SoundResourceCells.UiSfxBankLoadedCount[bank] + 1);
            }
        }
        MacToolbox.MaxMem(); // grow-zone probe; out-param never read
    }
}
