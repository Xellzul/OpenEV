using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100513e4 (EV Override-11.c lines 33292-33365) — the single-mission OFFER
// dialog's modal filter. Keymap actions fire the map (item 4) / player-info (5) /
// missions-info (7); Return/Enter/'y'/'o' accept (item 1), 'n' refuses (item 2,
// only when the accept/refuse layout is up); mouse-downs track the button row
// (TrackSingleMissionButtonMouseDown) onto items 1/2; update events redraw via
// RedrawSingleMissionDialog. Registered under MissionBoardGlobals.OfferFilterProc.
public static class ConfirmDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        int handled;

        Keymap.RefreshCachedKeymap();
        short keyHit = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9));
        if (keyHit == 0)
        {
            keyHit = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action28));
            if (keyHit == 0)
            {
                keyHit = (short)Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action43));
                if (keyHit == 0)
                {
                    if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
                    {
                        int keyChar = (int)(Misc.LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message));
                        if (keyChar == '\r' || keyChar == '\x03' || keyChar == 'y' || keyChar == 'o')
                        {
                            itemHit = 1;
                            return 1;
                        }
                        // Refuse only exists in the accept/refuse layout.
                        if (keyChar == 'n' && MissionBoardGlobals.OfferAcceptRefuseLayout != 0)
                        {
                            itemHit = 2;
                            return 1;
                        }
                    }
                    if (evt.WhatType == MacEventType.MouseDown)
                    {
                        int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
                        short hitButton = (short)Mission.TrackSingleMissionButtonMouseDown.Run(mousePoint);
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
                            MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
                            MacToolbox.BeginUpdate(MissionBoardGlobals.DialogWindow);
                            Mission.RedrawSingleMissionDialog.Run();
                            MacToolbox.EndUpdate(MissionBoardGlobals.DialogWindow);
                        }
                        handled = 0;
                    }
                }
                else
                {
                    itemHit = 7;
                    handled = 1;
                }
            }
            else
            {
                itemHit = 5;
                handled = 1;
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
