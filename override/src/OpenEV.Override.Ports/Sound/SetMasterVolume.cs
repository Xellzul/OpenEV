using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 48764-48780.
// Stores the 0..0x100 level into the mixer's software-volume word and, in
// hardware-volume mode, pushes it to both hardware channels (level * 0x10001
// = L<<16|R).
public static class SetMasterVolume
{
    public static void Run(ushort volume)
    {
        // Port host bridge (kept): forward the level to the host SoundEngine so the
        // prefs Sound Volume slider actually changes the output volume.
        // volume is the prefs value<<5 (0..0x100); the host wants 0..1.
        ushort hostLevel = volume > 0x100 ? (ushort)0x100 : volume;
        MacToolbox.MasterVolumeSetter?.Invoke(hostLevel / 256f);

        if (EvoGlobals.IsSoundSubsystemBooted)
        {
            if (0x100 < volume)
            {
                volume = 0x100;
            }
            // ORIGINAL QUIRK: this cell (0x1008240c) is WRITE-ONLY in the binary —
            // no reader anywhere; even GetMasterVolume's software branch reads
            // SavedHardwareVolume instead.
            SoundMixer.SoftwareVolume = volume;
            if (SoundMixer.UseHardwareVolume)
            {
                MacToolbox.SetDefaultOutputVolume(volume * 0x10001);
            }
        }
    }
}
