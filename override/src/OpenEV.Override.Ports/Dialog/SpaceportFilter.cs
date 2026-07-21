using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Systems.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100377d8 (EV Override-11.c lines 22731-22866) — the spaceport hub's
// ModalDialog filter. Per event: re-derives the mission-BBS tab enable from
// the spob service flags, runs the ambient-sound countdown, then translates
// keymap actions / keyboard shortcuts / tab-bar clicks into item hits
// (itemHit out + return 1 = consumed). Update events redraw the dialog.
//
// Port dispatch: registered via MacToolbox.RegisterModalFilter under
// SpaceportGlobals.FilterProc. ModalDialog forwards mouseDown (what==1) and
// keyDown/autoKey (what==3/5) to the filter before its own typed-char →
// default-item fallback — this dialog never calls SetDialogDefaultItem, so
// the fallback alone could never fire the right item; the mouse/key handling
// below is what's actually live.
public static class SpaceportFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if ((CurrentSpob.Rec.Flags & (int)SpobFlags.Uninhabited) == 0)
        {
            SpaceportGlobals.MissionBbsEnabled = 1;
        }
        else
        {
            SpaceportGlobals.MissionBbsEnabled = 0;
        }
        if (SpaceportGlobals.AmbientSndHandle != 0)
        {
            SpaceportGlobals.AmbientTimer -= 1;
            if (SpaceportGlobals.AmbientTimer < 1)
            {
                SndPlay.Run(SpaceportGlobals.AmbientSndHandle, 10, 128, 128);
                SpaceportGlobals.AmbientTimer = (short)(SeedEvoRng.Run(512) + 512);
            }
        }
        Keymap.RefreshCachedKeymap();
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9)) != 0)
        {
            itemHit = 3;
            return 1;
        }
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action28)) != 0)
        {
            itemHit = 2;
            return 1;
        }
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action43)) != 0)
        {
            itemHit = 14;
            return 1;
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if ((keyChar == '\r') || (keyChar == '\x03'))
            {
                evt.WhatType = MacEventType.MouseDown;   // decompile: *param_2 = 1 — faithful; inert here since we return immediately
                itemHit = 12;
                return 1;
            }
            if ((keyChar == 'r') || (keyChar == 'f'))
            {
                itemHit = 4;
                return 1;
            }
            if (((keyChar == 'c') || (keyChar == 'e')) || (keyChar == 't'))
            {
                itemHit = 7;
                return 1;
            }
            if (keyChar == 'o')
            {
                itemHit = 8;
                return 1;
            }
            if (keyChar == 's')
            {
                itemHit = 9;
                return 1;
            }
            if (keyChar == 'n')
            {
                itemHit = 10;
                return 1;
            }
            if (keyChar == 'b')
            {
                itemHit = 11;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            var itemType = new short[1];     // GetDialogItem itemType out (never read back)
            var itemHandle = new int[1];     // GetDialogItem handle out (never read back)
            var itemRect = new short[4];     // item-5 (spob picture) Rect
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short tabHit = (short)TrackDialogButtonHit.Run(mousePoint);
            MacToolbox.GetDialogItem(SpaceportGlobals.DialogWindow, 5, itemType, itemHandle, itemRect);
            if (MacToolbox.PtInRect(mousePoint, itemRect))
            {
                itemHit = 5;
                return 1;
            }
            switch (tabHit)
            {
                case 0: itemHit = 12; break;
                case 1: itemHit = 4; break;
                case 2: itemHit = 7; break;
                case 3: itemHit = 8; break;
                case 4: itemHit = 9; break;
                case 5: itemHit = 10; break;
                case 6: itemHit = 11; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.BeginUpdate(SpaceportGlobals.DialogWindow);
            RedrawSpaceportDialog.Run();
            MacToolbox.EndUpdate(SpaceportGlobals.DialogWindow);
            return 0;
        }
        return 0;
    }
}
