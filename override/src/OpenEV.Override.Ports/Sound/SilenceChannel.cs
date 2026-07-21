using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_100763c0 (EV Override-11.c lines 49874-49903).
// The channel layer's stop-current-sound: flushCmd (4) then quietCmd (3) on the
// slot's SndChannel, then clear its Busy flag. PlaySoundOnChannel calls it
// before reusing a still-busy channel.
public static class SilenceChannel
{
    public static void Run(short channel)
    {
        SoundChannels.ChannelState state = SoundChannels.Channels[channel - 1];
        if (state.Handle != 0)
        {
            // SndCommand {flushCmd 4, 0, 0} / {quietCmd 3, 0, 0} passed by address;
            // the no-op shim takes the command id in the (unread) cmdPtr arg for
            // line-mapping.
            int sndError = MacToolbox.SndDoImmediate(state.Handle, 4);
            if ((short)sndError != 0)
            {
                ReportSoundError.Run((short)sndError);
            }
            sndError = MacToolbox.SndDoImmediate(state.Handle, 3);
            if ((short)sndError != 0)
            {
                ReportSoundError.Run((short)sndError);
            }
            state.Busy = 0;
        }
        return;
    }
}
