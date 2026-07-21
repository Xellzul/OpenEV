using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003dd08 (EV Override-11.c lines 25295-25315): open + show the generic
// alert dialog (DLOG 0x82) into the managed DialogScratch.GenericAlertDialog
// (was the cell behind ptr cell 0x10080fe8; DisposeCurrentAlertDialog pairs).
public static class ShowGenericAlert
{
    public static void Run()
    {
        DialogScratch.GenericAlertDialog = MacToolbox.GetNewDialog(0x82, 0, -1);
        int dlg = DialogScratch.GenericAlertDialog;
        NewDialogHook.Run(dlg, 0);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.DrawDialog(dlg);
    }
}
