using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 50475-50490.
// The decompile's `int in_r11` is not uninitialized: the ASM shows caller
// RunMultiButtonModalDialog does `mr r11, r1` — r11 is the caller's own stack
// pointer, and +0x40/+0x44 of that frame are where its GetPort/GetGDevice
// results were stashed before the fallible NewHandle/NewDialog call. So this
// restores the caller's saved GrafPtr/GDevice before ExitToShell.
public static class FatalAlertExit
{
    public static void Run(int savedGrafPtr, int savedGDevice)
    {
        MacToolbox.SysBeep(1);
        MacToolbox.SetPort(savedGrafPtr);
        MacToolbox.SetGDevice(savedGDevice);
        MacToolbox.ExitToShell();
        return;
    }
}
