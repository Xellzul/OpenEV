using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10019f88 — the boot prefs-load (EV Override-11.c lines 11813-11936):
// open the prefs file, read the 'Mp¨Ä' id-0x80 blob and apply it; fall back
// to defaults (DefaultGamePrefs + default keymap + CPU benchmark) when the
// file/resource is missing. A wrong-version blob triggers the legacy
// cleanup: delete the OLD prefs file and alert (or alert + ExitToShell when
// the delete fails).
//
// Port note: this IS the boot prefs-load — GameBootSequence step 7 (FUN_10061bb0
// order) calls it. The fallback RunCpuSpeedBenchmark calls (FUN_10054db0) are the SOLE
// CPU benchmark (there is no standalone boot step, as in the original): they seed
// the game-speed/time-scale physics cell (WorldState.CpuSpeedScale == the
// GameSpeed pref the happy path loads via PrefsFile.ApplyBlob) on the no-prefs
// path. The folder location is Pilot.Model.PrefsFolderLocation, the flag/speed are
// managed (SystemGlobals/PrefsDialogState via PrefsFile.ApplyBlob), and the alert
// strings below are PEF-dump literals.
public static class ApplyDefaultPrefsToMemory
{
    // toc-0x55e7 (PEF dump). ORIGINAL BUG (preserved bug-for-bug): the wrong-version
    // path deletes a file named "Escape Velocity Prefs" — the EV1 name — while
    // the file it actually opened is GetIndString(0x82,7) ("Override
    // Preferences"). Leftover EV1 code.
    private const string OldPrefsFileName = "Escape Velocity Prefs";
    // toc-0x55d1 (PEF dump, MacRoman ’).
    private const string PrefsReplacedMessage =
        "The Override Preferences file in your system folder appears to have been created with an " +
        "older version of Override. It’s now been updated, but you’ll have to redo your " +
        "settings in the preferences dialog.";
    // toc-0x5505 (PEF dump, MacRoman ’).
    private const string PrefsDeleteFailedMessage =
        "The Override Preferences file in your system folder was created with an older version of " +
        "Override. We tried to delete it but couldn’t - please remove it and restart Override.";

    public static void Run()
    {
        // BOUNDARY: FSSpec record — the File Manager traps walk it by address
        // (shared with WritePrefsToDisk; both are one-shot, never concurrent).
        int fsSpec = PrefsMemory.WritePrefs_FSSpec;

        MacToolbox.GetTime(0);   // no-op shim; the DateTimeRec out is unread in the original too
        string fileName = MacToolbox.GetIndString(0x82, 7);   // "Override Preferences"
        // VRefNum/DirID: GameToc+0x1e8c/+0x1e90 -> managed PrefsFolderLocation (see its own doc).
        short osErr = MacToolbox.FSMakeFSSpec((int)Pilot.Model.PrefsFolderLocation.VRefNum,
                                        Pilot.Model.PrefsFolderLocation.DirID, fileName, fsSpec);
        if (osErr == -43)   // fnfErr — prefs file not found
        {
            ApplyPrefsDefaults();
        }
        else if (osErr == 0)
        {
            short prefsResRefNum = MacToolbox.FSpOpenResFile(fsSpec, 3);   // ex ptr cell 0x100870dc
            if (prefsResRefNum == -1)
            {
                ApplyPrefsDefaults();
            }
            else
            {
                int prefsHandle = MacToolbox.GetResource(MacResType.PrefsFile, 0x80);   // ex ptr cell 0x100870cc
                if (prefsHandle == 0)
                {
                    MacToolbox.CloseResFile((int)prefsResRefNum);
                    ApplyPrefsDefaults();
                }
                else
                {
                    MacToolbox.HNoPurge(prefsHandle);
                    MacToolbox.HLock(prefsHandle);
                    Core.Model.GamePrefs.GfxDetailFlag = 0;
                    Core.Model.GamePrefs.PrefByte551 = 0;
                    Core.Model.GamePrefs.IntroMusicEnabled = 0;
                    Core.Model.GamePrefs.ProjectileStreaksDisabled = 0;
                    Core.Model.GamePrefs.UseQuickdraw = 0;
                    Core.Model.GamePrefs.QuickTimeMoviesDisabled = 0;
                    byte[] blob = MacToolbox.HandleToBytes(prefsHandle);
                    osErr = blob.Length >= 2 ? (short)((blob[0] << 8) | blob[1]) : (short)0;
                    if (osErr == 0x68)
                    {
                        PrefsFile.ApplyBlob(blob);
                    }
                    MacToolbox.HUnlock(prefsHandle);
                    MacToolbox.HPurge(prefsHandle);
                    MacToolbox.CloseResFile((int)prefsResRefNum);
                    if (osErr != 0x68)
                    {
                        osErr = MacToolbox.FSMakeFSSpec((int)Pilot.Model.PrefsFolderLocation.VRefNum,
                                                        Pilot.Model.PrefsFolderLocation.DirID, OldPrefsFileName, fsSpec);
                        if (osErr == 0)
                        {
                            osErr = (short)MacToolbox.FSpDelete(fsSpec);
                            if (osErr == 0)
                            {
                                // NOTE: order here is DefaultGamePrefs, BENCHMARK, then Keymap — the decompile
                                // swaps the last two vs. every other defaults-application site in this
                                // function (see ApplyPrefsDefaults); preserved bug-for-bug, not shared.
                                Misc.DefaultGamePrefs.Run();
                                Misc.RunCpuSpeedBenchmark.Run();
                                Misc.Model.Keymap.InitDefaultMacKeyBindings();
                                Graphics.Model.Palette.FadeOut(8);
                                Title.AlertModal_OneButton.Run(PrefsReplacedMessage);
                                // FUN_1005d148(8, *(toc-0x7860)) — fade to the screen-fade cell (ScreenFadeCTab,
                                // the original never writes it -> black). Faithful ptr-overload form; the host composite ramp now
                                // runs (Bridge.cs ScreenFadeToColor(steps, ptr)). NOTE: this whole branch is the
                                // wrong-version prefs RESET path — it FSpDeletes a legacy Mac prefs file, so it is
                                // dead on the Windows port (reachable only if such a file exists). The closing fade-to-black is revealed by the boot's
                                // later FadeOut(16) (GameBootSequence step 33). See project_evo_v2_prefs_reset_fade_REVIEW.
                                Graphics.Model.Palette.FadeIn(8, Graphics.Model.Palette.ScreenFadeCTab);   // cell 0x10080e00 / *(toc-0x7860), the original never writes it -> black
                            }
                            else
                            {
                                PrefsDeleteFailedExit();
                            }
                        }
                        else
                        {
                            PrefsDeleteFailedExit();
                        }
                    }
                }
            }
        }
        else
        {
            ApplyPrefsDefaults();
        }
    }

    // DefaultGamePrefs + default keymap + CPU benchmark, in this order — the decompile repeats
    // this exact sequence at 4 of its 5 fallback sites (fnfErr / open-fails / GetResource-fails /
    // any-other-osErr); shared here verbatim.
    private static void ApplyPrefsDefaults()
    {
        Misc.DefaultGamePrefs.Run();
        Misc.Model.Keymap.InitDefaultMacKeyBindings();
        Misc.RunCpuSpeedBenchmark.Run();
    }

    // The decompile duplicates this exit sequence at both delete-failure sites
    // (FSMakeFSSpec-fails and FSpDelete-fails); shared here verbatim.
    private static void PrefsDeleteFailedExit()
    {
        Graphics.Model.Palette.FadeOut(8);
        Title.AlertModal_OneButton.Run(PrefsDeleteFailedMessage);
        RestoreMacMenuBar.Run();
        Sound.TeardownSoundSubsystem.Run();
        Graphics.TearDownSavedPalette.Run();
        MacToolbox.ExitToShell();
    }
}
