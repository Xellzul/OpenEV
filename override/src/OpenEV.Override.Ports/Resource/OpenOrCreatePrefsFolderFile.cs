// FUN_10072b0c (EV Override-11.c lines 47328-47356) — open, or create-then-open, a NAMED file
// inside the Mac Preferences folder (FindFolder(kPreferencesFolderType)). Generic helper, not
// prefs-content-specific: its only two callers are Misc.LoadRegistrationRecord and
// Resource.WriteHandleToFile, both working the shareware-registration "License" file
// (GetIndString(900,1) = "License"; see MacToolbox.HfsDataFork.cs).

using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

public static class OpenOrCreatePrefsFolderFile
{
    // FindFolder(vRefNum, folderType, createFolder, ...) selectors.
    private const int OnSystemDisk = -32768;            // kOnSystemDisk (0xffff8000)
    private const int PreferencesFolderType = 0x70726566; // 'pref' = kPreferencesFolderType
    private const int CreateFolder = 1;                 // createFolder = true

    // Creator signature + type HCreate stamps the file with (hardcoded in FUN_10072b0c
    // regardless of which file it's asked to open — these are the registration/license file's,
    // not the real prefs file's).
    private const int LicenseFileCreator = 0x41726567;    // 'Areg'
    private const int LicenseFileType = 0x416c6963;       // 'Alic'

    private const short FnfErr = -43;                   // file-not-found (decompile -0x2b)

    // fileName = the file's name (was the Pascal name's ADDRESS, decompile
    // &local_318); refNumOut[0] receives the opened refNum or -1.
    public static int Run(string fileName, int permission, short[] refNumOut)
    {
        var refNum = new ushort[2];
        int dirId = default;
        var foundVRefNum = new short[10];

        refNum[0] = 0xffff;
        refNumOut[0] = -1;
        short osErr = (short)MacToolbox.FindFolder(OnSystemDisk, PreferencesFolderType, CreateFolder, foundVRefNum, dirId);
        if (osErr == 0)
        {
            osErr = (short)MacToolbox.HOpen((int)foundVRefNum[0], dirId, fileName, permission, refNum);
            if (osErr == FnfErr)
            {
                osErr = (short)MacToolbox.HCreate((int)foundVRefNum[0], dirId, fileName, LicenseFileCreator, LicenseFileType);
                if (osErr != 0)
                {
                    return osErr;
                }
                osErr = (short)MacToolbox.HOpen((int)foundVRefNum[0], dirId, fileName, permission, refNum);
            }
            if (osErr == 0)
            {
                refNumOut[0] = (short)refNum[0];
            }
            return osErr;
        }
        return osErr;
    }
}
