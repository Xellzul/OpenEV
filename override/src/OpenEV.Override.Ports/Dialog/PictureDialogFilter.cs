using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1003c864 (EV Override-11.c lines 24836-24934) — the shipyard SHIP-SPECS
// sub-dialog's modal filter (window = *_DAT_10080fec, the managed
// ShipyardState.SpecsDialogWindow). Keymap Action9 fires item 4 (map),
// Action28 item 2 (player info), Action43 item 6 (mission info);
// Return/Enter (via the unshifted key table) fire item 1; mouse-downs track
// the OK button (item 1) with the pressed/normal PICT pair
// (ShipyardState.Picts[5]/[4] = *(_DAT_10080c94+0x14)/(+0x10)); update events
// redraw via DrawDialog + DrawShipyardInfoDialog. Registered under
// ShipyardState.SpecsFilterProc by RunShipSpecsDialog.
//
// Dialog 4-rules rewrite (B10): typed MacEvent filter; GetDialogItem outs are
// managed arrays; the mouse Point is the packed GetMouse()/WherePacked int.
public static class PictureDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        var itemType = new short[1];   // auStack_30 — GetDialogItem itemType out (never read back)
        var itemHandle = new int[1];     // auStack_34 — GetDialogItem handle out (never read back)
        var itemRect = new short[4];   // auStack_2e — the OK-button Rect

        // *puVar3 (puVar3 = _DAT_10080fec): the slot holds a pointer to the
        // DialogPtr cell — managed ShipyardState.SpecsDialogWindow now.
        int dialogHandle = ShipyardState.SpecsDialogWindow;
        Keymap.RefreshCachedKeymap();   // FUN_1005f900
        short statusCode = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9));    // sRam1008a56a
        int filterResult;
        if (statusCode == 0)
        {
            statusCode = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action28));   // sRam1008a590
            if (statusCode == 0)
            {
                statusCode = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action43));   // sRam1008a5ae
                if (statusCode == 0)
                {
                    bool keyAccept = false;
                    if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
                    {
                        byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);   // FUN_100760fc(charCode)
                        keyAccept = keyChar == 13 || keyChar == 3;
                    }
                    if (keyAccept)
                    {
                        // Decompile writes *param_2 = 1 — flips the consumed event's
                        // `what` field to mouseDown (original quirk, kept on the
                        // typed record).
                        evt.WhatType = MacEventType.MouseDown;
                        itemHit = 1;
                        filterResult = 1;
                    }
                    else if (evt.WhatType == MacEventType.UpdateEvt)
                    {
                        MacToolbox.BeginUpdate(dialogHandle);
                        MacToolbox.DrawDialog(dialogHandle);
                        DrawShipyardInfoDialog.Run();   // FUN_1003cad0
                        MacToolbox.EndUpdate(dialogHandle);
                        filterResult = 0;
                    }
                    else
                    {
                        if (evt.WhatType == MacEventType.MouseDown)
                        {
                            int mouseLocal = MacToolbox.GlobalToLocal(evt.WherePacked);   // local_38 = *(evt+10)
                            MacToolbox.GetDialogItem(dialogHandle, 1, itemType, itemHandle, itemRect);
                            if (MacToolbox.PtInRect(mouseLocal, itemRect))   // cVar6 != '\0'
                            {
                                MacToolbox.DrawPicture(ShipyardState.Picts[5], itemRect);   // *(iVar2+0x14) — pressed
                                statusCode = 1;
                                while (MacToolbox.StillDown())
                                {
                                    // GetMouse(&local_38) writes the Point, PtInRect reads it back.
                                    short newDragState = MacToolbox.PtInRect(MacToolbox.GetMouse(), itemRect) ? (short)1 : (short)-1;
                                    bool stateChanged = newDragState != statusCode;
                                    statusCode = newDragState;
                                    if (stateChanged)
                                    {
                                        if (newDragState == 1)
                                        {
                                            MacToolbox.DrawPicture(ShipyardState.Picts[5], itemRect);   // pressed
                                        }
                                        else
                                        {
                                            MacToolbox.DrawPicture(ShipyardState.Picts[4], itemRect);   // *(iVar2+0x10) — normal
                                        }
                                    }
                                }
                                if (statusCode == 1)
                                {
                                    itemHit = 1;
                                    return 1;
                                }
                                itemHit = -1;   // 0xffff
                                return 1;
                            }
                        }
                        filterResult = 0;
                    }
                }
                else
                {
                    itemHit = 6;
                    filterResult = 1;
                }
            }
            else
            {
                itemHit = 2;
                filterResult = 1;
            }
        }
        else
        {
            itemHit = 4;
            filterResult = 1;
        }
        return filterResult;
    }
}
