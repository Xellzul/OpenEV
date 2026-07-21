using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Mission;

// FUN_10048110 (EV Override-11.c 30073-30137) — (re)build the mission BBS list
// box: reset the board selection, count the current mode's available missions,
// LNew a one-column list in DITL item 2, and fill each row with the mission
// name (token-expanded through the shared text scratch).
public static class BuildMissionBbsList
{
    public static void Run()
    {
        int window = MissionBoardGlobals.DialogWindow;
        SpaceportGlobals.BbsSelectedRow = 0;

        short count = 0;
        for (short i = 0; i < MissionAvailGrid.Count; i = (short)(i + 1))
        {
            if (MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][i] != -1)
            {
                count = (short)(count + 1);
            }
        }

        var itemType = new short[1];
        var itemHandle = new int[1];
        var listRect = new short[4];
        var dataBounds = new short[4];
        MacToolbox.SetPort(window);
        MacToolbox.GetDialogItem(window, 2, itemType, itemHandle, listRect);
        // SetRect(dataBounds, left 0, top 0, right 1, bottom count) — Rect memory is {top,left,bottom,right}.
        dataBounds[0] = 0; dataBounds[1] = 0; dataBounds[2] = count; dataBounds[3] = 1;
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        // 9th arg scrollVert=1: ASM does `li r0,1` / `stw r0,0x38(r1)` right before
        // `bl LNew` — the decompile shows only 8 args, dropping the stack-passed
        // 9th (see MacToolbox.LNew's doc comment).
        MissionBoardGlobals.BbsListHandle = MacToolbox.LNew(listRect, dataBounds, 0, 0x80, window, 0, 0, 0, 1);
        MacToolbox.LSetSelFlags(MissionBoardGlobals.BbsListHandle, 0x80);   // lOnlyOne
        MacToolbox.LSetDrawingMode(0, MissionBoardGlobals.BbsListHandle);

        for (short row = 0; row < count; row = (short)(row + 1))
        {
            short pers = MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][row];
            string line;
            if (pers == -1)
            {
                line = "";
            }
            else
            {
                // The mission name flows through the shared text scratch so
                // SubstituteMissionDescTags can expand its tokens in place.
                TextScratch.Text = TextScratch.Trunc(MissionBoardGlobals.Names[pers] ?? "", 250);
                SubstituteMissionDescTags.Run(1, pers);
                line = TextScratch.Trunc(TextScratch.Text, 250);
            }
            MacToolbox.LSetCell(line, row << 16, MissionBoardGlobals.BbsListHandle);
        }
        MacToolbox.LSetSelect(1, 0, MissionBoardGlobals.BbsListHandle);
        MacToolbox.LSetDrawingMode(1, MissionBoardGlobals.BbsListHandle);
    }
}
