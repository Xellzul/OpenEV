using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Resource;

// FUN_10072964 (EV Override-11.c lines 47266-47327): write a handle's bytes to
// the prefs-folder file "<STR# strListId entry 1> License" (the shareware
// registration file; callers pass STR# 900).
// SPLIT-TOC: the suffix record `ReadInt(tocBase-0x35ea)` resolves to
// 0x10085076 under GameToc; its +8 Pascal suffix dumps as "\x08 License" —
// the literal below (the Mac block-copied the whole 0x100-byte record to
// the stack; only the suffix was consumed).
public static class WriteHandleToFile
{
    private const string FileNameSuffix = " License";   // @0x1008507e (GameToc-0x35e2, dumped)

    public static int Run(int dataHandle, int strListId)
    {
        short[] fileRefNum = { -1 };
        // int-arg overload — the argless GetHandleSize() absorber always returns 0.
        int handleSize = MacToolbox.GetHandleSize(dataHandle);
        string fileName = MacToolbox.GetIndString((short)strListId, 1) + FileNameSuffix;
        int err = OpenOrCreatePrefsFolderFile.Run(fileName, 3, fileRefNum);
        if ((short)err == 0)
        {
            err = MacToolbox.SetFPos(fileRefNum[0], 1, 0);
            if ((short)err == 0)
            {
                MacToolbox.HLock(dataHandle);
                // FSWrite's buffer arg must be a byte[] (HandleToBytes) — passing the
                // raw int handle silently binds the discard `params object?[]` FSWrite
                // overload instead of the real one, and the file writes empty. Matches
                // the ASM's `lwz r5, 0(r31)` handle deref (the handle's master pointer).
                byte[] dataBytes = MacToolbox.HandleToBytes(dataHandle);
                err = MacToolbox.FSWrite(fileRefNum[0], handleSize, dataBytes);
                MacToolbox.HUnlock(dataHandle);
                if ((short)err == 0)
                {
                    err = MacToolbox.FlushVol(0, 0);
                    if ((short)err == 0)
                    {
                        err = MacToolbox.FSClose(fileRefNum[0]);
                    }
                }
                else
                {
                    MacToolbox.FlushVol(0, 0);
                    MacToolbox.FSClose(fileRefNum[0]);
                }
            }
            else
            {
                MacToolbox.FSClose(fileRefNum[0]);
            }
        }
        return err;
    }
}
