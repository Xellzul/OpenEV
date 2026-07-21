using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 49950-50058.
// CHANNEL-layer play (the queued/pending path the ring + retry scratch feed; mixer
// one-shots go through EnqueueSoundVoice instead): allocate or reuse the Mac
// SndChannel in slot `channel` (1-based), silence it if still busy, push the snd's
// bufferCmd, compute the channel's busy-expiry tick from the RESOURCE size and the
// SoundHeader encoding, then mark the channel busy at the sound's priority.
// DEAD IN PRACTICE: both real callers (DrainSoundQueue.Run, TickSoundSubsystem's
// pending-retry branch) are themselves confirmed dead — the ring is never fed and
// the retry scratch is never set (see SoundQueueRing.cs / SoundChannels.cs) — so
// this body never actually executes in the running game.
public static class PlaySoundOnChannel
{
    // priority is stored into Channels[].PlayingPriority; volumeParam is the
    // stereo-volume word the ring/pending scratch carry into the volumeCmd below.
    public static void Run(int sndHandle, short priority, int channelArg, int volumeParam)
    {
        short channel = (short)channelArg;
        if (channel == 0)
        {
            return;
        }
        SoundChannels.ChannelState state = SoundChannels.Channels[channel - 1];
        if (state.Handle == 0)
        {
            // NO-OP: .glue::SndNewChannel(&Channels[ch-1].Handle, sampledSynth 5,
            // NewChannelInitFlags, FUN_1007700c userRoutine UPP). The port's shim
            // writes the 'Schn' sentinel and reports noErr — no real Sound
            // Manager channel exists behind it (this CHANNEL layer isn't
            // wired to real playback; the real one-shot audio path is
            // EnqueueSoundChannel/SoundMixer, see the class header above);
            // the userRoutine TVector (cell 0x10081a74) has no managed field
            // — see SoundChannels header — so 0 stands in below.
            short newChannelErr = MacToolbox.SndNewChannel(out state.Handle, 5,
                SoundChannels.NewChannelInitFlags, 0);
            if (newChannelErr != 0)
            {
                ReportSoundError.Run(newChannelErr);
                return;
            }
        }
        else
        {
            if (IsChannelBusy.Run(state))
            {
                SilenceChannel.Run(channel);
            }
        }
        if (state.Handle == 0)
        {
            return;
        }
        if (sndHandle == 0)
        {
            return;
        }
        // (*param_1 == 0) — null-master-pointer test on the snd handle. The port: a
        // handle the registry doesn't know has no block behind it — same bail-out.
        if (!SndResourceRegistry.TryGet(sndHandle, out SndResourceRegistry.DecodedSnd decoded))
        {
            return;
        }
        if (SoundChannels.HardwareStereoCapable && SoundChannels.DrainEnabled)
        {
            // NO-OP: SndCommand {volumeCmd 46, 0, volumeParam} passed by address —
            // sets the channel's stereo volume for this play. No 3-scalar
            // SndDoImmediate overload exists in MacToolbox to carry volumeParam
            // through, so this shim call drops it (harmless: the shim ignores every
            // arg and this whole function never runs — see the class header).
            MacToolbox.SndDoImmediate(state.Handle, 46);
        }
        // local_24 = FUN_100764b0(sndHandle) — a SoundHeader pointer computed by
        // walking past the format-1 modifier list (if any) and all numCommands
        // command entries. NOT independently re-derived by this port: the decoded
        // record's header fields instead come from FindSoundCommandData's search
        // for the bufferCmd/soundCmd's own embedded param2 offset (cached at load
        // time in SndResourceRegistry). ASSUMPTION, not decompile-proven: for a
        // resource with exactly one bufferCmd and the SoundHeader immediately
        // following it (the standard simple-sampled-sound layout EVO's assets use),
        // both computations land on the same header — but the two algorithms are
        // structurally different and this equivalence is not verified byte-for-byte
        // against the ASM. Moot in practice: this function never executes (see the
        // class header), so no live behavior depends on it either way.
        // NO-OP: SndCommand {bufferCmd 81, 0, soundHeaderPtr} queues the sample
        // playback in the original; soundHeaderPtr (local_24 above) has no live
        // equivalent in this port's architecture, so it's not threaded through this
        // shim call either.
        int sndErr = MacToolbox.SndDoCommand(state.Handle, 81, false);
        if ((short)sndErr != 0)
        {
            ReportSoundError.Run((short)sndErr);
            return;
        }
        // uVar9 = *(ushort*)(soundHeader + 8) — the INTEGER half of the Fixed
        // sample rate (0x56EE.8BA3 -> 22254). 0 is clamped to 1.
        int sampleRate = (ushort)((uint)decoded.SampleRateFixed >> 16);
        if (sampleRate == 0)
        {
            sampleRate = 1;
        }
        if (decoded.EncodeByte == 0xfe) // header+0x14 == -2: cmpSH compressed header
        {
            short compressionId = decoded.CompressionId;
            if (compressionId == 3) // MACE 3:1
            {
                state.ExpiryTick = ComputeExpiryTick(decoded.ResourceLength, 180, sampleRate);
            }
            else
            {
                if (compressionId < 3)
                {
                    if (compressionId == 0) // cmpSH but not compressed
                    {
                        state.ExpiryTick = ComputeExpiryTick(decoded.ResourceLength, 60, sampleRate);
                        goto LAB_10076880;
                    }
                }
                else if (compressionId == 6) // MACE 6:1
                {
                    state.ExpiryTick = ComputeExpiryTick(decoded.ResourceLength, 360, sampleRate);
                    goto LAB_10076880;
                }
                // ORIGINAL QUIRK (kept): unknown compression reports -43
                // (0xffffffd5) but does NOT return — the channel is still marked
                // busy below with a STALE ExpiryTick from its previous play.
                ReportSoundError.Run(-43);
            }
        }
        else // stdSH / extSH
        {
            state.ExpiryTick = ComputeExpiryTick(decoded.ResourceLength, 60, sampleRate);
        }
    LAB_10076880:
        state.Busy = 1;
        state.PlayingPriority = priority;
        // NO-OP: SndCommand {waitCmd 10, param1 1, 0} queues a one-tick wait behind
        // the buffer in the original; 4-scalar shim call is a no-op here.
        MacToolbox.SndDoCommand(state.Handle, 10, 1, 0);
        return;
    }

    // Shared ExpiryTick formula for the four codec branches above (EV
    // Override-11.c ~49996-50026) — differs only by the per-byte tick cost:
    // 60 (uncompressed / stdSH-extSH), 180 (MACE 3:1), 360 (MACE 6:1).
    private static int ComputeExpiryTick(int resourceLength, int multiplier, int sampleRate) =>
        (int)MacToolbox.TickCount() + (resourceLength * multiplier) / sampleRate;
}
