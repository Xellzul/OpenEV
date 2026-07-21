using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1001a3b8 — persist the prefs to disk (EV Override-11.c lines
// 11937-12035). Recreates the prefs file (delete + FSpCreateResFile), opens
// its resource fork, and writes the 'Mp¨Ä' id-0x80 blob ("EV Prefs").
//
// Dialog 4-rules rewrite: the NewHandle(0x74) + ~40 big-endian field pokes
// are PrefsFile.BuildBlob() (byte-identical on-disk format); the handle/
// refNum cells 0x100870cc/0x100870dc are locals; the prefs
// folder vRefNum/dirID come from Pilot.Model.PrefsFolderLocation (the early transcription's
// `*(toc+0x1e8c/0x1e90)` reads ran under the unseeded title TOC → read 0 —
// that was the [PREFS] near-null read, faithfully fixed by the managed cells). Only the FSSpec
// record stays a raw-address toolbox boundary (the File Manager traps take it by address; no
// EvoMemory backing survives — MacToolbox keys its internal state off the address instead).
public static class WritePrefsToDisk
{
    public static void Run()
    {
        // BOUNDARY: FSSpec record (byte[70]) — FSMakeFSSpec writes it, then
        // FSpCreateResFile/FSpOpenResFile/FSpDelete take it back by address.
        int fsSpec = PrefsMemory.WritePrefs_FSSpec;

        int savedResFile = MacToolbox.CurResFile();
        string fileName = MacToolbox.GetIndString(0x82, 7);   // "Override Preferences"
        short result = MacToolbox.FSMakeFSSpec((int)Pilot.Model.PrefsFolderLocation.VRefNum,
                                         Pilot.Model.PrefsFolderLocation.DirID, fileName, fsSpec);
        short prefsResRefNum;
        if (result == -43)   // fnfErr
        {
            MacToolbox.FSpCreateResFile(fsSpec, MacFileType.EvoCreator, PrefsFile.FileType, 0);
            prefsResRefNum = MacToolbox.FSpOpenResFile(fsSpec, 3);
        }
        else
        {
            result = MacToolbox.FSpDelete(fsSpec);
            if (result != 0)
            {
                return;
            }
            MacToolbox.FSpCreateResFile(fsSpec, MacFileType.EvoCreator, PrefsFile.FileType, 0);
            prefsResRefNum = MacToolbox.FSpOpenResFile(fsSpec, 3);
        }
        if (prefsResRefNum != -1)
        {
            // The decompile guards on NewHandle(0x74) succeeding (HNoPurge/HLock/
            // HUnlock/HPurge around the pokes); the managed blob can't fail.
            byte[] blob = PrefsFile.BuildBlob();
            MacToolbox.UseResFile(prefsResRefNum);
            MacToolbox.AddResource(blob, PrefsFile.ResType, PrefsFile.ResId, PrefsFile.ResourceName);
            MacToolbox.UseResFile(savedResFile);
            MacToolbox.UpdateResFile(prefsResRefNum);
            MacToolbox.CloseResFile(prefsResRefNum);
            MacToolbox.FlushVol(0, (int)Pilot.Model.PrefsFolderLocation.VRefNum);
        }
    }
}
