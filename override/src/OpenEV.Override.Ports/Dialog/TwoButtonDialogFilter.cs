using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100134dc (EV Override-11.c lines 9668-9713) — the buy/sell HAGGLE (two-
// button, DLOG 0x3f0) dialog's modal filter. Return (13) / Enter (3) fire item 1;
// mouse-downs track the two-button row (TrackTwoButtonDialog) onto items 1/2
// (miss = -1); update events redraw the comm-status line between Begin/EndUpdate
// on the haggle dialog window. Registered under DialogScratch.TwoButtonFilterProc
// by ShowBuyShipDialog.
//
// Dialog 4-rules rewrite (B10): typed MacEvent filter (was the legacy byte-address
// EventRecord shape).
public static class TwoButtonDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int handled;

        if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) &&
           ((byte)evt.Message == '\r' || (byte)evt.Message == '\x03'))
        {
            itemHit = 1;
            handled = 1;
        }
        else if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);   // local_18[0] = *(evt+10)
            short buttonIndex = (short)TrackTwoButtonDialog.Run(mousePoint);   // FUN_1000e8ec
            if (buttonIndex == 0)
            {
                itemHit = 1;
                handled = 1;
            }
            else if (buttonIndex == 1)
            {
                itemHit = 2;
                handled = 1;
            }
            else
            {
                itemHit = -1;   // 0xffff
                handled = 1;
            }
        }
        else
        {
            if (evt.WhatType == MacEventType.UpdateEvt)
            {
                MacToolbox.BeginUpdate(DialogScratch.BuyShipDialogRecord);
                Misc.RedrawCommStatusLine.Run();   // FUN_10013614
                // Decompile EndUpdate(*(local_4c + -0x1b9c)) — a decompiled TOC artifact for the
                // SAME cell BeginUpdate reads (GameToc-0x1b9c = _DAT_10086ac4), which is the
                // managed DialogScratch.BuyShipDialogRecord now.
                MacToolbox.EndUpdate(DialogScratch.BuyShipDialogRecord);
            }
            handled = 0;
        }
        return handled;
    }
}
