using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004d31c (EV Override-11.c lines 31768-31887) — redraw the mission BBS
// dialog ("UpdateKeyRebindDialog" was an early transcription misname): paints into the
// backdrop GWorld, frames the list+scrollbar (items 2∪3), outlines the default
// button (item 1, round rect), draws the "The following missions are available
// here:" header (item 8), the selected mission's name in Times 18 (item 5,
// token-expanded) and description (item 4), the current date bottom-left, the
// 2-button row, then blits backdrop -> window and LUpdates the list.
public static class RedrawMissionBbsDialog
{
    public static void Run()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];
        var scrollRect = new short[4];

        int window = Dialog.Model.MissionBoardGlobals.DialogWindow;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.Frame);
        MacToolbox.GetDialogItem(window, 2, itemType, itemHandle, itemRect);
        MacToolbox.GetDialogItem(window, 3, itemType, itemHandle, scrollRect);
        MacToolbox.UnionRect(itemRect, scrollRect, itemRect);
        MacToolbox.InsetRect(itemRect, -2, -2);
        MacToolbox.FrameRect(itemRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 1, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            MacToolbox.PenSize(3, 3);
            MacToolbox.InsetRect(itemRect, -4, -4);
            MacToolbox.FrameRoundRect(itemRect[0], itemRect[1], itemRect[2], itemRect[3], 16, 16);   // oval corner width/height (px)
            MacToolbox.PenSize(1, 1);
        }
        MacToolbox.GetDialogItem(window, 8, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.Unexplored);
            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
            MacToolbox.DrawString("The following missions are available here:");   // Pascal at toc-0x3f49
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        short selRow = Dialog.Model.SpaceportGlobals.BbsSelectedRow;
        short selPers = (selRow >= 0 && selRow < 512)
            ? Core.Model.MissionAvailGrid.ByMode[Dialog.Model.SpaceportGlobals.InBarFlag][selRow] : (short)-1;
        MacToolbox.GetDialogItem(window, 5, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            if (selRow == -1)
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(itemRect);
            }
            else
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.TextFont(20);   // Times
                MacToolbox.TextSize(18);
                // The name expansion round-trips through the shared text scratch
                // buffer — the original SAVES the buffer and RESTORES it after, so the
                // description text (item 4) survives.
                string savedBuf = Core.Model.TextScratch.Trunc(Core.Model.TextScratch.Text, 250);
                // DEVIATION (faithful): the ASM reads Names[selPers] unconditionally here
                // (even in the compacted-grid edge case where selRow is still valid but its
                // ByMode entry is -1 — e.g. right after the last available mission is
                // accepted), landing 256 bytes BEFORE the Names table and rendering whatever
                // garbage precedes it. The managed Names[] can't reproduce that out-of-bounds
                // read, so a -1 grid entry substitutes an empty name instead of garbage.
                // SubstituteMissionDescTags.Run(1, -1) is unaffected — it already no-ops its
                // param_2-indexed lookup for -1 and needs no guard here.
                Core.Model.TextScratch.Text = Core.Model.TextScratch.Trunc(
                    (selPers == -1 ? "" : Dialog.Model.MissionBoardGlobals.Names[selPers]) ?? "", 250);
                SubstituteMissionDescTags.Run(1, selPers);
                string name = Core.Model.TextScratch.Trunc(Core.Model.TextScratch.Text, 250);
                Core.Model.TextScratch.Text = savedBuf;
                MacToolbox.TETextBox(name, itemRect, 0);
                MacToolbox.InvertRect(itemRect);
            }
        }
        MacToolbox.GetDialogItem(window, 4, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            if (selRow == -1)
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(itemRect);
            }
            else
            {
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.TextFont(3);
                MacToolbox.TextSize(9);
                MacToolbox.TETextBox(Core.Model.TextScratch.Text, itemRect, 0);
                MacToolbox.InvertRect(itemRect);
            }
        }
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.Frame);
        var date = Core.Model.GameDate.Current;
        // Decompile calls FUN_1005db98 = FormatDateLong (abbreviated "Jan."-style
        // months) — NOT FormatDateLongFull (FUN_1005de74).
        string dateText = Text.FormatDateLong.Run(date.Year, date.Month, date.Day);
        var portRect = MacToolbox.GetDialogPortRect(window);   // {top,left,bottom,right}
        MacToolbox.MoveTo(portRect[1] + 5, portRect[2] - 6);
        MacToolbox.DrawString(dateText);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        Dialog.DrawBbsButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // CopyBits src/dst are pixmap keys: GWorld/window handle + 2 (see
        // RenderGlobals.BackdropGWorld).
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                            MacToolbox.GetDialogPortRect(window), MacToolbox.GetDialogPortRect(window), 0, MacToolbox.GetPortVisRgn(window));
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        if (Dialog.Model.MissionBoardGlobals.BbsListHandle != 0)
        {
            MacToolbox.LUpdate(MacToolbox.GetPortVisRgn(window), Dialog.Model.MissionBoardGlobals.BbsListHandle);
        }
    }
}
