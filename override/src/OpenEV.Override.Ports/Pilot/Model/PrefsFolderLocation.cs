namespace OpenEV.Override.Ports.Pilot.Model;

// The located preferences-folder FSSpec components, written in InitPrefsPathAndBugBits and
// read when building the pilot file's FSSpec. VRefNum is a SHORT volume reference; DirID is
// the directory id (long). Migrated from the GameToc-relative BSS slots (alias out
// 0x1008a4ee/0x1008a4f4, FindFolder out 0x1008a4ec/0x1008a4f0) to managed fields.
public static class PrefsFolderLocation
{
    // ResolveMacFileAlias results — these build the pilot FSSpec (was 0x1008a4ee/0x1008a4f4).
    public static short VRefNum;
    public static int DirID;

    // FindFolder(kPreferencesFolderType) results (was 0x1008a4ec/0x1008a4f0).
    public static short FindFolderVRefNum;
    public static int FindFolderDirID;
}
