using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003eb2c (EV Override-11.c lines 25773-25818) — the picture-alert (About
// EVO, DLOG 0xfa6 with the PICT 0x96 panel) dialog's modal filter (window =
// *_DAT_10080c64, the managed GameData.AlertDialog). Return (13) /
// Enter (3) fire item 1; mouse-downs track the single OK button (item 1);
// update events redraw via RedrawPictureAlertDialog. Registered under
// PictureAlertDialogFilter.FilterProc by RunAboutDialog.
public static class PictureAlertDialogFilter
{
    // Modal-filter proc key (was UPP source cell 0x10080fe0 -> FUN_1003eb2c).
    public const int FilterProc = 0x1003eb2c;

    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int filterResult;

        if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) &&
           ((byte)evt.Message == 13 || (byte)evt.Message == 3))
        {
            itemHit = 1;
            filterResult = 1;
        }
        else if (evt.WhatType == MacEventType.MouseDown)
        {
            var itemType = new short[1];     // GetDialogItem itemType out (never read back)
            var itemHandle = new int[1];     // GetDialogItem handle out (never read back)
            var itemRect = new short[4];     // the OK-button Rect
            MacToolbox.SetPort(GameData.AlertDialog);
            MacToolbox.GetDialogItem(GameData.AlertDialog, 1, itemType, itemHandle, itemRect);
            bool buttonDown = TrackSingleButtonClick.Run(itemRect);
            DrawButtonPressed.Run(itemRect, false);
            if (!buttonDown)
            {
                itemHit = -1;   // 0xffff
            }
            else
            {
                itemHit = 1;
            }
            filterResult = 1;
        }
        else
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                MacToolbox.BeginUpdate(GameData.AlertDialog);
                RedrawPictureAlertDialog.Run();
                MacToolbox.EndUpdate(GameData.AlertDialog);
            }
            filterResult = 0;
        }
        return filterResult;
    }
}
