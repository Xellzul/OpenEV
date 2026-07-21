using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_1001d394 (EV Override-11.c lines 13073-13097): returns 1 when a
// pilot file with this name exists in the prefs/pilot folder, 0 on fnfErr (-43)
// or any other error — the exact original result mapping.
public static class PilotFileExistsOnDefaultVolume
{
    public static int Run(string pilotName)
    {
        // GetVol's volume-name buffer was reused as the FSMakeFSSpec file name; with
        // the name a managed string, only the error gate remains.
        short osErr = MacToolbox.GetVol(out _);
        if (osErr == 0)
        {
            var fsSpec = new MacToolbox.FsSpec();
            osErr = MacToolbox.FSMakeFSSpec(PrefsFolderLocation.VRefNum,
                                            PrefsFolderLocation.DirID,
                                            pilotName, fsSpec);
            if (osErr == -43)    // fnfErr — no pilot file with this name
                return 0;
            if (osErr == 0)
                return 1;
        }
        return 0;
    }
}
