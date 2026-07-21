using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Title;

// FUN_10044480 — the Set Prefs dialog (DLOG 4001, DITL 4001).
// Decompile: EV Override-11.c lines 28371-28609. Driven by
// FUN_1004445c -> PrefsDialog -> here. Loads the volume PICTs (134..138) +
// the "Keys" grid PICT 132, installs control values from Core.Model.GamePrefs,
// runs ModalDialog dispatching each item-hit:
//   1    OK (validates keybinds, persists prefs, calls WritePrefsToDisk)
//   2    QuickDraw toggle
//   35   Cancel
//   3..33    keybind capture slots (filter writes the new key short)
//   37..39   volume readout + up/down buttons (PICT pressed-swap + beep)
//   40/41/43 toggle checkboxes (41 inverted: QuickTime movies)
//   42   open the Game Speed sub-dialog
//
// Dialog 4-rules rewrite: state lives in PrefsDialogState/Core.Model.GamePrefs;
// GetDialogItem outs are managed arrays.
public static class PrefsDialogInit
{
    public static void Run()
    {
        var itemKind = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];
        short itemHit = 0;

        bool done = false;
        int modalUpp = MacToolbox.NewRoutineDescriptor(PrefsDialogState.KeyAssignFilterProc, 0xfd0, 1);
        PrefsDialogState.SelectedKeybindSlot = 0;
        Misc.Model.Keymap.PackKeyBindings();
        GamePrefs.DialogWorkingVolume = GamePrefs.MasterVolume;
        byte streaksDisabled = GamePrefs.ProjectileStreaksDisabled;
        byte useQuickdraw = GamePrefs.UseQuickdraw;
        byte qtMovies = GamePrefs.QuickTimeMoviesDisabled;
        byte pref551 = GamePrefs.PrefByte551;
        byte introMusic = GamePrefs.IntroMusicEnabled;

        // The decompile also stages this value into a dead double local here —
        // dropped (it's overwritten by the OK branch below before ever being read).
        PrefsDialogState.GameSpeedPercent =
            (short)(int)(GameSpeedScale.SliderScale * PrefsDialogState.GameSpeed + GameSpeedScale.SliderBias);
        PrefsDialogState.Pict132Handle = MacToolbox.GetPicture(132);
        for (short i = 0; i < PrefsDialogState.VolumePicts.Length; i++)
        {
            PrefsDialogState.VolumePicts[i] = MacToolbox.GetPicture(i + 134);
        }
        PrefsDialogState.DialogWindow = MacToolbox.GetNewDialog(4001, 0, -1);
        if (PrefsDialogState.DialogWindow != 0)
        {
            NewDialogHook.Run(PrefsDialogState.DialogWindow, 0);
            MacToolbox.ShowWindow(PrefsDialogState.DialogWindow);
            MacToolbox.SelectWindow(PrefsDialogState.DialogWindow);
            MacToolbox.SetPort(PrefsDialogState.DialogWindow);
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 2, itemKind, itemHandle, itemRect);
            if (useQuickdraw == 0)
            {
                MacToolbox.SetControlValue(itemHandle[0], 0);
            }
            else
            {
                MacToolbox.SetControlValue(itemHandle[0], 1);
            }
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 40, itemKind, itemHandle, itemRect);
            if (introMusic == 0)
            {
                MacToolbox.SetControlValue(itemHandle[0], 0);
            }
            else
            {
                MacToolbox.SetControlValue(itemHandle[0], 1);
            }
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 41, itemKind, itemHandle, itemRect);
            if (qtMovies == 0)
            {
                MacToolbox.SetControlValue(itemHandle[0], 1);
            }
            else
            {
                MacToolbox.SetControlValue(itemHandle[0], 0);
            }
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 43, itemKind, itemHandle, itemRect);
            if (streaksDisabled == 0)
            {
                MacToolbox.SetControlValue(itemHandle[0], 0);
            }
            else
            {
                MacToolbox.SetControlValue(itemHandle[0], 1);
            }
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 37, itemKind, itemHandle, itemRect);
            MacToolbox.InvalRect(itemRect);
            do
            {
                MacToolbox.ModalDialog(modalUpp, ref itemHit);
                if (itemHit == 1)
                {
                    bool conflict = false;
                    for (short idx = 0; idx < 31; idx++)
                    {
                        if (Misc.Model.Keymap.KeybindConflictCheck(Misc.Model.Keymap.LiveGet(idx), idx))
                        {
                            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, PrefsDialogState.SelectedKeybindSlot + 3, itemKind, itemHandle, itemRect);
                            MacToolbox.InvalRect(itemRect);
                            PrefsDialogState.SelectedKeybindSlot = idx;
                            MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, PrefsDialogState.SelectedKeybindSlot + 3, itemKind, itemHandle, itemRect);
                            MacToolbox.InvalRect(itemRect);
                            MacToolbox.SysBeep(0);
                            conflict = true;
                            break;
                        }
                    }
                    if (!conflict)
                    {
                        Misc.Model.Keymap.UnpackKeyBindings();
                        GamePrefs.MasterVolume = GamePrefs.DialogWorkingVolume;
                        GamePrefs.IntroMusicEnabled = introMusic;
                        GamePrefs.PrefByte551 = pref551;
                        GamePrefs.QuickTimeMoviesDisabled = qtMovies;
                        GamePrefs.UseQuickdraw = useQuickdraw;
                        GamePrefs.ProjectileStreaksDisabled = streaksDisabled;
                        // speed = (percent + 50) / C1 — the decompile's PowerPC signed
                        // int-to-double magic pair collapses to this plain cast+divide.
                        PrefsDialogState.GameSpeed =
                            (PrefsDialogState.GameSpeedPercent + 50) / GameSpeedScale.SliderScale;
                        done = true;
                        WritePrefsToDisk.Run();
                    }
                }
                if (itemHit == 35)
                {
                    done = true;
                }
                if (2 < itemHit && itemHit < 34)
                {
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, PrefsDialogState.SelectedKeybindSlot + 3, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    PrefsDialogState.SelectedKeybindSlot = (short)(itemHit - 3);
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, PrefsDialogState.SelectedKeybindSlot + 3, itemKind, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                }
                if (itemHit == 39)   // volume UP: pressed PICT flash, then bump
                {
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 39, itemKind, itemHandle, itemRect);
                    MacToolbox.DrawPicture(PrefsDialogState.VolumePicts[2], itemRect);
                    MacToolbox.Delay(5, 0);   // finalTicks out-param discarded
                    MacToolbox.DrawPicture(PrefsDialogState.VolumePicts[0], itemRect);
                    if (GamePrefs.DialogWorkingVolume < 8)
                    {
                        MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 37, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        GamePrefs.DialogWorkingVolume = (short)(GamePrefs.DialogWorkingVolume + 1);
                        Sound.SetMasterVolume.Run((ushort)(GamePrefs.DialogWorkingVolume << 5));
                        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    }
                    else
                    {
                        MacToolbox.SysBeep(0);
                    }
                }
                if (itemHit == 38)   // volume DOWN
                {
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 38, itemKind, itemHandle, itemRect);
                    MacToolbox.DrawPicture(PrefsDialogState.VolumePicts[3], itemRect);
                    MacToolbox.Delay(5, 0);   // finalTicks out-param discarded
                    MacToolbox.DrawPicture(PrefsDialogState.VolumePicts[1], itemRect);
                    if (GamePrefs.DialogWorkingVolume < 1)
                    {
                        MacToolbox.SysBeep(0);
                    }
                    else
                    {
                        MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 37, itemKind, itemHandle, itemRect);
                        MacToolbox.InvalRect(itemRect);
                        GamePrefs.DialogWorkingVolume = (short)(GamePrefs.DialogWorkingVolume - 1);
                        Sound.SetMasterVolume.Run((ushort)(GamePrefs.DialogWorkingVolume << 5));
                        Sound.SndPlay.Run(Sound.Model.CombatSoundCells.UiSoundBankA[4], 1, 128, 128);
                    }
                }
                if (itemHit == 2)
                {
                    useQuickdraw = (byte)(useQuickdraw == 0 ? 1 : 0);
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 2, itemKind, itemHandle, itemRect);
                    if (useQuickdraw != 0)
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 1);
                    }
                    else
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 0);
                    }
                }
                if (itemHit == 40)
                {
                    introMusic = (byte)(introMusic == 0 ? 1 : 0);
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 40, itemKind, itemHandle, itemRect);
                    if (introMusic != 0)
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 1);
                    }
                    else
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 0);
                    }
                }
                if (itemHit == 41)
                {
                    qtMovies = (byte)(qtMovies == 0 ? 1 : 0);
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 41, itemKind, itemHandle, itemRect);
                    if (qtMovies != 0)
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 0);
                    }
                    else
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 1);
                    }
                }
                if (itemHit == 42)
                {
                    GameSpeedDialog.Run();
                    MacToolbox.SetPort(PrefsDialogState.DialogWindow);
                    MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(PrefsDialogState.DialogWindow));
                }
                if (itemHit == 43)
                {
                    streaksDisabled = (byte)(streaksDisabled == 0 ? 1 : 0);
                    MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, 43, itemKind, itemHandle, itemRect);
                    if (streaksDisabled != 0)
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 1);
                    }
                    else
                    {
                        MacToolbox.SetControlValue(itemHandle[0], 0);
                    }
                }
            } while (!done);
            if (GamePrefs.IntroMusicEnabled == 0)
            {
                Sound.DisposeSoundFileChannel.Run(false);
            }
            Graphics.SetGamePortAndDevice.Run();
            for (short i = 0; i < PrefsDialogState.VolumePicts.Length; i++)
            {
                if (PrefsDialogState.VolumePicts[i] != 0)
                {
                    MacToolbox.HPurge(PrefsDialogState.VolumePicts[i]);
                    MacToolbox.ReleaseResource(PrefsDialogState.VolumePicts[i]);
                }
            }
            if (PrefsDialogState.Pict132Handle != 0)
            {
                MacToolbox.HPurge(PrefsDialogState.Pict132Handle);
                MacToolbox.ReleaseResource(PrefsDialogState.Pict132Handle);
            }
            MacToolbox.DisposeRoutineDescriptor(modalUpp);
            MacToolbox.DisposeDialog(PrefsDialogState.DialogWindow);
            Graphics.DrawTitleSecondaryPict.Run();
        }
    }
}
