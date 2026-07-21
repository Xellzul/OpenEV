using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Boot;

// FUN_10015abc (EV Override-11.c 10581-10603) — open the current resource file, cache the
// 'ëbug' pilot-save guard bits (12/13), resolve the prefs-file alias, and locate the
// preferences folder. All storage is managed now (EvoGlobals.BootResFileRefNum / BugBits.Stored /
// PrefsFolderLocation); the prefs alias name is read from the PEF data segment as a C# string.
public static class InitPrefsPathAndBugBits
{
    private const string PrefsAliasName = "Pilots";   // was the Pascal @0x10082d62 (GameToc-0x58fe, dumped)
    private const int PreferencesFolderType = 0x70726566;   // 'pref' = kPreferencesFolderType
    private const int OnSystemDisk = -32768;       // kOnSystemDisk (0xffff8000)

    public static void Run()
    {
        // 0x1008f732 — caches CurResFile() for ResolveMacFileAlias below and for later
        // reads by OpenPluginResourceFiles' EV Plug-Ins scan (EvoGlobals.BootResFileRefNum).
        EvoGlobals.BootResFileRefNum = (short)MacToolbox.CurResFile();

        // Cache the pilot-save guard bits: 12 (read by PilotSave), 13 (read by SavePilotFile).
        BugBits.SetStored(BugBit.Bit0xC, BugBits.IsSet(BugBit.Bit0xC));
        BugBits.SetStored(BugBit.SavePilotFileGuard, BugBits.IsSet(BugBit.SavePilotFileGuard));

        ResolveMacFileAlias.Run(EvoGlobals.BootResFileRefNum,
                                PrefsAliasName,
                                out PrefsFolderLocation.DirID,
                                out PrefsFolderLocation.VRefNum);
        MacToolbox.FindFolder(OnSystemDisk, PreferencesFolderType, 1,
                              out PrefsFolderLocation.FindFolderVRefNum,
                              out PrefsFolderLocation.FindFolderDirID);
    }
}
