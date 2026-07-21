using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Title;

// FUN_10046054 (EV Override-11.c lines 29171-29258) — the "Name your pilot"
// entry dialog (DLOG 0xc1c = 3100). Item map: 1 = OK, 2 = Strict-play checkbox,
// 5 = name edit field, 6 = Cancel. Returns 1 on OK (typed name validated
// against maxLen), 0 on Cancel.
public static class NewPilotDialog
{
    public static int Run(string defaultName, int maxLen, ref byte strictFlag)
    {
        byte strictPlay = strictFlag;
        int dlg = MacToolbox.GetNewDialog(0xc1c, 0, -1);
        // Mac: the result lives in r27, UNINITIALIZED if the dialog fails to
        // load (decompile `unaff_r27`); 0 here.
        int result = 0;
        if (dlg == 0)
            return result;

        MacToolbox.SetDialogDefaultItem(dlg, 1);
        NewDialogHook.Run(dlg, 0);
        MacToolbox.ShowWindow(dlg);
        MacToolbox.SelectWindow(dlg);
        MacToolbox.SetPort(dlg);
        MacToolbox.DrawDialog(dlg);
        DrawDefaultButtonOutline.Run(dlg, 1);
        if (strictPlay == 0)
        {
            MacToolbox.SetControlValue(MacToolbox.GetDialogItemHandle(dlg, 2), 0);
        }
        else
        {
            MacToolbox.SetControlValue(MacToolbox.GetDialogItemHandle(dlg, 2), 1);
        }

        // Seed the name field (FUN_10076178 capped defaultName at 254 bytes).
        string boundedName = defaultName ?? "";
        if (boundedName.Length > 254) boundedName = boundedName.Substring(0, 254);
        MacToolbox.SetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5), boundedName);
        MacToolbox.SelectDialogItemText(dlg, 5, 0, 254);
        MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);

        bool done = false;
        short itemHit = default;
        do
        {
            MacToolbox.ModalDialog(0, ref itemHit);
            if (itemHit == 1)
            {
                string typedName = MacToolbox.GetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5));
                if ((short)maxLen < (short)typedName.Length)
                {
                    MacToolbox.SysBeep(0);
                    MacToolbox.SelectDialogItemText(dlg, 5, 0, maxLen - 1);
                }
                else
                {
                    strictFlag = strictPlay;   // persist the toggle on OK only
                    result = 1;
                    done = true;
                }
            }
            if (itemHit == 2)
            {
                strictPlay = (byte)(strictPlay == 0 ? 1 : 0);
                if (strictPlay != 0)
                {
                    MacToolbox.SetControlValue(MacToolbox.GetDialogItemHandle(dlg, 2), 1);
                }
                else
                {
                    MacToolbox.SetControlValue(MacToolbox.GetDialogItemHandle(dlg, 2), 0);
                }
            }
            if (itemHit == 6)
            {
                result = 0;
                done = true;
            }
        } while (!done);

        // Capture the typed name into PilotIdentity.CapturedNameEntry.
        PilotIdentity.CapturedNameEntry = MacToolbox.GetDialogItemText(MacToolbox.GetDialogItemHandle(dlg, 5));
        SetGamePortAndDevice.Run();
        MacToolbox.DisposeDialog(dlg);
        SetGamePortAndDevice.Run();
        return result;
    }
}
