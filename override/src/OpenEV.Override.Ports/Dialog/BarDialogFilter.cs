using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000a3ac (EV Override-11.c lines 5480-5541) — the BAR dialog's modal
// filter. Any key/click resets the mash counter; Return/Enter leaves (item 1);
// clicks track the 6-button row (TrackBarButtonHit) or hit nothing (-1);
// idle events tick the mash counter and when it beats rng(0x96) fire item 6
// (the bar-person mission encounter); update events redraw the bar.
//
// Port dispatch: registered under SpaceportGlobals.BarFilterProc. ModalDialog
// forwards mouseDown (what==1) to the filter and also returns a POSITIVE
// itemHit set on a NULL event — so both the button clicks AND the mash-timer
// bar-person encounter (item 6, fired on idle null events) work. ModalDialog
// now also forwards keyDown/autoKey (what==3/5) before falling back to its own
// typed-key-drain default-item handling, so this filter's Return/Enter→item 1
// branch is live too.
//
// NOTE this filter reads the raw charCode (decompile 5490 `(char)*(param_2+1)`
// — NO keymap translation).
public static class BarDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            DialogScratch.SpaceportMashCounter = 0;
            byte keyChar = (byte)evt.Message;
            if (keyChar == '\r' || keyChar == '\x03')
            {
                itemHit = 1;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            DialogScratch.SpaceportMashCounter = 0;
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short hit = (short)TrackBarButtonHit.Run(mousePoint);
            switch (hit)
            {
                case 0: itemHit = 1; break;
                case 1: itemHit = 2; break;
                case 2: itemHit = 3; break;
                case 4: itemHit = 5; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        DialogScratch.SpaceportMashCounter = (short)(DialogScratch.SpaceportMashCounter + 1);
        // DEVIATION (host substrate, test-only): SuppressBarMashTimer has no decompile
        // counterpart — see DialogScratch.SuppressBarMashTimer for why it's Mac-invisible.
        if (!DialogScratch.SuppressBarMashTimer && SeedEvoRng.Run(150) < DialogScratch.SpaceportMashCounter)
        {
            itemHit = 6;
            DialogScratch.SpaceportMashCounter = 0;
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.BeginUpdate(DialogScratch.SpaceportDialogRecord);
            RedrawBarDialog.Run();
            MacToolbox.EndUpdate(DialogScratch.SpaceportDialogRecord);
            return 0;
        }
        return 0;
    }
}
