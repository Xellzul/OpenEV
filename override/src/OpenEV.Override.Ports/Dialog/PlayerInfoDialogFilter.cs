using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1003f044 (EV Override-11.c lines 25937-26020) — the player-info dialog's
// modal filter. Tab/Shift-Tab cycles WorldState.PlayerInfoPage 1..3; Cmd-./
// Return leave (Return is disabled on page 4 — the capture dialog reuses this
// filter); clicks track the 4-tab row; update events redraw the page.
// Registered under Dialog.Model.PlayerInfoGlobals.FilterProc.
//
// charCode is the raw message low byte (decompile 25950 `(char)*(param_2+1)` —
// no keymap translation); modifiers come from evt.Modifiers (byte +14).
public static class PlayerInfoDialogFilter
{
    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte charCode = (byte)evt.Message;
            if (charCode == 9)
            {
                // Tab / Shift-Tab cycles the page 1..3.
                if ((evt.Modifiers & 0x200U) == 0)
                {
                    WorldState.PlayerInfoPage += 1;
                }
                else
                {
                    WorldState.PlayerInfoPage -= 1;
                }
                if (3 < WorldState.PlayerInfoPage)
                {
                    WorldState.PlayerInfoPage = 1;
                }
                if (WorldState.PlayerInfoPage < 1)
                {
                    WorldState.PlayerInfoPage = 3;
                }
                MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(PlayerInfoGlobals.DialogWindow));
                return 1;
            }
            if (charCode == 3)
            {
                itemHit = 1;
                return 1;
            }
            // (page 4 = the capture dialog's reuse of this filter — Return disabled.)
            if ((WorldState.PlayerInfoPage != 4) && (charCode == 13))
            {
                itemHit = 1;
                return 1;
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            short hitZone = (short)TrackPlayerInfoTabMouseDown.Run((int)WorldState.PlayerInfoPage, mousePoint);
            switch (hitZone)
            {
                case 0: itemHit = 1; break;
                case 1: itemHit = 2; break;
                case 2: itemHit = 3; break;
                case 3: itemHit = 4; break;
                case 4: itemHit = 5; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.BeginUpdate(PlayerInfoGlobals.DialogWindow);
            RenderPlayerInfoDialog.Run();
            MacToolbox.EndUpdate(PlayerInfoGlobals.DialogWindow);
            return 0;
        }
        return 0;
    }
}
