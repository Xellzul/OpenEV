namespace OpenEV.Override.Ports.Sound;

// FUN_10075450 (EV Override-11.c lines 49015-49069): load a 'snd ' resource by id,
// locate its SoundHeader and decode the 8-bit samples into an 'asnd' block. The port
// decodes into the managed SndResourceRegistry and returns the established
// 0x5D?????? snd-handle sentinel (the registry is the only lookup — the sentinel
// is never dereferenced).
//
// Must return 0 when the resource is missing, matching the decompile's null
// return: LoadAllUiSoundEffects' ambient bank-probe loop (line 34058
// `if (iVar2 == 0) break`) depends on the 0 to terminate.
public static class LoadSndResource
{
    public static int Run(int sndId) => SndResourceRegistry.LoadAndRegister(sndId);
}
