using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Title;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Sound;

namespace OpenEV.Override.Ports.Boot;

// FUN_10015b4c (EV Override-11.c lines 10604-10687) — boot step 10. Locates the
// Pilots folder, opens the six plugin resource files (STR# 130 idx 1..6) into the
// PluginResourceRefs slots, then opens the EV Plug-Ins fork. Each failure fades
// out, shows a one-button "couldn't locate X" alert, tears the subsystems down and
// ExitToShells. These guards are LIVE: OpenResFile reflects the real per-fork outcome
// (MacToolbox.ResFileOpener, wired in TitleAdapter from the loader's actually-opened
// forks — the real loading is still OverrideDataLoader), so a missing Graphics/Sounds/
// Titles/Data fork fires its alert + ExitToShell. The Pilots-folder guard is wired the
// same way (FsSpecByNameProbe) but the host auto-creates that folder, so it always passes.
public static class OpenPluginResourceFiles
{
    // Error-alert + filename strings (MacRoman curly quotes kept faithfully).
    private const string PilotsFolderName = ":Pilots";       // was Toc-0x58f7
    private const string ErrPilotsFolderMissing =                  // was Toc-0x58ef
        "EV couldn’t locate the Pilots folder. Please make sure the folder called “Pilots” is in the same folder as EV.";
    private const string ErrGraphicsFileMissing =                  // was Toc-0x5880
        "Override couldn’t locate its graphics file. Please make sure the file “Override Graphics” is in the same folder as Override.";
    private const string ErrSoundsFileMissing =                  // was Toc-0x5803
        "Override couldn’t locate its sounds file. Please make sure the file “Override Sounds” is in the same folder as Override.";
    private const string ErrTitlesFileMissing =                  // was Toc-0x578a
        "Override couldn’t locate its titles file. Please make sure the file “Override Titles” is in the same folder as Override.";
    // public: the host reuses this exact wording for its no-data-folder message box (the case where
    // the whole EV Override folder — and thus DLOG 3000 — is absent, so the Mac alert can't render).
    public const string ErrDataFilesMissing =                  // was Toc-0x5711
        "Override couldn’t locate its data files. Please make sure the files “Override Data 1” and “Override Data 2” are in the same folder as Override.";
    private const string PluginsFolderName = "EV Plug-Ins";   // was Toc-0x5681
    private const string ErrPluginsFolderMissing =                  // was Toc-0x5675
        "Override couldn’t locate the plugins folder. Please make sure the folder called “EV Plug-Ins” is in the same folder as Override.";

    private const short PluginNameList = 130;         // STR# of the resource-file names
    private const int PluginForkType = 0x4f709566;   // 'Opïf' — EV Override plugin OSType (0x95 = MacRoman ï)

    public static void Run()
    {
        if (MacToolbox.GetVol(out short volRef) == 0)
        {
            if (MacToolbox.FSMakeFSSpec(volRef, 0, PilotsFolderName) != 0)
                FailAndExit(ErrPilotsFolderMissing);
        }

        // File->slot order is a permutation: f1->2, f2->3, f3->0, f4->1, f5->4, f6->5.
        OpenPluginFile(1, 2);
        OpenPluginFile(2, 3);
        OpenPluginFile(3, 0);
        OpenPluginFile(4, 1);
        OpenPluginFile(5, 4);
        OpenPluginFile(6, 5);

        if (PluginResourceRefs.Ref(2) == -1) FailAndExit(ErrGraphicsFileMissing);
        if (PluginResourceRefs.Ref(3) == -1) FailAndExit(ErrSoundsFileMissing);
        if (PluginResourceRefs.Ref(4) == -1) FailAndExit(ErrTitlesFileMissing);
        // Slots 0 + 1 (the two data files) are validated together; slot 5 is never checked.
        if (PluginResourceRefs.Ref(0) == -1 || PluginResourceRefs.Ref(1) == -1)
            FailAndExit(ErrDataFilesMissing);

        // EvoGlobals.BootResFileRefNum (0x1008f732) is the CurResFile() snapshot
        // InitPrefsPathAndBugBits cached at boot — NOT PluginResourceRefs slot 1
        // (0x100870d2), which OpenPluginFile(4, 1) above already overwrote with file 4's
        // refNum.
        short err = (short)OpenResourceForksByType.Run(
            EvoGlobals.BootResFileRefNum, PluginsFolderName, PluginForkType);
        if (err != 0) FailAndExit(ErrPluginsFolderMissing);
    }

    private static void OpenPluginFile(short strIndex, int slot)
    {
        string name = MacToolbox.GetIndString(PluginNameList, strIndex);
        PluginResourceRefs.SetRef(slot, MacToolbox.OpenResFile(name));
    }

    // Shared fatal path for a missing plugin file/folder.
    private static void FailAndExit(string message)
    {
        Palette.FadeOut(8);
        AlertModal_OneButton.Run(message);
        RestoreMacMenuBar.Run();
        TeardownSoundSubsystem.Run();
        TearDownSavedPalette.Run();
        MacToolbox.ExitToShell();
    }
}
