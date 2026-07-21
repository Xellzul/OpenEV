using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Port of FUN_100768fc (EV Override-11.c lines 50064-50087).
// Picks a CHANNEL NUMBER (1-based, 0 = none) for a sound of the given priority:
// scan the non-Reserved channels; the first unallocated-or-idle one wins
// outright, otherwise track the busy channel playing at the LOWEST priority
// strictly below the request — that one is evictable.
public static class FindEvictableSoundChannel
{
    public static int Run(short priority)
    {
        short resultChannel = 0;
        short bestChannel = 0;
        for (short channelIndex = 1; channelIndex <= SoundChannels.ChannelCount; channelIndex = (short)(channelIndex + 1))
        {
            // C comma-operator init in the original for-condition.
            resultChannel = bestChannel;
            SoundChannels.ChannelState state = SoundChannels.Channels[channelIndex - 1];
            if (state.Reserved == 0)
            {
                resultChannel = channelIndex;
                if (state.Handle == 0 || state.Busy == 0) break; // free channel — take it
                if (state.PlayingPriority < priority)
                {
                    priority = state.PlayingPriority;
                    bestChannel = channelIndex;
                }
            }
            resultChannel = bestChannel;
            if (channelIndex == short.MaxValue) break; // sVar2 == 0x7fff loop-overflow guard
        }
        return resultChannel;
    }
}
