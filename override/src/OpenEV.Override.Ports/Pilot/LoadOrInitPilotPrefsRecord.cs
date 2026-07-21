using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_10072344 (EV Override-11.c lines 47053-47110): find this owner's
// 0x11c registration record in the "EV Override Pilots" prefs file (scan by
// EqualString on the leading owner-name Str255); if absent, initialise a fresh
// record (owner name + install date, counters zeroed) and write it back.
//
// FindFolder resolves LIVE: this call binds to the array-form overload in
// MacToolbox.HfsDataFork.cs (not the fnfErr stub in MacToolbox.UnwiredStubs.cs), which
// maps the 'pref' folder type to a real Windows folder — matching a real Mac, where
// FindFolder succeeds and a missing prefs file drives the HOpen-fail init path below.
public static class LoadOrInitPilotPrefsRecord
{
    private const int OnSystemDisk = -32768;              // kOnSystemDisk (0xffff8000)
    private const int PreferencesFolderType = 0x70726566; // 'pref' = kPreferencesFolderType

    public static int Run()
    {
        var record = ShareWareGlobals.Record;
        string ownerKey = ShareWareGlobals.OwnerName;   // EqualString scan key + init copy source

        short[] vRefNum = new short[2];
        int dirID = 0;
        int result = MacToolbox.FindFolder(OnSystemDisk, PreferencesFolderType, 1, vRefNum, dirID);

        bool needsInit = false;
        if ((short)result == 0)
        {
            short[] fileRefNum = { -1, 0 };
            result = MacToolbox.HOpen((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, 3, fileRefNum);
            if ((short)result == 0)
            {
                // Mark the prefs file invisible: OR kIsInvisible (0x4000) into FInfo.fdFlags (the
                // big-endian halfword at buffer offset 8) in place, then write the same buffer
                // back. NO-OP today — HGetFInfo/HSetFInfo are still unwired stubs.
                byte[] finfo = new byte[16];   // sizeof(FInfo): fdType+fdCreator+fdFlags+fdLocation+fdFldr
                MacToolbox.HGetFInfo((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, finfo);
                ushort finderFlags = (ushort)((finfo[8] << 8) | finfo[9] | 0x4000);
                finfo[8] = (byte)(finderFlags >> 8);
                finfo[9] = (byte)finderFlags;
                MacToolbox.HSetFInfo((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, finfo);

                // Scan the file record-by-record for this owner's entry.
                bool found = false;
                short ioErr = (short)MacToolbox.SetFPos((int)fileRefNum[0], 1, 0);
                byte[] recordBuf = new byte[RegistrationRecord.Size];
                while (!found && ioErr == 0)
                {
                    ioErr = (short)MacToolbox.FSRead((int)fileRefNum[0], RegistrationRecord.Size, recordBuf);
                    if (ioErr == 0 && MacToolbox.EqualString(ownerKey, recordBuf, 1, 1) != 0)
                    {
                        found = true;
                    }
                }
                // The decompile inverts the flag here: a match means "no init needed".
                needsInit = !found;
                if (!needsInit)
                {
                    // Matched an existing record: copy it into the managed block.
                    for (int i = 0; i < RegistrationRecord.Size; i++)
                    {
                        record.Block.Data[i] = recordBuf[i];
                    }
                }
                result = MacToolbox.FSClose((int)fileRefNum[0]);
            }
            else
            {
                needsInit = true;   // prefs file absent -> take the init path
            }
        }

        if (needsInit)
        {
            // Fresh record: owner name + install-date stamp, counters zeroed.
            record.OwnerName = ownerKey;
            MacToolbox.GetDateTime(out int installSeconds);
            record.InstallDateSeconds = installSeconds;
            record.ClearCounters();
            result = WritePilotRecordToPrefsFile.Run();
        }
        return result;
    }
}
