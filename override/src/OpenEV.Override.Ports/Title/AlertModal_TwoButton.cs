using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_1003e0c8 (EV Override-11.c lines 25420-25473).
// DLOG 3002 — Yes/No (or OK/Cancel) modal: returns 1 on OK (item 1), 0 on
// Cancel (item 5).
public static class AlertModal_TwoButton
{
    public static int Run(string message)
    {
        // FUN_10076178 copied at most 254 bytes of the message.
        message ??= "";
        if (message.Length > 254) message = message.Substring(0, 254);

        GameData.AlertDialog = 0;
        int dlg = MacToolbox.GetNewDialog(3002, 0, -1);
        GameData.AlertDialog = dlg;
        // Decompile's `iVar3 = unaff_r30` before the loop is dead (the loop
        // always overwrites the value before returning); this initializer is
        // only actually read by the dlg == 0 early return below.
        int result = 0;
        if (dlg == 0)
            return result;

        MacToolbox.SetDialogDefaultItem(dlg, 1);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        DrawDefaultButtonOutline.Run(dlg, 1);

        // Message → static-text item 3 (the Mac GetDialogItem handle-out dance).
        MacToolbox.SetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 3), message);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);   // flush all but disk-inserted

        bool done = false;
        short itemHit = default;
        do
        {
            MacToolbox.ModalDialog(0, ref itemHit);
            if (itemHit == 1)
            {
                result = 1;
                done = true;
            }
            if (itemHit == 5)
            {
                result = 0;
                done = true;
            }
        } while (!done);

        SetGamePortAndDevice.Run();
        MacToolbox.HideWindow(dlg);
        MacToolbox.DisposeDialog(dlg);
        SetGamePortAndDevice.Run();
        return result;
    }
}
