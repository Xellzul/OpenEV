using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004839c (EV Override-11.c lines 30143-30215) — (re)build the active-missions
// list box for the mission-info dialog: fills MissionInfoGlobals.RowToMissionSlot from
// the live MissionStateTable slots, LNews a one-column list in DITL item 2, and sets
// each row to "[prefix]<token-expanded mission name>" (the bullet prefix marks a mission
// whose Failed flag is set, MissionState +3). The row map and selection state drive the
// mission-info dialog.
public static class BuildMissionsListBox
{
    public static void Run()
    {
        int window = MissionInfoGlobals.DialogWindow;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            MissionInfoGlobals.RowToMissionSlot[i] = -1;
        }
        short count = 0;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            if (GameData.MissionStates[i].IsActive != 0)
            {
                MissionInfoGlobals.RowToMissionSlot[count] = i;
                count = (short)(count + 1);
            }
        }

        var itemType = new short[1];
        var itemHandle = new int[1];
        var listRect = new short[4];
        var dataBounds = new short[4];
        MacToolbox.SetPort(window);
        MacToolbox.GetDialogItem(window, 2, itemType, itemHandle, listRect);
        // DLOG 1012 item 2's rect spans the 15px scrollbar strip; carve it off the right
        // edge before LNew (decompile 30180) so the cells end where the scrollbar begins.
        listRect[3] -= 15;
        // SetRect(dataBounds, left=0, top=0, right=1, bottom=count); Mac Rect memory order is {top, left, bottom, right}.
        dataBounds[0] = 0; dataBounds[1] = 0; dataBounds[2] = count; dataBounds[3] = 1;
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        // 9th arg scrollVert=1: ASM does `li r0,1` / `stw r0,0x38(r1)` right before
        // `bl LNew` — the decompile shows only 8 args, dropping the stack-passed
        // 9th (see MacToolbox.LNew's doc comment).
        MissionInfoGlobals.ListHandle =
            MacToolbox.LNew(listRect, dataBounds, 0, 0x80, window, 0, 0, 0, 1);
        MacToolbox.LSetSelFlags(MissionInfoGlobals.ListHandle, 0x80);   // lOnlyOne (single-selection)
        short row = 0;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            if (GameData.MissionStates[i].IsActive != 0)
            {
                // The bullet prefix marks a failed mission (decompile 30193); the exact
                // glyph is an unconfirmed guess from the TOC dump (toc-0x6587).
                string prefix = GameData.MissionStates[i].Failed != 0 ? "• " : "";
                // Round-trip the name through the shared text scratch so
                // SubstituteMissionDescTags can expand its tokens in place.
                TextScratch.Text = TextScratch.Trunc(GameData.Missions[i].MissionName, 250);
                SubstituteMissionDescTags.Run(0, i);
                string line = prefix + TextScratch.Trunc(TextScratch.Text, 250);
                MacToolbox.LSetCell(line, row << 16, MissionInfoGlobals.ListHandle);
                row = (short)(row + 1);
            }
        }
        MacToolbox.LSetSelect(1, 0, MissionInfoGlobals.ListHandle);
        MacToolbox.LSetDrawingMode(1, MissionInfoGlobals.ListHandle);
    }
}
