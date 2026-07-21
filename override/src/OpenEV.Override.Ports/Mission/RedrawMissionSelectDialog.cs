using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Mission;

// FUN_10050834 (EV Override-11.c lines 32975-33049) — redraw the active-missions
// info dialog: paints into the backdrop GWorld, frames the list box (item 2),
// draws the "Currently active missions:" header with the right-aligned current
// date (item 3 row), TETextBoxes the selected mission's description (item 4,
// painted out when nothing is selected), draws the 2-button row, then blits
// backdrop -> window and LUpdates the list.
public static class RedrawMissionSelectDialog
{
    public static void Run()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        int window = Dialog.Model.MissionInfoGlobals.DialogWindow;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 2, itemType, itemHandle, itemRect);
        MacToolbox.PaintRect(itemRect);
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.Frame);
        MacToolbox.GetDialogItem(window, 2, itemType, itemHandle, itemRect);
        MacToolbox.InsetRect(itemRect, -2, -2);
        MacToolbox.FrameRect(itemRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 3, itemType, itemHandle, itemRect);
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.MoveTo(itemRect[1], itemRect[2]);                  // (rect.left, rect.bottom)
        MacToolbox.DrawString("Currently active missions:");          // Pascal at toc-0x3eb3
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.Frame);
        var date = Core.Model.GameDate.Current;
        // Decompile calls FUN_1005db98 = FormatDateLong (abbreviated "Jan."-style
        // months) — NOT FormatDateLongFull (FUN_1005de74).
        string dateText = Text.FormatDateLong.Run(date.Year, date.Month, date.Day);
        int dateWidth = MacToolbox.StringWidth(dateText);
        MacToolbox.MoveTo(MacToolbox.GetDialogPortRect(window)[3] - dateWidth - 5, itemRect[2]);
        MacToolbox.DrawString(dateText);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 4, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            if (Dialog.Model.MissionInfoGlobals.SelectedRow == -1)
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
        Dialog.DrawMissionInfoButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        // CopyBits src/dst are pixmap keys: GWorld/window handle + 2 (see
        // RenderGlobals.BackdropGWorld).
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                            MacToolbox.GetDialogPortRect(window), MacToolbox.GetDialogPortRect(window), 0, MacToolbox.GetPortVisRgn(window));
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        if (Dialog.Model.MissionInfoGlobals.ListHandle != 0)
        {
            MacToolbox.LUpdate(MacToolbox.GetPortVisRgn(window), Dialog.Model.MissionInfoGlobals.ListHandle);
        }
    }
}
