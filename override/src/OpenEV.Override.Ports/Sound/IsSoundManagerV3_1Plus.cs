namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49485-49502.
//
// Original: FUN_10075c4c(0xA800 _SoundDispatch trap available?) then
// SndSoundManagerVersion -> 1 when major > 3, or major == 3 with the BCD
// minor byte > 0xf (i.e. >= 3.1), else 0. PORT HONEST: the game shipped on
// Sound Manager 3.1+ and the port has no trap table — report 3.1+ unconditionally.
// Sole caller: BootSoundSubsystem, whose hardware-rate probe branch (the
// 0x56ee8ba3 seed) depends on this returning true.
public static class IsSoundManagerV3_1Plus
{
    public static bool Run() => true;
}
