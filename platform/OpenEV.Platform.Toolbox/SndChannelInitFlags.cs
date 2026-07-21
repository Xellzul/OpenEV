using System;

namespace OpenEV.Platform.Toolbox;

// Classic Mac Sound Manager SndNewChannel "init" option bits (Inside Macintosh:
// Sound). Only the members the sound-boot call sites actually combine are
// listed here (BootSoundSubsystem picks Mono/Stereo; InitSoundSubsystem's
// default channel-init value is Mono | NoInterp) — other Apple-documented bits
// (initChanLeft/initChanRight/initMACE3/initMACE6/initNoDrop) are left out
// until a real call site needs them, rather than inventing unverified members.
[Flags]
public enum SndChannelInitFlags : short
{
    NoInterp = 0x0004, // Apple: initNoInterp — no interpolation between samples
    Mono     = 0x0080, // Apple: initMono
    Stereo   = 0x00c0, // Apple: initStereo
}
