using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003e6e4 (EV Override-11.c lines 25622-25676) — the generic alert /
// news-scene dialog's modal filter (window = *_DAT_10080c64, the managed
// GameData.AlertDialog). Keymap Action9 fires item 4 (the map button
// DoSceneTransition handles); Return (13) / Enter (3) fire item 1; mouse-downs
// track the single OK button (item 1); update events redraw via
// RedrawGenericAlertDialog. Registered under GenericAlertDialogFilter.FilterProc
// by DoSceneTransition.
public static class GenericAlertDialogFilter
{
    // Modal-filter proc key (was UPP source cell 0x10080fe4 -> FUN_1003e6e4).
    public const int FilterProc = 0x1003e6e4;

    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int handled;
        Keymap.RefreshCachedKeymap();
        short abortFlag = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9));
        if (abortFlag == 0)
        {
            if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) &&
               ((byte)evt.Message == '\r' || (byte)evt.Message == '\x03'))
            {
                itemHit = 1;
                handled = 1;
            }
            else if (evt.WhatType == MacEventType.MouseDown)
            {
                var itemType = new short[1];     // GetDialogItem itemType out (never read back)
                var itemHandle = new int[1];     // GetDialogItem handle out (never read back)
                var itemRect = new short[4];     // the OK-button Rect
                MacToolbox.SetPort(GameData.AlertDialog);
                MacToolbox.GetDialogItem(GameData.AlertDialog, 1, itemType, itemHandle, itemRect);
                bool buttonHit = TrackSingleButtonClick.Run(itemRect);
                DrawButtonPressed.Run(itemRect, false);
                if (!buttonHit)
                {
                    itemHit = -1;   // 0xffff
                }
                else
                {
                    itemHit = 1;
                }
                handled = 1;
            }
            else
            {
                if (evt.WhatType == MacEventType.UpdateEvt)
                {
                    MacToolbox.BeginUpdate(GameData.AlertDialog);
                    RedrawGenericAlertDialog.Run();
                    MacToolbox.EndUpdate(GameData.AlertDialog);
                }
                handled = 0;
            }
        }
        else
        {
            itemHit = 4;
            handled = 1;
        }
        return handled;
    }
}
