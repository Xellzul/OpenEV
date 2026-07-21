using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10054efc (EV Override-11.c lines 34804-34810). ASM: loc_54EFC,
// reference/disasm/_code_interstitial.asm orig lines 105698-105713 (NOT the
// 105579-105697 range of the neighboring FUN_10054db0 split file — that's a
// different function; this one has no split file of its own).
public static class TeardownAudioForExit
{
    public static void Run()
    {
        TeardownSoundSubsystem.Run();
        TearDownSavedPalette.Run();
    }
}
