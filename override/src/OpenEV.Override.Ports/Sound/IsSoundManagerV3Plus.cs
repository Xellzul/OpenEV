namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49467-49481.
//
// Original: FUN_10075c4c(0xA800 _SoundDispatch trap available?) then
// SndSoundManagerVersion -> 1 when the NumVersion major byte >= 3, else 0.
// PORT HONEST: the game shipped on (and the boot/mixer paths assume) Sound
// Manager 3.1+, and the port has no trap table to interrogate — report 3+
// unconditionally. Sole caller: InitSoundMixerState, which keys
// SoundMixer.UseHardwareVolume off this.
public static class IsSoundManagerV3Plus
{
    public static bool Run() => true;
}
