using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10076d70 (EV Override-11.c lines 50231-50249).
// Hard-stops one channel slot (1-based, bounds-checked against ChannelCount):
// dispose its SndChannel if allocated, then clear the Handle and Busy flag.
// TickSoundSubsystem calls this when a busy channel passes its ExpiryTick.
public static class ForceStopChannel
{
    public static void Run(short channel)
    {
        if (channel <= SoundChannels.ChannelCount && 0 < channel)
        {
            SoundChannels.ChannelState state = SoundChannels.Channels[channel - 1];
            if (state.Handle != 0)
            {
                MacToolbox.SndDisposeChannel(state.Handle, true);
            }
            state.Handle = 0;
            state.Busy = 0;
        }
        return;
    }
}
