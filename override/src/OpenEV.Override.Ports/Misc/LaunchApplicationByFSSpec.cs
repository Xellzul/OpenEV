using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// FUN_10072148 (EV Override-11.c lines 46980-47052) — launch the shareware
// "Register" helper application: first by FSSpec in the current volume/dir
// (tryDirect), then by scanning a found-files list for an 'APPL' with creator
// 'Areg' that isn't an alias (finderFlags bit 0x8000 = kIsAlias).
//
// The only caller (ShowSharewareNagDialog) composes the app name as a C# string
// ("Register " + STR# 900:1). The direct launch runs through the host AppLauncher
// glue (V2TitleAdapter, a real Process.Start); when it returns fnfErr(-43) because
// OpenEV.Register.exe isn't built, the scan fallback runs, but FUN_10074930 is a
// genuinely-unavailable HFS catalog search kept a no-op, so it finds nothing.
public static class LaunchApplicationByFSSpec
{
    public static int Run(string appName, byte tryDirect)
    {
        int result = 0;
        if (tryDirect != 0)
        {
            result = MacToolbox.HGetVol(0, out short vRefNum, out int dirId);
            if ((short)result == 0)
            {
                // FSMakeFSSpec(vRefNum, dirId, name, &spec): the FSSpec out (auStack_8c) only
                // fed the LaunchParamBlockRec's launchAppSpec field, which the LaunchApplication
                // stub never reads.
                MacToolbox.FSMakeFSSpec(vRefNum, dirId, appName);
                // DEVIATION (faithful): the decompile's LaunchApplication trap is a pure no-op
                // stub (LaunchApplication(int)=>0) with no counterpart capability in the port.
                // When the host wires MacToolbox.AppLauncher (TitleAdapter.LaunchRegisterApp),
                // this spawns a REAL OS process (Process.Start on the built OpenEV.Register exe)
                // instead of doing nothing — the intended bridge from the game to the standalone
                // Register app. Falls back to the no-op stub when AppLauncher isn't wired.
                result = MacToolbox.AppLauncher != null
                    ? MacToolbox.AppLauncher(appName)
                    : MacToolbox.LaunchApplication(0);   // auStack_3c LaunchParamBlockRec — stub ignores it
            }
        }
        if (tryDirect == 0 || (short)result != 0)
        {
            // decompile: `puVar1 = GetCursor(4); if (puVar1 != 0) SetCursor(*puVar1)` — show the watch
            // cursor for the search. GetCursor(4) returns the id (4, non-zero) and SetCursor forwards
            // to the host cursor hook; the Mac's *handle deref is collapsed into the id pass-through.
            int cursorHandle = MacToolbox.GetCursor(4);
            if (cursorHandle != 0)
            {
                MacToolbox.SetCursor(cursorHandle);
            }
            // FUN_10074930(0, -1, name, fileList, 0x14, &local_90, 1, 0) — the found-count is an
            // OUT param; fileList was the byte[1400] auStack_608 (20 × 0x46 FSSpecs). Always
            // finds nothing (see header) — the 'APPL'/'Areg' scan below is dead today.
            result = CatSearchForRegisterApp.Run(0, -1, appName, 0, 20, out int fileCount, true, false);   // FUN_10074930; 20 = max FSSpecs in fileList
            MacToolbox.InitCursor();
            if (0 < fileCount)
            {
                short foundIndex = -1;
                for (short fileIndex = 0; fileIndex < fileCount; fileIndex = (short)(fileIndex + 1))
                {
                    // FSpGetFInfo(fileList + fileIndex*0x46, &fInfo): fdType at +0 (local_618),
                    // fdCreator at +4 (local_614), fdFlags at +8 (local_610) — the FSpGetFInfo
                    // shim only returns fdType, so creator/flags stay default.
                    int fileCreator = default;
                    ushort finderFlags = default;
                    MacToolbox.FSpGetFInfo(fileIndex * 0x46, out int fileType);
                    if (fileType == 0x4150504c)
                    {            // 'APPL'
                        if (fileCreator == 0x41726567 && (finderFlags & 0x8000) == 0)
                        {   // 'Areg', not an alias (0x8000 = kIsAlias)
                            foundIndex = fileIndex;
                        }
                    }
                }
                if (foundIndex != -1)
                {
                    // launchAppSpec = fileList + foundIndex*0x46 — same as the direct-launch
                    // path above, the param block is never staged; the stub ignores it.
                    result = MacToolbox.LaunchApplication(0);
                }
            }
        }
        return result;
    }
}
