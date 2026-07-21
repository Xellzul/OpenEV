namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49858-49868.
// Forwards a Sound Manager error code to the installed handler, when one is
// set. The binary never installs one (InitSoundSubsystem only ever nulls the
// cell), so this stays a no-op — see SoundProcs.ErrorHandlerProc.
public static class ReportSoundError
{
    public static void Run(short errorCode) => SoundProcs.ErrorHandlerProc?.Invoke(errorCode);
}
