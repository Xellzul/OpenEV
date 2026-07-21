using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50265-50322.
// Per-frame channel-layer upkeep: clear the Busy flag of every channel whose
// SndChannel reports idle, force-stop channels past their expiry tick, then either
// retry the pending single play (PendingSndHandle) or drain the queued-sound ring.
public static class TickSoundSubsystem
{
    public static void Run()
    {
        // Double-buffer pump (2 of exactly 2 pump sites — site 1 is
        // CountMatchingSoundVoices, polled by the title/dialog wait loops):
        // gameplay calls this every frame, so the Mac interrupt-time doubleback
        // cadence is replayed from here. See SoundMixer.PumpDoubleBuffer.
        SoundMixer.PumpDoubleBuffer();

        short count = SoundChannels.ChannelCount;
        for (short ch = 1; ch <= count; ch = (short)(ch + 1))
        {
            SoundChannels.ChannelState state = SoundChannels.Channels[ch - 1];
            if (state.Handle == 0)
            {
                state.Busy = 0;
            }
            else if (!IsChannelBusy.Run(state))
            {
                state.Busy = 0;
            }
            if (ch == short.MaxValue) break;
        }
        int nowTicks = (int)MacToolbox.TickCount();
        // Re-read — same ChannelCount cell as above (see SoundChannels).
        count = SoundChannels.ChannelCount;
        for (short ch = 1; ch <= count; ch = (short)(ch + 1))
        {
            SoundChannels.ChannelState state = SoundChannels.Channels[ch - 1];
            if (state.Busy != 0 && state.ExpiryTick < nowTicks)
            {
                ForceStopChannel.Run(ch);
            }
            if (ch == short.MaxValue) break;
        }
        if (SoundChannels.PendingSndHandle == 0)
        {
            if (0 < SoundQueueRing.Count)
            {
                DrainSoundQueue.Run();
            }
        }
        else
        {
            int channel = FindEvictableSoundChannel.Run(SoundChannels.PendingPriority);
            bool channelFound = channel != 0;
            if (channelFound)
            {
                PlaySoundOnChannel.Run(SoundChannels.PendingSndHandle,
                    SoundChannels.PendingPriority, channel,
                    SoundChannels.PendingImmediateParam);
            }
            if (channelFound || SoundChannels.RetryPendingWhenNoChannel == 0)
            {
                SoundChannels.PendingSndHandle = 0;
                SoundChannels.PendingPriority = 0;
            }
        }
        return;
    }
}
