using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10025060 (EV Override-11.c lines 15956-15966), called once per frame
// from RunMainGameLoop to copy the game-speed scale into the physics time scale.
public static class CopyCpuSpeedScaleToTimeScale
{
    public static void Run()
    {
        // The scale is set at boot by the prefs loader (FUN_10019f88): the CPU
        // benchmark (RunCpuSpeedBenchmark) on the no-prefs path, or the saved
        // game-speed pref on the happy path — both land in CpuSpeedScale
        // (0x100e0200), the same cell as PrefsDialogState.GameSpeed.
        WorldState.TimeScale = WorldState.CpuSpeedScale;
    }
}
