using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100410b4 (EV Override-11.c lines 26704-26755) — the 2-button confirm
// (DLOG 0x3fa) dialog's modal filter: 'm' fires item 1, 'e' fires item 2;
// mouse-downs track the 2-button row (TrackDialogButtonClick) onto items
// 1/2; update events redraw via RedrawConfirmYesNoDialog. Registered under
// RunConfirmYesNoDialog.ConfirmFilterProc.
public static class ConfirmYesNoDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int handled;

        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)Misc.LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if (keyChar == 'm')
            {
                itemHit = 1;
                return 1;
            }
            if (keyChar == 'e')
            {
                itemHit = 2;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short hitButton = (short)TrackDialogButtonClick.Run(mousePoint);
            if (hitButton == 0)
            {
                itemHit = 1;
                handled = 1;
            }
            else if (hitButton == 1)
            {
                itemHit = 2;
                handled = 1;
            }
            else
            {
                itemHit = -1;
                handled = 1;
            }
        }
        else
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                // BeginUpdate(*_DAT_10080c64) / EndUpdate(**(toc-0x79fc)) — the
                // same alert-dialog ptr cell (-> GameData.AlertDialog).
                MacToolbox.BeginUpdate(GameData.AlertDialog);
                RedrawConfirmYesNoDialog.Run();
                MacToolbox.EndUpdate(GameData.AlertDialog);
            }
            handled = 0;
        }
        return handled;
    }
}
