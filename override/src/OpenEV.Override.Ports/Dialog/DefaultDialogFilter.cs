using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1007328c (EV Override-11.c lines 47603-47637) — modal filter
// installed by the shareware nag (ShowSharewareNagDialog, DLOG 900):
// re-enables the dimmed item-2 "Not Yet" button once the SetWRefCon hold-off
// stamp is 300+ ticks old, and maps a plain (no-cmd) Return/Enter to item 1
// with a press-flash.
public static class DefaultDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];
        var finalTicks = new int[1];

        int handled = 0;
        // Arg-less GetWRefCon, as in the decompile's glue call.
        uint lastHiliteTicks = (uint)MacToolbox.GetWRefCon();
        if (lastHiliteTicks != 0)
        {
            uint ticks = MacToolbox.TickCount();
            // Two separate TickCount() calls, as in the original; both compares are
            // unsigned per the ASM (cmplwi/cmplw).
            if (300 < ticks - lastHiliteTicks && lastHiliteTicks < MacToolbox.TickCount())
            {
                MacToolbox.GetDialogItem(dialog, 2, itemType, itemHandle, itemRect);
                MacToolbox.HiliteControl(itemHandle[0], 0);
                MacToolbox.SetWRefCon(dialog, 0);
            }
        }
        int cmdKeyBit = evt.Modifiers & 0x100;
        if ((evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey) && cmdKeyBit == 0)
        {
            byte keyChar = (byte)(evt.Message & 0xff);
            if (keyChar == '\x03' || keyChar == '\r')
            {
                itemHit = 1;
                handled = 1;
                MacToolbox.GetDialogItem(dialog, 1, itemType, itemHandle, itemRect);
                MacToolbox.HiliteControl(itemHandle[0], 1);
                MacToolbox.Delay(8, finalTicks);
                MacToolbox.HiliteControl(itemHandle[0], 0);
            }
        }
        return handled;
    }
}
