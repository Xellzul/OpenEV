using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075dbc (EV Override-11.c lines 49450-49463).
//
// Probe the hardware output sample rate: SndNewChannel(probe, sampledSynth 5,
// no init flags) -> SndGetInfo(chan, 'srat' 0x73726174, &rateOut) ->
// SndDisposeChannel. Sole caller: BootSoundSubsystem (FUN_10074af0) passes
// &HardwareSampleRateFixed after seeding it 0x56ee8ba3 (22254.5454 Hz).
//
// PORT HONEST: the SndGetInfo shim reports noErr and leaves the destination
// UNTOUCHED — there is no Mac output hardware to ask, and the mixer's rate
// maths are calibrated to the 0x56ee8ba3 boot default, which must (and does)
// survive the probe. `sampleRateFixedOut` is therefore UNUSED below (the
// original threaded its address all the way into SndGetInfo's out-pointer;
// the port passes 0 instead) — kept as a parameter only to mirror the original call
// shape for the sole caller.
public static class ProbeSoundChannelSampleRate
{
    public static short Run(ref int sampleRateFixedOut)
    {
        // The original passed no userRoutine; the out-overload's 4th arg is 0.
        short err = MacToolbox.SndNewChannel(out int probeChannel, 5, 0, 0);
        if (err == 0)
        {
            err = MacToolbox.SndGetInfo(probeChannel, 0x73726174, 0);   // 'srat' — rate left untouched
            MacToolbox.SndDisposeChannel(probeChannel, true);
        }
        return err;
    }
}
