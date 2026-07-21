using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 45275-45285.
public static class SetActiveChatterSpeaker
{
    public static void Run(short speakerId)
    {
        // The queued-ambient-bank cell; TickAmbientSoundChannel consumes it.
        SoundFilePlayState.QueuedAmbientBank = speakerId;
        return;
    }
}
