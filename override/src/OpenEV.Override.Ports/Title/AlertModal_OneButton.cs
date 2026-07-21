using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_1003df50 (EV Override-11.c lines 25369-25419).
// Modal 1-button alert: DLOG 3000, OK dismisses via item 1.
public static class AlertModal_OneButton
{
    public static void Run(string message)
    {
        // FUN_10076178 copied at most 254 bytes of the message.
        message ??= "";
        if (message.Length > 254) message = message.Substring(0, 254);

        SetGamePortAndDevice.Run();
        GameData.AlertDialog = 0;
        int dlg = MacToolbox.GetNewDialog(3000, 0, -1);
        GameData.AlertDialog = dlg;
        if (dlg == 0)
            return;

        MacToolbox.SetPort(dlg);
        MacToolbox.SetDialogDefaultItem(dlg, 1);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.DrawDialog(dlg);
        DrawDefaultButtonOutline.Run(dlg, 1);

        // Message → static-text item 3 (the Mac GetDialogItem handle-out dance).
        MacToolbox.SetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 3), message);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);

        bool done = false;
        short itemHit = default;
        do
        {
            MacToolbox.ModalDialog(0, ref itemHit);
            if (itemHit == 1)
            {
                done = true;
            }
        } while (!done);

        MacToolbox.HideWindow(dlg);
        SetGamePortAndDevice.Run();
        MacToolbox.DisposeDialog(dlg);
        SetGamePortAndDevice.Run();
    }
}
