using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// Port of FUN_1003e23c (EV Override-11.c lines 25474-25541).
// DLOG 3001 — modal with an editable-text item (item 5) plus OK (item 1) /
// Cancel (item 6) buttons. Used by New Pilot confirm / ship-christen prompts.
// Returns 1 on OK (entered text length <= maxLen), 0 on Cancel.
public static class AlertModal_ThreeButton
{
    public static int Run(string promptStr, string defaultText, int maxLen)
    {
        GameData.AlertDialog = 0;
        GameData.AlertDialog = MacToolbox.GetNewDialog(3001, 0, -1);
        if (GameData.AlertDialog == 0)
            return 0;

        int dlg = GameData.AlertDialog;
        MacToolbox.SetDialogDefaultItem(dlg, 1);
        NewDialogHook.Run(dlg, 0);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        DrawDefaultButtonOutline.Run(dlg, 1);

        MacToolbox.SetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 3), promptStr);

        // Default text truncated to 254 chars (FUN_10076178's copy limit).
        string defaultStr = defaultText ?? "";
        if (defaultStr.Length > 254) defaultStr = defaultStr.Substring(0, 254);
        MacToolbox.SetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5), defaultStr);
        MacToolbox.SelectDialogItemText(dlg, 5, 0, 254);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);

        int result = 0;  // definite-assignment default; the ASM's r29 is never read until
                         // one of the two loop-exit branches below has just set it
        bool done = false;
        short itemHit = default;
        do
        {
            MacToolbox.ModalDialog(0, ref itemHit);
            if (itemHit == 1)
            {
                string entered = MacToolbox.GetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5));
                if (maxLen < entered.Length)
                {
                    MacToolbox.SysBeep(0);
                    MacToolbox.SelectDialogItemText(dlg, 5, 0, maxLen - 1);
                }
                else
                {
                    result = 1;
                    done = true;
                }
            }
            if (itemHit == 6)
            {
                result = 0;
                done = true;
            }
        } while (!done);

        PilotIdentity.CapturedNameEntry = MacToolbox.GetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5));
        SetGamePortAndDevice.Run();
        MacToolbox.DisposeDialog(dlg);
        SetGamePortAndDevice.Run();
        return result;
    }
}
