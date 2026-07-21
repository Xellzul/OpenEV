using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10076528 (EV Override-11.c lines 49923-49944).
// Asks whether the channel's Mac SndChannel HANDLE is still playing (the ASM's
// param_1 is the raw Handle, not a slot number — takes the ChannelState here so
// callers don't unwrap-then-repass .Handle; the Handle itself still forwards
// into the toolbox call below): either via the SndChannelStatus trap (when
// UseChannelStatusFlag is set) or by peeking the channel record's qHead at
// +0x20 (-1 = command queue empty = idle).
//
// RESTORED (was hardcoded `isBusy = false` in the only reachable branch):
// The port's channels are 'Schn' sentinels with NO record behind them — every
// allocated channel shares the exact same sentinel int
// (MacToolbox.SoundChannelHandle), so there is no per-channel memory at
// "handle+0x20" to peek, and the real qHead signal is genuinely unavailable
// (playback is host-bridged; there is no software mixer queue). Faithful
// substitute: ChannelState.Busy/ExpiryTick — already the port's own stand-in
// for "how long this channel's queued commands take to drain", computed by
// PlaySoundOnChannel from the sound's REAL decoded duration, and already
// trusted by TickSoundSubsystem's ForceStopChannel cleanup path for the same
// question. Busy-and-not-yet-expired reproduces "qHead != -1".
public static class IsChannelBusy
{
    public static bool Run(SoundChannels.ChannelState state)
    {
        bool isBusy;

        if (SoundChannels.UseChannelStatusFlag == 0)
        {
            // *(short*)(channel + 0x20) != -1 — the SndChannel record's real qHead
            // read in the original. UseChannelStatusFlag has exactly one writer in
            // the whole binary (decompile line 50157; InitSoundSubsystem here) and
            // it always sets 0, so THIS branch is the only one ever taken, in both
            // the original and the port — not a rare fallback.
            // No real record exists behind the 'Schn' sentinel, so read the
            // channel's own Busy/ExpiryTick fields instead (see header ).
            isBusy = state.Busy != 0 && MacToolbox.TickCount() < state.ExpiryTick;
        }
        else
        {
            // Dead in the ORIGINAL binary too, not only a port simplification:
            // UseChannelStatusFlag's one writer always sets 0, so this
            // SndChannelStatus-trap path never ran in the shipping game either —
            // preserved bug-for-bug (Rule 11) rather than deleted.
            // auStack_26 was a 12-byte SCStatus record written by SndChannelStatus;
            // local_1a (the byte right after it, record offset 0xC) = scChannelBusy field.
            // Use the 1-arg overload that returns (ok, isBusy) directly.
            var status = MacToolbox.SndChannelStatus(state.Handle);
            if (status.ok)
            {
                isBusy = status.isBusy != 0;
            }
            else
            {
                // qHead fallback — same Busy/ExpiryTick substitute as above.
                isBusy = state.Busy != 0 && MacToolbox.TickCount() < state.ExpiryTick;
            }
        }
        return isBusy;
    }
}
