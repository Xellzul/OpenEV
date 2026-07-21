using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// Port of FUN_1001eac8 — EV Override-11.c lines 13732-13749.
//
// DEVIATION (faithful): the decompile's not-found path returns `unaff_r30` —
// a decompiler phantom default (r30 is only written on the found path; on the
// not-found path the ASM epilogue returns whatever the CALLER's r30 held at
// entry, an uncomputable register leak). The port defaults to 0 instead.
// Verified harmless: `[FM] GetResource type=0x79918aa8 id=0x80` traced at runtime always resolves (18 bytes, host resource fork) — the
// shipped game's resource fork always contains this resource, so the
// not-found/default branch is unreachable with real game data.
public static class ReadStoredVersionStamp
{
    public static int Run()
    {
        int versionStamp = 0;

        var handle = MacToolbox.GetResource(MacResType.VersionStamp, 128);
        if (handle != 0)
        {
            // Read via the handle's real double-deref (`*(short *)*handle`, what
            // ReadResourceShort does) — reading EvoMemory at the handle VALUE
            // itself resolves to a garbage address, not resource data.
            versionStamp = MacToolbox.ReadResourceShort(handle, 0);
            MacToolbox.ReleaseResource(handle);
        }
        return versionStamp;
    }
}
