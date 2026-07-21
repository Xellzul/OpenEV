using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100108b0 (EV Override-11.c lines 8587-8638; ASM: reference/disasm/_code_interstitial.asm
// loc_108B0 — no top-level `sub_`, reached only via the filter-proc pointer table off_82498) —
// the modal filter of the spaceport-PERSON comm dialog (SpaceportPersonDialog = FUN_1000f4f8,
// DLOG 0x3ef) — do not confuse with the unrelated spob hail / planet DOMINATION/tribute
// comm dialog (ShowSpobHailDialog, FUN_10010f70, DLOG 0x3f1).
// Keys: Return/Enter/'e' = leave (item 1), 'r' = item 2, 'g' = item 3; mouse-downs map the four
// comm button zones (HitTestCommButtonRow) onto items 1..4; update events redraw via
// DrawOutfitterItemPanel. Registered as the filter proc in SpaceportPersonDialog.FilterAdapter.
public static class SpaceportPersonDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if (keyChar == '\r' || keyChar == '\x03' || keyChar == 'e')
            {
                itemHit = 1;
                return 1;
            }
            if (keyChar == 'r')
            {
                itemHit = 2;
                return 1;
            }
            if (keyChar == 'g')
            {
                itemHit = 3;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short hitRegion = (short)HitTestCommButtonRow.Run(mousePoint);
            itemHit = (hitRegion < 0 || 3 < hitRegion) ? (short)-1 : (short)(hitRegion + 1);
            return 1;
        }

        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            // BeginUpdate/EndUpdate both target the same cell (SpaceportCommDialogRecord); the
            // decompile's EndUpdate arg renders as a read through an uninitialized local, but
            // the ASM (loc_108B0) shows it's really a TOC-relative access to that same cell.
            MacToolbox.BeginUpdate(DialogScratch.SpaceportCommDialogRecord);
            DrawOutfitterItemPanel.Run();
            MacToolbox.EndUpdate(DialogScratch.SpaceportCommDialogRecord);
        }
        return 0;
    }
}
