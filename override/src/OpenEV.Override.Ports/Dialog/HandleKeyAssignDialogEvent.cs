using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10044d68 — the Set Prefs modal dialog filter proc (registered under
// PrefsDialogState.KeyAssignFilterProc). EV Override-11.c lines 28610-28662.
//
// On a key event (what 3/0/6) with a slot armed it reads the held key
// (PollFirstHeldUserKey → Mac keycode via GetKeys), stores it into the LIVE
// keymap at the armed slot, advances to the next slot (mod 31), and waits
// for the key to be released (FUN_1005f964). On an update event (what 6) it
// redraws the keybind grid (PrefsDialogDraw) + the standard items.
public static class HandleKeyAssignDialogEvent
{
    public static int Run(int dialogPtr, MacEvent evt)
    {
        if (evt.WhatType == MacEventType.MouseDown)
        {
            MacToolbox.SetPort(PrefsDialogState.DialogWindow);
            // DEAD (original): the decompile local-izes the click point but never reads the
            // result afterward (the only other use of that computation, `ppuVar3 = local_6c`,
            // feeds a branch that's mutually exclusive with MouseDown) — SetPort above is this
            // branch's only observable effect. Preserved bug-for-bug.
            var mouseLocal = new int[2];
            mouseLocal[0] = evt.WherePacked;
            MacToolbox.GlobalToLocal(mouseLocal);
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.NullEvent or MacEventType.UpdateEvt)
        {
            int heldKeyCode = (int)Keymap.PollFirstHeldUserKey();
            if ((short)heldKeyCode != -1 && PrefsDialogState.SelectedKeybindSlot != -1)
            {
                // Store the captured keycode at the armed slot, then advance (slot+1) mod 31.
                // The decompile captures prevSlot BEFORE advancing and invalidates BOTH the old
                // item (prevSlot+3) and the new (SelectedKeybindSlot+3).
                Keymap.LiveSet(PrefsDialogState.SelectedKeybindSlot, (short)heldKeyCode);
                short prevSlot = PrefsDialogState.SelectedKeybindSlot;
                PrefsDialogState.SelectedKeybindSlot = (short)((prevSlot + 1) % Keymap.LiveCount);
                var itemRect = new short[4];
                MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, prevSlot + 3, 0, 0, itemRect);
                MacToolbox.InvalRect(itemRect);
                MacToolbox.GetDialogItem(PrefsDialogState.DialogWindow, PrefsDialogState.SelectedKeybindSlot + 3, 0, 0, itemRect);
                MacToolbox.InvalRect(itemRect);
                // heldKeyCode is already EVO-keycode-space (PollFirstHeldUserKey) — keep it on the
                // `int` overload; casting to MacKeycode would double-XOR it and this loop would
                // read "released" on its first spin even though the key is still physically held.
                short keyReleased;
                do
                {
                    keyReleased = (short)Keymap.TestLiveKeymapBit(heldKeyCode);
                } while (keyReleased != 0);
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.SetPort(PrefsDialogState.DialogWindow);
            MacToolbox.BeginUpdate(PrefsDialogState.DialogWindow);
            PrefsDialogDraw.Run();
            MacToolbox.DrawDialog(PrefsDialogState.DialogWindow);
            MacToolbox.EndUpdate(PrefsDialogState.DialogWindow);
        }
        return 0;
    }
}
