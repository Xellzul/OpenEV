using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_10072520 (EV Override-11.c lines 47116-47168): write the managed
// 0x11c registration record (ShareWareGlobals.Record) into the "EV Override Pilots"
// prefs file — overwriting this owner's existing record if the scan finds one
// (EqualString on the leading owner-name Str255), appending otherwise.
//
// FindFolder resolves LIVE here too, via the same array-form overload documented in
// MacToolbox.HfsDataFork.cs (not the fnfErr stub) — the guard below actually opens the
// real prefs file, same as on a real Mac.
public static class WritePilotRecordToPrefsFile
{
    private const int OnSystemDisk = -32768;              // kOnSystemDisk (0xffff8000)
    private const int PreferencesFolderType = 0x70726566; // 'pref' = kPreferencesFolderType

    public static int Run()
    {
        string ownerKey = ShareWareGlobals.OwnerName;            // EqualString scan key
        byte[] recordOut = ShareWareGlobals.Record.Block.Data;   // the managed record, staged for FSWrite

        short[] vRefNum = new short[2];
        int dirID = default;
        short[] fileRefNum = new short[2];
        fileRefNum[0] = -1;

        int result = MacToolbox.FindFolder(OnSystemDisk, PreferencesFolderType, 1, vRefNum, dirID);
        if ((short)result == 0)
        {
            result = MacToolbox.HOpen((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, 3, fileRefNum);
            if ((short)result == -43)   // fnfErr — prefs file not present yet
            {
                // HCreate with '????' creator/type (an unknown-creator file).
                MacToolbox.HCreate((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, 0x3f3f3f3f, 0x3f3f3f3f);
                result = MacToolbox.HOpen((int)vRefNum[0], dirID, PilotPrefsFile.NameStr, 3, fileRefNum);
            }
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

                // Scan record-by-record for this owner's existing entry.
                bool found = false;
                byte[] recordBuf = new byte[RegistrationRecord.Size];
                short ioErr = (short)MacToolbox.SetFPos((int)fileRefNum[0], 1, 0);
                while (!found && ioErr == 0)
                {
                    ioErr = (short)MacToolbox.FSRead((int)fileRefNum[0], RegistrationRecord.Size, recordBuf);
                    if (ioErr == 0 && MacToolbox.EqualString(ownerKey, recordBuf, 1, 1) != 0)
                    {
                        found = true;
                    }
                }
                if (found)
                {
                    // Seek back one record to overwrite the matched entry in place.
                    MacToolbox.SetFPos((int)fileRefNum[0], 3, -RegistrationRecord.Size);
                }
                MacToolbox.FSWrite((int)fileRefNum[0], RegistrationRecord.Size, recordOut);
                result = MacToolbox.FSClose((int)fileRefNum[0]);
            }
        }
        return result;
    }
}
