using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 48786-48807.
// Reports the master volume: 0 when the mixer isn't booted; in hardware-volume
// mode the L/R average of the volume BootSoundSubsystem captured via
// GetDefaultOutputVolume, clamped to 0x100; otherwise a 0..7 speaker-scale value.
public static class GetMasterVolume
{
    public static uint Run()
    {
        if (!EvoGlobals.IsSoundSubsystemBooted)
        {
            return 0;
        }
        if (SoundMixer.UseHardwareVolume)
        {
            // (L>>16 + R) of the saved hardware volume, signed-halved with the PPC
            // round-toward-zero idiom, clamped to unity 0x100.
            var hardwareVolume = (uint)((SoundMixer.SavedHardwareVolume >> 0x10) + (SoundMixer.SavedHardwareVolume & 0xffff));
            hardwareVolume = (uint)(((int)hardwareVolume >> 1) + ((int)hardwareVolume < 0 && (hardwareVolume & 1) != 0 ? 1 : 0));
            if (0x100 < (hardwareVolume & 0xffff))
            {
                hardwareVolume = 0x100;
            }
            return hardwareVolume;
        }
        // ORIGINAL QUIRK (kept): the software branch ALSO reads SavedHardwareVolume
        // (0x1008241c) — NOT the SoftwareVolume cell (0x1008240c) that
        // SetMasterVolume writes — and clamps to the 0..7 Mac speaker scale. With
        // no hardware volume ever captured in this mode the cell stays 0.
        var speakerVolume = (uint)SoundMixer.SavedHardwareVolume;
        if (7 < (speakerVolume & 0xffff))
        {
            speakerVolume = 7;
        }
        return speakerVolume;
    }
}
