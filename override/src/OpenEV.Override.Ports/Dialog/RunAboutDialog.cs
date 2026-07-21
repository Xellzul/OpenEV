using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003e960 (EV Override-11.c lines 25712-25772).
// ModalDialog receives local_22 (short[5]) — the short[] overload (MacToolbox.cs:287) sets [0]=1, loop exits correctly.
public static class RunAboutDialog
{
    // Port bridge for the modal-filter UPP (cell 0x10080fe0 → TVector 0x10082518 →
    // FUN_1003eb2c = PictureAlertDialogFilter) — typed MacEvent shape.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = PictureAlertDialogFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    public static void Run()
    {
        short[] itemHit = new short[5];

        // _DAT_10080fdc → DialogScratch.AlertPictHandle (background PICT 150);
        // *ButtonPictPairSlot → DialogScratch.ButtonPictPair (credit PICT pair).
        int[] creditPics = DialogScratch.ButtonPictPair;
        bool done = false;
        // _DAT_10080fe0 — about-dialog modal filter UPP cell; named proc key +
        // typed registration replace the raw cell read.
        int filterUpp = MacToolbox.NewRoutineDescriptor(PictureAlertDialogFilter.FilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(PictureAlertDialogFilter.FilterProc, FilterAdapter);
        for (int loadIndex = 0; loadIndex < creditPics.Length; loadIndex++)
        {
            creditPics[loadIndex] = MacToolbox.GetPicture(loadIndex + 0x1b8e);
        }
        DialogScratch.AlertPictHandle = MacToolbox.GetPicture(0x96);
        GameData.AlertDialog = 0;
        GameData.AlertDialog = MacToolbox.GetNewDialog(0xfa6, 0, -1);
        if (GameData.AlertDialog != 0)
        {
            NewDialogHook.Run(GameData.AlertDialog, 0);
            RecenterWindowIntoPlayArea.Run(GameData.AlertDialog);
            MacToolbox.ShowWindow(GameData.AlertDialog);
            MacToolbox.SelectWindow(GameData.AlertDialog);
            MacToolbox.SetPort(GameData.AlertDialog);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            do
            {
                MacToolbox.ModalDialog(filterUpp, ref itemHit[0]);
                if (itemHit[0] == 1)
                {
                    done = true;
                }
            } while (!done);
            for (short purgeIndex = 0; purgeIndex < creditPics.Length; purgeIndex = (short)(purgeIndex + 1))
            {
                if (creditPics[purgeIndex] != 0)
                {
                    MacToolbox.HPurge(creditPics[purgeIndex]);
                    MacToolbox.ReleaseResource(creditPics[purgeIndex]);
                }
            }
            if (DialogScratch.AlertPictHandle != 0)
            {
                MacToolbox.HPurge(DialogScratch.AlertPictHandle);
                MacToolbox.ReleaseResource(DialogScratch.AlertPictHandle);
            }
            SetGamePortAndDevice.Run();
            MacToolbox.DisposeRoutineDescriptor(filterUpp);
            MacToolbox.DisposeDialog(GameData.AlertDialog);
        }
    }
}
