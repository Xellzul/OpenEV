using System;

namespace OpenEV.Platform.Toolbox;

// Gestalt('snd ' gestaltSoundAttr) result bits (Inside Macintosh: Sound).
// BootSoundSubsystem and InitSoundSubsystem — the two callers of
// MacToolbox.GestaltSoundAttrs — only ever test bit 0.
[Flags]
public enum SoundGestaltAttrs : uint
{
    StereoCapability = 0x0001, // Apple: gestaltStereoCapability
}
