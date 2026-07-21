using OpenEV.Override.Ports.Graphics;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000a560 (EV Override-11.c lines 5547-5579) — redraw the BAR dialog's
// content: paints into the backdrop GWorld, draws item 7 (the bar description
// TETextBox out of the shared alert/desc C-string buffer) and the 6-button
// row, then blits backdrop -> bar window over the portRect masked by the visRgn.
public static class RedrawBarDialog
{
    public static void Run()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        int window = DialogScratch.SpaceportDialogRecord;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 7, itemType, itemHandle, itemRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.TETextBox(DialogScratch.BarDescText, itemRect, 0);
        MacToolbox.InvertRect(itemRect[0], itemRect[1], itemRect[2], itemRect[3]);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        DrawBarButtonRow.Run(-1);
        Graphics.SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                            MacToolbox.GetDialogPortRect(window), MacToolbox.GetDialogPortRect(window), 0, MacToolbox.GetPortVisRgn(window));
    }
}
