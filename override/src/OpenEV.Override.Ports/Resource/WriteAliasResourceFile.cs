using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// Port of FUN_1001d4bc (EV Override-11.c lines 13126-13205): record the "Last
// Pilot" pointer after a pilot save/load, so the boot auto-load (FUN_1001b56c)
// can resume it. Called from SavePilotFile and the two pilot loaders.
//
// DEVIATION (faithful): the Mac wrote a real Finder 'alis' file resolving to
// the pilot; NewAlias/FSpCreateResFile/FSpSetFInfo are no-op shims with no
// Windows analog, so a literal port produced nothing and boot never resumed.
// Port-native replacement: persist the pilot's leaf NAME via
// MacToolbox.WriteLastPilotPointer; at boot ResolveAliasFile redirects a
// "Last Pilot" spec to that name, so the pilot loads under its real name
// exactly as the Mac alias resolved transparently (LoadPluginPilotData reads
// the display name from the FSSpec leaf, hence a pointer not a copy).
public static class WriteAliasResourceFile
{
    public static void Run(int pilotSpec)
    {
        // pilotSpec already names the resolved pilot file: SavePilotFile builds it from
        // PilotIdentity.Name, and the loaders pass it AFTER ResolveAliasFile has redirected
        // a "Last Pilot" spec to the real target — so this never points the pointer at itself.
        string targetName = MacToolbox.FsSpecName(pilotSpec);
        MacToolbox.WriteLastPilotPointer(targetName);
    }
}
