using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50141-50182.
// One-shot channel-layer init, gated on ChannelCount < 1 (it stays 1 afterwards):
// raise DrainEnabled, probe Gestalt('snd ') for stereo hardware, zero all 16
// channel slots, default the SndNewChannel init flags, drop the error handler,
// and reset the queue ring + pending scratch.
public static class InitSoundSubsystem
{
    public static void Run()
    {
        if (SoundChannels.ChannelCount < 1)
        {
            SoundChannels.DrainEnabled = true;
            SoundChannels.UseChannelStatusFlag = 0;
            SoundChannels.QueueResetFlag = 0;
            // Gestalt('snd ' 0x736e6420) bit0 (gestaltStereoCapability) ->
            // HardwareStereoCapable. B4: routed through the honest
            // GestaltSoundAttrs shim shared with BootSoundSubsystem (bit0 SET —
            // the host engine is stereo-capable).
            short gestaltErr = MacToolbox.GestaltSoundAttrs(out uint soundAttrs);
            if (gestaltErr == 0)
            {
                SoundChannels.HardwareStereoCapable = (soundAttrs & (uint)SoundGestaltAttrs.StereoCapability) != 0;
            }
            foreach (var state in SoundChannels.Channels)
            {
                state.Handle = 0;
                state.Busy = 0;
                state.Reserved = 0;
            }
            SoundChannels.ChannelCount = 1;
            if (SoundChannels.NewChannelInitFlags == 0)
            {
                SoundChannels.NewChannelInitFlags = (int)(SndChannelInitFlags.Mono | SndChannelInitFlags.NoInterp);
            }
            SoundProcs.ErrorHandlerProc = null;
            // ORIGINAL double-zero quirk (kept): the Busy band is zeroed a second
            // time right after the combined wipe above.
            foreach (var state in SoundChannels.Channels)
            {
                state.Busy = 0;
            }
            SoundChannels.FlagA48 = 0;
            ResetSoundQueueIndices.Run();
            SoundChannels.PendingSndHandle = 0;
        }
        return;
    }
}
