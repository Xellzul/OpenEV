using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100153a0 (EV Override-11.c lines 10399-10454; ASM: _code_interstitial.asm
// loc_153A0, orig lines 27564-27710 — NOT FUN_10014ae8__CommodityPricing.asm) —
// modal filter of the spaceport BARTER / six-button dialog family
// (DialogScratch.BoardingDialogRecord). Return/Enter fires item 1; mouse-downs
// track the six-button row (Track6ButtonMouseDown) onto items 1,2,3,4,6,7;
// update events redraw via RedrawPilotInfoPanel. Registration (UPP cell
// 0x10080cf0) happens in ShowBoardingDialog, not here.
//
// charCode is the low byte of Message (decompile line 10408) — no keymap
// translation in this filter.
public static class BoardingDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int handled;

        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey &&
            ((byte)evt.Message == '\r' || (byte)evt.Message == '\x03'))
        {
            itemHit = 1;
            handled = 1;
        }
        else if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short hitButton = (short)Track6ButtonMouseDown.Run(mousePoint);
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
            else if (hitButton == 2)
            {
                itemHit = 3;
                handled = 1;
            }
            else if (hitButton == 3)
            {
                itemHit = 4;
                handled = 1;
            }
            else if (hitButton == 4)
            {
                itemHit = 6;
                handled = 1;
            }
            else if (hitButton == 5)
            {
                itemHit = 7;
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
                MacToolbox.BeginUpdate(DialogScratch.BoardingDialogRecord);
                RedrawPilotInfoPanel.Run();
                // Decompile renders this EndUpdate arg as a stray local (the decompiler
                // lost track of r2 across the calls) — the ASM re-derives the same toc
                // cell as BeginUpdate's arg, so BoardingDialogRecord here is correct.
                MacToolbox.EndUpdate(DialogScratch.BoardingDialogRecord);
            }
            handled = 0;
        }
        return handled;
    }
}
