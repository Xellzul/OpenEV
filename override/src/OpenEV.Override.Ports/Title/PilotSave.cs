using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Pilot;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_1001a778 (EV Override-11.c lines 12036-12067). Writes the pilot
// record to disk: if a save file already exists at the pilot's FSSpec,
// FSpDelete it first; either way (delete succeeded or no file existed) calls
// SavePilotFile (FUN_1001a868), the actual writer.
public static class PilotSave
{
    public static int Run(int dockedSpobIndex)
    {
        // Register this pilot's save file (named after the pilot) as a managed
        // resource fork so the FSSpec traps below treat it as real, on-disk storage.
        MacToolbox.RegisterManagedForkFile(PilotIdentity.Name);

        var fsSpec = new MacToolbox.FsSpec();
        int result;
        if (!BugBits.IsStoredSet(BugBit.Bit0xC))   // 'ëbug' bit 0xc — save guard
        {
            result = MacToolbox.FSMakeFSSpec(PrefsFolderLocation.VRefNum,
                PrefsFolderLocation.DirID, PilotIdentity.Name, fsSpec);
            if ((short)result == 0)
            {
                result = MacToolbox.FSpDelete(fsSpec);
                if ((short)result == 0)
                {
                    // The decompile rebuilds this FSSpec off "local_7c" — the decompiler's rendering of
                    // the TOC-register reload after the FSpDelete call, not a real second
                    // pointer; it resolves to the SAME PrefsFolderLocation/PilotIdentity
                    // globals as the FSMakeFSSpec call above.
                    result = MacToolbox.FSMakeFSSpec(PrefsFolderLocation.VRefNum,
                        PrefsFolderLocation.DirID, PilotIdentity.Name, fsSpec);
                    if ((short)result == -43)   // fnfErr: the delete took, write fresh
                    {
                        result = SavePilotFile.Run(dockedSpobIndex);
                    }
                }
            }
            else if ((short)result == -43)   // fnfErr: no file existed, write fresh
            {
                result = SavePilotFile.Run(dockedSpobIndex);
            }
        }
        else
        {
            result = 0;
        }
        return result;
    }
}
