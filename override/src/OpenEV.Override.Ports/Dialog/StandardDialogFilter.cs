using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// Decompile: EV Override-11.c lines 50348-50377.
// The generic modal filter RunMultiButtonModalDialog installs: maps cmd-period
// to the cancel button and Return/Enter to the default button (1-based DITL
// item = button index + 1, because item 1 is the statText).
public static class StandardDialogFilter
{
    // Modal-filter proc key (was UPP source cell 0x10081a8c -> FUN_100770ac).
    public const int FilterProc = 0x100770ac;

    // The default/cancel button indices RunMultiButtonModalDialog publishes for
    // this filter — were the shorts behind ptr cells 0x10081a94 (toc-0x6bcc,
    // default) / 0x10081a98 (toc-0x6bc8, cancel); both ends migrated.
    public static short DefaultButtonIndex;
    public static short CancelButtonIndex;

    // ModalDialog forwards mouseDown (what=1) and returns a positive null-event
    // itemHit; it also forwards keyDown (what=3) before its own typed-char →
    // default-item fallback, so the cmd-period-cancel and Return/Enter-default
    // paths below are live (NOTE: cmd-period still needs a physical '.'-while-
    // Cmd-held keystroke to actually reach the typed-char queue — only Return/
    // Enter are bridged past the printable-only SDL TextInput gate so far).
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if (evt.WhatType == MacEventType.KeyDown)
        {
            byte keyChar = (byte)(evt.Message & 0xff);
            int cmdKeyBit = evt.Modifiers & 0x100;
            if (cmdKeyBit != 0 && keyChar == '.')
            {
                // **(toc-0x6bc8) — the cancel-button index (was cell 0x10081a98, managed now).
                itemHit = (short)(CancelButtonIndex + 1);
                MacToolbox.HiliteControl(MacToolbox.GetDialogItemHandle(dialog, itemHit), 1);
                return 1;
            }
            if (keyChar == '\r' || keyChar == '\x03')
            {
                // **(toc-0x6bcc) — the default-button index (was cell 0x10081a94, managed now).
                itemHit = (short)(DefaultButtonIndex + 1);
                MacToolbox.HiliteControl(MacToolbox.GetDialogItemHandle(dialog, itemHit), 1);
                return 1;
            }
        }
        return 0;
    }
}
