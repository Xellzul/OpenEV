using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Mission;

// FUN_100515d8 (EV Override-11.c lines 33366-33407) — redraw the single-mission
// OFFER dialog (DLOG 0x3f8, shares the mission-board window with the BBS):
// paints into the backdrop GWorld, frames the window, draws the mission
// description text (item 3, the shared text scratch buffer) inverted, then the
// button row, and blits backdrop -> window through the visRgn.
public static class RedrawSingleMissionDialog
{
    public static void Run()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        int window = Dialog.Model.MissionBoardGlobals.DialogWindow;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 3, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(window)))
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.TETextBox(Core.Model.TextScratch.Text, itemRect, 0);
            MacToolbox.InvertRect(itemRect);
        }
        RenderSingleMissionButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        var portRect = MacToolbox.GetDialogPortRect(window);
        // CopyBits src/dst are pixmap keys: GWorld/window handle + 2 (see
        // RenderGlobals.BackdropGWorld).
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                            portRect, portRect, 0, MacToolbox.GetDialogVisRgn(window));
    }
}
