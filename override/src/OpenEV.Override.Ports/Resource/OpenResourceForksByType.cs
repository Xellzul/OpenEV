using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_1006189c (EV Override-11.c 40874-40936) — scan the folder GetCatalogStartDir finds
// and FSpOpenResFile every file whose Finder type matches `fileType` (opens the EV Plug-Ins
// fork for OpenPluginResourceFiles). On Windows the File Manager traps are stubs over a
// non-existent FS: PBGetCatInfoSync returns fnfErr at ioFDirIndex 1, so the scan exits at the
// first read (no entries) and the function returns noErr — real plug-in loading is
// OverrideDataLoader's job. `folderName` is the catalog name (a C# string).
public static class OpenResourceForksByType
{
    public static int Run(short volIndex, string folderName, int fileType)
    {
        int err = GetCatalogStartDir.Run(volIndex, folderName, out int startDir, out short startVRef);
        if ((short)err != 0)
            return err;

        // Catalog-scan state (managed; was a NewPtrClear CInfoPBRec + Str255 name buffer).
        // DEVIATION (faithful): the decompile returns memFullErr (-108) if that NewPtrClear
        // fails; managed locals can't fail to allocate, so that branch has no port equivalent.
        short ioFDirIndex = 0;
        while ((short)err == 0)
        {
            ioFDirIndex++;
            err = MacToolbox.PBGetCatInfoSync(ioFDirIndex, startVRef, startDir, out short entryVRef);
            if ((short)err != 0)
                break;   // end of directory (always at index 1 under Windows — no file system)

            // Per-entry handling — unreached under Windows (PBGetCatInfoSync always fails past
            // ioFDirIndex 0, so this block never executes here). DEVIATION (faithful): even as
            // dead code this isn't fully ported — the decompile builds a real FSSpec (vRefNum +
            // startDir + entry name) and passes its address to ResolveAliasFile/FSpGetFInfo/
            // FSpOpenResFile; this block passes only the bare vRefNum (parID and name dropped).
            // Behavior-invisible since the block can't run, but flagging since it contradicts
            // ResolveAliasFile's own doc ("first arg is the FSSpec ptr, not a vRefNum").
            MacToolbox.ResolveAliasFile(entryVRef, 1, out byte targetIsFolder, out byte _);
            err = MacToolbox.FSpGetFInfo(entryVRef, out int entryFileType);
            if (targetIsFolder == 0)
            {
                if (entryFileType == fileType)
                    MacToolbox.FSpOpenResFile(entryVRef, 3);
            }
            else
            {
                err = 0;
            }
        }
        return 0;
    }
}
