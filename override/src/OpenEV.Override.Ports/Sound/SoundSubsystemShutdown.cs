namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10076e24 (EV Override-11.c lines 50253-50259) — the CHANNEL-layer
// shutdown (channel array + queue indices). The mixer teardown / hw-volume
// restore is the separate TeardownSoundSubsystem (FUN_10074d48).
public static class SoundSubsystemShutdown
{
    public static void Run()
    {
        DisposeAllSoundChannels.Run();
        ResetSoundQueueIndices.Run();
    }
}
