using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_1001d434 (EV Override-11.c lines 13103-13125): delete the pilot
// file with this name from the prefs/pilot folder, if it exists.
public static class DeletePilotFileIfExists
{
    public static void Run(string pilotName)
    {
        if (PilotFileExistsOnDefaultVolume.Run(pilotName) != 0)
        {
            var fsSpec = new MacToolbox.FsSpec();
            short err = MacToolbox.FSMakeFSSpec(PrefsFolderLocation.VRefNum,
                                                PrefsFolderLocation.DirID,
                                                pilotName, fsSpec);
            if (err == 0)
                MacToolbox.FSpDelete(fsSpec);
        }
    }
}
