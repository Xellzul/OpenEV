using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003dd88 (EV Override-11.c lines 25316-25326): dispose the generic-alert
// dialog (`DisposeDialog(*_DAT_10080fe8)` — the managed
// DialogScratch.GenericAlertDialog now; ShowGenericAlert stores it).
public static class DisposeCurrentAlertDialog
{
    public static void Run()
    {
        MacToolbox.DisposeDialog(DialogScratch.GenericAlertDialog);
    }
}
