using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_100727bc (EV Override-11.c lines 47191-47262) — load (or default-create) the
// shareware registration record. Opens the prefs file "<STR#900:1> License" in the Preferences
// folder (OpenOrCreatePrefsFolderFile); reads its size (GetEOF). If empty -> a fresh placeholder record
// (BuildDefaultRegistrationRecord); otherwise reads the saved 0x202 record. Returns the record
// handle (0 on any open/read failure). Callers: CheckShareWareRegistrationMatch (FUN_10071a40)
// and GetRegisteredOwnerName (FUN_10071f3c).
//
// The Mac built the filename by block-copying a 0x100 template then appending its " License"
// Pascal suffix to GetIndString(_,1); reduced here to the equivalent string concat (same as the
// write side, WriteHandleToFile). The record is returned as a managed NewHandleFromBytes handle
// (the decompile's NewHandleClear + FSRead-into-*handle, adapted to the managed handle model its
// HandleToBytes callers require). Same shared code the register app ported as
// Prefs.LoadRegistrationRecord (FUN_1000a560).
public static class LoadRegistrationRecord
{
    private const int RecordSize = 0x202;
    private const int FsRdPerm = 1;   // fsRdPerm

    public static int Run(int strListId)
    {
        string fileName = MacToolbox.GetIndString((short)strListId, 1) + " License";
        var refNumOut = new short[1];
        if ((short)OpenOrCreatePrefsFolderFile.Run(fileName, FsRdPerm, refNumOut) != 0) return 0;
        short refNum = refNumOut[0];

        var eof = new int[1];
        // Bug-for-bug: a GetEOF failure returns without an FSClose (the ASM leaks the open refNum);
        // an unexpected path since HOpen just succeeded, preserved as-is.
        if ((short)MacToolbox.GetEOF(refNum, eof) != 0) return 0;

        int handle;
        if (eof[0] == 0)
        {
            handle = BuildDefaultRegistrationRecord.Run(strListId);   // empty file -> "not registered" default
        }
        else
        {
            // The decompile allocates NewHandleClear(0x202) first and, bug-for-bug, early-returns
            // (leaking the open refNum) if that or MemError fails — both unreachable in the managed
            // model, so the alloc folds into the byte[] and only the FSRead-error path yields 0.
            var record = new byte[RecordSize];
            handle = (short)MacToolbox.FSRead(refNum, RecordSize, record) != 0
                ? 0                                        // short/failed read -> no record (decompile DisposeHandle's it)
                : MacToolbox.NewHandleFromBytes(record);
        }
        MacToolbox.FSClose(refNum);
        return handle;
    }
}
