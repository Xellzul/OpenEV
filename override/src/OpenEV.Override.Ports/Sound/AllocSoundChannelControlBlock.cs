using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10075c14 (EV Override-11.c lines 49372-49382).
// Original: NewPtrClear(0x424) — a Mac SndChannel record — then inits its
// +0x1e field to 0x80 and returns the Ptr. The decompile dropped the return
// value (it shows void); BootSoundSubsystem stores the result as the
// mixer channel. The port: the 0x424 record and its +0x1e init are absorbed by the
// host sound bridge — hand back the 'Schn' channel sentinel instead.
public static class AllocSoundChannelControlBlock
{
    public static int Run() => MacToolbox.MakeSoundChannelHandle();
}
