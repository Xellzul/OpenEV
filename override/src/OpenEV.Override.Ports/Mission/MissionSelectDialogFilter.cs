using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Misc.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Mission;

// FUN_10050230 (EV Override-11.c lines 32799-32969) — the active-missions info
// dialog's modal filter. The map key fires item 6 (show destination); Tab /
// down-arrow (0x1f) / up-arrow (0x1e) cycle the selected mission row (skipping
// empty rows) and rebuild the description; clicks in the list box re-derive the
// selection via LClick/LGetSelect, clicks elsewhere track the 2-button row;
// update events redraw. Registered under MissionInfoGlobals.FilterProc.
public static class MissionSelectDialogFilter
{
    // Rebuild the selected row's description into the shared text scratch buffer.
    private static void RebuildSelectedRowText()
    {
        TextScratch.Text = "";   // strncpy-clears from toc-0x6588; decompile's "ppuVar6 - 0x1962" is int*-scaled (x4) = toc-0x6588, not a byte offset
        if (MissionInfoGlobals.SelectedRow != -1)
        {
            short slot = MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow];
            TextScratch.Text = LoadDescriptionText.Load(GameData.Missions[slot].MissionInfoText);
            SubstituteMissionDescTags.Run(0, slot);
        }
    }

    // Items 4 (description TE box) and 5 (the Abort button, whose enabled state
    // depends on which row is selected — see TrackMissionInfoButtonHit) both need
    // invalidating on a selection change.
    private static void InvalSelectionDependentItems()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];
        MacToolbox.GetDialogItem(MissionInfoGlobals.DialogWindow, 4, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
        MacToolbox.GetDialogItem(MissionInfoGlobals.DialogWindow, 5, itemType, itemHandle, itemRect);
        MacToolbox.InvalRect(itemRect);
    }

    // DEVIATION (faithful): InvalRect is a no-op in this port's immediate-mode
    // renderer (the original relied on the OS turning it into an updateEvt), so a
    // selection change needs an explicit redraw to actually refresh the list
    // hilite / description panel.
    private static void InvalAndRedrawAfterSelectionChange()
    {
        InvalSelectionDependentItems();
        RedrawMissionSelectDialog.Run();
    }

    private static short ActiveMissionCount()
    {
        short count = 0;
        for (short j = 0; j < MissionStateTable.Count; j = (short)(j + 1))
        {
            if (GameData.MissionStates[j].IsActive != 0)
            {
                count = (short)(count + 1);
            }
        }
        return count;
    }

    public static int Run(int dialog, MacEvent evt, ref short itemHit)
    {
        Keymap.RefreshCachedKeymap();
        if (Keymap.TestCachedKeymapBit((int)Keymap.Slot(KeyAction.Action9)) != 0)
        {
            itemHit = 6;
            return 1;
        }
        if (evt.WhatType is MacEventType.KeyDown or MacEventType.AutoKey)
        {
            byte keyChar = (byte)evt.Message;   // low byte of message@+2 — direct charCode, no LookupKeyTableUnshifted (decompile 32828)
            if (keyChar == '\r' || keyChar == '\x03')
            {
                itemHit = 1;
                return 1;
            }
            if (keyChar == '\t' || keyChar == '\x1f')
            {
                short prevRow = MissionInfoGlobals.SelectedRow;
                if (prevRow != -1)
                {
                    MacToolbox.LSetSelect(0, prevRow << 16, MissionInfoGlobals.ListHandle);
                }
                if (0 < ActiveMissionCount())
                {
                    do
                    {
                        MissionInfoGlobals.SelectedRow += 1;
                        if (MissionStateTable.Count - 1 < MissionInfoGlobals.SelectedRow)
                        {
                            MissionInfoGlobals.SelectedRow = 0;
                        }
                    } while (MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow] == -1);
                }
                RebuildSelectedRowText();
                if (MissionInfoGlobals.SelectedRow != -1)
                {
                    MacToolbox.LSetSelect(1, MissionInfoGlobals.SelectedRow << 16, MissionInfoGlobals.ListHandle);
                }
                if (MissionInfoGlobals.SelectedRow != prevRow)
                {
                    InvalAndRedrawAfterSelectionChange();
                }
            }
            if (keyChar == '\x1e')
            {
                short prevRow = MissionInfoGlobals.SelectedRow;
                if (prevRow != -1)
                {
                    MacToolbox.LSetSelect(0, prevRow << 16, MissionInfoGlobals.ListHandle);
                }
                if (0 < ActiveMissionCount())
                {
                    do
                    {
                        MissionInfoGlobals.SelectedRow -= 1;
                        if (MissionInfoGlobals.SelectedRow < 0)
                        {
                            MissionInfoGlobals.SelectedRow = MissionStateTable.Count - 1;
                        }
                    } while (MissionInfoGlobals.RowToMissionSlot[MissionInfoGlobals.SelectedRow] == -1);
                }
                RebuildSelectedRowText();
                if (MissionInfoGlobals.SelectedRow != -1)
                {
                    MacToolbox.LSetSelect(1, MissionInfoGlobals.SelectedRow << 16, MissionInfoGlobals.ListHandle);
                }
                if (MissionInfoGlobals.SelectedRow != prevRow)
                {
                    InvalAndRedrawAfterSelectionChange();
                }
            }
        }
        if (evt.WhatType == MacEventType.MouseDown)
        {
            var itemType = new short[1];
            var itemHandle = new int[1];
            var listRect = new short[4];
            int mousePoint = MacToolbox.GlobalToLocal(evt.WherePacked);
            MacToolbox.GetDialogItem(MissionInfoGlobals.DialogWindow, 2, itemType, itemHandle, listRect);
            if (MacToolbox.PtInRect(mousePoint, listRect))
            {
                MacToolbox.LClick(mousePoint, evt.Modifiers, MissionInfoGlobals.ListHandle);
                short prevRow = MissionInfoGlobals.SelectedRow;
                MissionInfoGlobals.SelectedRow = -1;
                int cell = 0;
                while ((short)(cell >> 16) < MacToolbox.LGetRowCount(MissionInfoGlobals.ListHandle))
                {
                    if (MacToolbox.LGetSelect(0, ref cell, MissionInfoGlobals.ListHandle))
                    {
                        MissionInfoGlobals.SelectedRow = (short)(cell >> 16);
                        break;
                    }
                    cell = (((cell >> 16) + 1) & 0xffff) << 16 | (cell & 0xffff);
                }
                RebuildSelectedRowText();
                if (MissionInfoGlobals.SelectedRow != prevRow)
                {
                    InvalAndRedrawAfterSelectionChange();
                }
            }
            short hit = (short)TrackMissionInfoButtonHit.Run(mousePoint);
            switch (hit)
            {
                case 0: itemHit = 1; break;
                case 1: itemHit = 5; break;
                default: itemHit = -1; break;
            }
            return 1;
        }
        if (evt.WhatType == MacEventType.UpdateEvt)
        {
            MacToolbox.SetPort(MissionInfoGlobals.DialogWindow);
            MacToolbox.BeginUpdate(MissionInfoGlobals.DialogWindow);
            RedrawMissionSelectDialog.Run();
            MacToolbox.EndUpdate(MissionInfoGlobals.DialogWindow);
        }
        return 0;
    }
}
