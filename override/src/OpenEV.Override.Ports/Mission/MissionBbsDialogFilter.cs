using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004cad0 (EV Override-11.c lines 31553-31761) — the mission BBS's modal
// filter ("MissionDialogFilter" was an early transcription half-name). Keymap
// shortcuts fire the map (item 6) / player-info (9) / missions-info (10);
// Return/'l' leave (item 7), 'a' accepts (item 1); Tab cycles the selection
// (wrapping to 0), down-arrow/up-arrow step it (off the ends = -1); list clicks
// re-derive the selection via LClick/LGetSelect, the scrollbar (item 3) LClicks,
// other clicks track the 2-button row; update events redraw. Registered under
// MissionBoardGlobals.BbsFilterProc.
//
// Ground truth: this FUN has no dedicated reference/disasm split file — its ASM
// body (loc_4CAD0, DATA XREF off_82570 = the BbsFilterProc registration) is its
// own separate block immediately following sub_4C908's in EV_Override.asm (from
// ~line 95319), not inside sub_4C908's body.
public static class MissionBbsDialogFilter
{
    private static short Avail(int row)
    {
        // DEVIATION (faithful): the ASM's Tab branch (decompile 31607) has no
        // 0x200 guard and can read past this mode's half of the grid (or past
        // the BSS) once the selection sits on the last row. The managed array
        // can't alias adjacent memory; an out-of-range row reads as -1 (= wrap),
        // matching the original's eventual outcome for every reachable case.
        if (row < 0 || row >= MissionAvailGrid.Count) return -1;
        return MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][row];
    }

    private static void RebuildSelectedMissionText()
    {
        TextScratch.Text = "";
        if (SpaceportGlobals.BbsSelectedRow != -1)
        {
            TextScratch.Text = LoadDescriptionText.Load((short)(Avail(SpaceportGlobals.BbsSelectedRow) + 4000));
            SubstituteMissionDescTags.Run(1, Avail(SpaceportGlobals.BbsSelectedRow));
        }
    }

    // MacToolbox.InvalRect marks the board window's redraw-pending flag
    // (NoteWindowInvalidated); RunModalLoop's poll loop drains it the very next
    // iteration by dispatching an UpdateEvt back through this same filter (the
    // WhatType==UpdateEvt branch below), which repaints items 4/5 via
    // RedrawMissionBbsDialog — the real Mac update-event mechanism. Do NOT add
    // a manual RedrawMissionBbsDialog.Run() call here: the pending-flag path
    // already redraws next iteration, so a manual call here double-redraws.
    private static void InvalSelectionItems()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];
        MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 4, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
        MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 5, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
    }

    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        Keymap.RefreshCachedKeymap();
        // (int) selects the raw-int TestCachedKeymapBit overload (matches the
        // decompile passing the raw stored slot value): Slot() already returns a
        // value in cached-KeyMap space, so the MacKeycode overload's extra XOR-8
        // transform must NOT run here — don't simplify this cast away.
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9)) != 0)
        {
            itemHit = 6;
            return 1;
        }
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action28)) != 0)
        {
            itemHit = 9;
            return 1;
        }
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action43)) != 0)
        {
            itemHit = 10;
            return 1;
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)LookupKeyTableUnshifted.Run((uint)(sbyte)evt.Message);
            if (keyChar == '\r' || keyChar == '\x03')
            {
                itemHit = 7;
                return 1;
            }
            if (keyChar == 'l')
            {
                itemHit = 7;
                return 1;
            }
            if (keyChar == 'a')
            {
                itemHit = 1;
                return 1;
            }
            if (keyChar == '\t')
            {
                short prevRow = SpaceportGlobals.BbsSelectedRow;
                if (prevRow != -1)
                {
                    MacToolbox.LSetSelect(0, prevRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                SpaceportGlobals.BbsSelectedRow += 1;
                if (Avail(SpaceportGlobals.BbsSelectedRow) == -1)
                {
                    SpaceportGlobals.BbsSelectedRow = 0;
                }
                RebuildSelectedMissionText();
                if (SpaceportGlobals.BbsSelectedRow != -1)
                {
                    MacToolbox.LSetSelect(1, SpaceportGlobals.BbsSelectedRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                if (SpaceportGlobals.BbsSelectedRow != prevRow)
                {
                    InvalSelectionItems();
                }
                // decompile 31624-31625 / ASM loc_4CD64 — Tab-only: the original calls
                // LSetSelect(1, ...) unconditionally a second time here, exactly
                // duplicating the guarded call a few lines up (which always already
                // fired, since Tab's wrap-to-0 fallback means the row is never -1 at
                // this point). Preserved bug-for-bug, not a copy-paste accident.
                MacToolbox.LSetSelect(1, SpaceportGlobals.BbsSelectedRow << 16, MissionBoardGlobals.BbsListHandle);
            }
            // decompile 31628/ASM `cmpwi r0, 0x200` — the same per-mode grid capacity Avail() bounds against.
            if (keyChar == '\x1f' && SpaceportGlobals.BbsSelectedRow + 1 < MissionAvailGrid.Count &&
                Avail(SpaceportGlobals.BbsSelectedRow + 1) != -1)
            {
                short prevRow = SpaceportGlobals.BbsSelectedRow;
                if (prevRow != -1)
                {
                    MacToolbox.LSetSelect(0, prevRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                SpaceportGlobals.BbsSelectedRow += 1;
                if (Avail(SpaceportGlobals.BbsSelectedRow) == -1)
                {
                    SpaceportGlobals.BbsSelectedRow = -1;
                }
                RebuildSelectedMissionText();
                if (SpaceportGlobals.BbsSelectedRow != -1)
                {
                    MacToolbox.LSetSelect(1, SpaceportGlobals.BbsSelectedRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                if (SpaceportGlobals.BbsSelectedRow != prevRow)
                {
                    InvalSelectionItems();
                }
            }
            if (keyChar == '\x1e' && 0 < SpaceportGlobals.BbsSelectedRow)
            {
                short prevRow = SpaceportGlobals.BbsSelectedRow;
                if (prevRow != -1)
                {
                    MacToolbox.LSetSelect(0, prevRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                SpaceportGlobals.BbsSelectedRow -= 1;
                if (SpaceportGlobals.BbsSelectedRow < 0)
                {
                    SpaceportGlobals.BbsSelectedRow = -1;
                }
                else if (Avail(SpaceportGlobals.BbsSelectedRow) == -1)
                {
                    SpaceportGlobals.BbsSelectedRow = -1;
                }
                RebuildSelectedMissionText();
                if (SpaceportGlobals.BbsSelectedRow != -1)
                {
                    MacToolbox.LSetSelect(1, SpaceportGlobals.BbsSelectedRow << 16, MissionBoardGlobals.BbsListHandle);
                }
                if (SpaceportGlobals.BbsSelectedRow != prevRow)
                {
                    InvalSelectionItems();
                }
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            var itemType = new short[1];
            var itemHandle = new int[1];
            var itemRect = new short[4];
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 2, itemType, itemHandle, itemRect);
            if (MacToolbox.PtInRect(mousePoint, itemRect))
            {
                MacToolbox.LClick(mousePoint, evt.Modifiers, MissionBoardGlobals.BbsListHandle);
                short prevRow = SpaceportGlobals.BbsSelectedRow;
                SpaceportGlobals.BbsSelectedRow = -1;
                int cell = 0;
                while ((short)(cell >> 16) < MacToolbox.LGetRowCount(MissionBoardGlobals.BbsListHandle))
                {
                    if (MacToolbox.LGetSelect(0, ref cell, MissionBoardGlobals.BbsListHandle))
                    {
                        SpaceportGlobals.BbsSelectedRow = (short)(cell >> 16);
                        break;
                    }
                    cell = (((cell >> 16) + 1) & 0xffff) << 16 | (cell & 0xffff);
                }
                RebuildSelectedMissionText();
                if (SpaceportGlobals.BbsSelectedRow != prevRow)
                {
                    InvalSelectionItems();
                }
            }
            MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 3, itemType, itemHandle, itemRect);
            if (MacToolbox.PtInRect(mousePoint, itemRect))
            {
                MacToolbox.LClick(mousePoint, evt.Modifiers, MissionBoardGlobals.BbsListHandle);
            }
            short hit = (short)TrackBbsButtonHit.Run(mousePoint);
            switch (hit)
            {
                case 0: itemHit = 1; break;
                case 1: itemHit = 7; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.SetPort(MissionBoardGlobals.DialogWindow);
            MacToolbox.BeginUpdate(MissionBoardGlobals.DialogWindow);
            RedrawMissionBbsDialog.Run();
            MacToolbox.EndUpdate(MissionBoardGlobals.DialogWindow);
        }
        return 0;
    }
}
