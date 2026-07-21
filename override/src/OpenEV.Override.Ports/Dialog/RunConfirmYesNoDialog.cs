using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10040eb8 (EV Override-11.c lines 26636-26703) — runs the 2-button
// confirm dialog (DLOG 0x3fa; uses the shared alert-dialog window cell + the
// 2-button PICT array Render2ButtonRow draws), paired with the modal filter
// ConfirmYesNoDialogFilter. Item 1 = yes (true), item 2 = no (false).
public static class RunConfirmYesNoDialog
{
    // Modal-filter proc key (was UPP source cell 0x10080fd0 -> FUN_100410b4 =
    // ConfirmYesNoDialogFilter).
    public const int ConfirmFilterProc = 0x100410b4;

    // Bridge for the modal-filter UPP — typed MacEvent shape.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = ConfirmYesNoDialogFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    public static bool Run()
    {
        int[] picts = DialogScratch.ConfirmButtonPicts;
        int modalUpp = MacToolbox.NewRoutineDescriptor(ConfirmFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(modalUpp, FilterAdapter);
        picts[0] = MacToolbox.GetPicture(0x1bca);   // yes normal
        picts[1] = MacToolbox.GetPicture(0x1bcb);   // yes pressed
        picts[2] = MacToolbox.GetPicture(0x1bc8);   // no normal
        picts[3] = MacToolbox.GetPicture(0x1bc9);   // no pressed
        GameData.AlertDialog = 0;
        GameData.AlertDialog = MacToolbox.GetNewDialog(0x3fa, 0, -1);

        // unaff_r28 in the decompile (an untracked-register artifact) — every
        // reachable path assigns it before return, so `default` here is safe.
        bool result = default;
        if (GameData.AlertDialog == 0)
        {
            result = false;
        }
        else
        {
            NewDialogHook.Run(GameData.AlertDialog, 0);
            Graphics.RecenterWindowIntoPlayArea.Run(GameData.AlertDialog);
            MacToolbox.ShowWindow(GameData.AlertDialog);
            MacToolbox.SelectWindow(GameData.AlertDialog);
            MacToolbox.SetPort(GameData.AlertDialog);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            short hitItem = default;
            bool done = false;
            do
            {
                MacToolbox.ModalDialog(modalUpp, ref hitItem);
                if (hitItem == 1)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                    result = true;
                    done = true;
                }
                if (hitItem == 2)
                {
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 128, 128);
                    result = false;
                    done = true;
                }
            } while (!done);
            foreach (int pict in picts)
            {
                if (pict != 0)
                {
                    MacToolbox.HPurge(pict);
                    MacToolbox.ReleaseResource(pict);
                }
            }
            Graphics.SetGamePortAndDevice.Run();
            MacToolbox.DisposeRoutineDescriptor(modalUpp);
            MacToolbox.DisposeDialog(GameData.AlertDialog);
        }
        return result;
    }
}
