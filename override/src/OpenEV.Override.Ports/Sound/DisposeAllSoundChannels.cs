using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10076c58 (EV Override-11.c lines 50188-50225).
// Teardown sweep over the channel layer: SndDisposeChannel every allocated slot,
// then zero all Handles, then zero all Busy flags (three separate ChannelCount
// walks, exactly as the original), and finally drop the pending play.
public static class DisposeAllSoundChannels
{
    public static void Run()
    {
        short channelCount = SoundChannels.ChannelCount;
        short index = 1;
        while (true)
        {
            if (channelCount < index) break;
            if (SoundChannels.Channels[index - 1].Handle != 0)
            {
                MacToolbox.SndDisposeChannel(SoundChannels.Channels[index - 1].Handle, true);
            }
            if (index == short.MaxValue) break;
            index = (short)(index + 1);
        }
        // Plain re-reads of ChannelCount, matching the decompile's *psVar4 re-reads.
        channelCount = SoundChannels.ChannelCount;
        for (index = 1; index <= channelCount; index = (short)(index + 1))
        {
            SoundChannels.Channels[index - 1].Handle = 0;
            if (index == short.MaxValue) break;
        }
        channelCount = SoundChannels.ChannelCount;
        for (index = 1; index <= channelCount; index = (short)(index + 1))
        {
            SoundChannels.Channels[index - 1].Busy = 0;
            if (index == short.MaxValue) break;
        }
        SoundChannels.PendingSndHandle = 0;
    }
}
