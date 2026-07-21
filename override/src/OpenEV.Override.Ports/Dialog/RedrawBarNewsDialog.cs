using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000a898 (EV Override-11.c lines 5632-5663) — redraw the bar news-terminal
// dialog: paints into the backdrop GWorld, draws the PICT 9000 art over the
// portRect, TETextBoxes the two news lines into window-relative rects, then
// blits backdrop -> window (no mask rgn).
public static class RedrawBarNewsDialog
{
    public static void Run()
    {
        int window = DialogScratch.BarNewsDialogWindow;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.DrawPicture(DialogScratch.BarNewsPictHandle, MacToolbox.GetDialogPortRect(window));
        var winRect = MacToolbox.GetDialogPortRect(window);
        short winTop = winRect[0];
        short winLeft = winRect[1];
        short winBottom = winRect[2];
        short winRight = winRect[3];
        var lineRect = new short[4]
        {
            (short)(winTop + 140), (short)(winLeft + 15),
            (short)(winTop + 180), (short)(winRight - 15),
        };
        MacToolbox.TETextBox(SpaceportGlobals.BarNewsLineA, lineRect, 0);
        MacToolbox.InvertRect(lineRect[0], lineRect[1], lineRect[2], lineRect[3]);
        lineRect[0] = (short)(winTop + 170);
        lineRect[2] = (short)(winBottom - 18);
        MacToolbox.TETextBox(SpaceportGlobals.BarNewsLineB, lineRect, 0);
        MacToolbox.InvertRect(lineRect[0], lineRect[1], lineRect[2], lineRect[3]);
        Graphics.SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        // Mode + maskRgn are both literal 0 here (no visRgn mask), unlike the sibling
        // Redraw*Dialog.Run() blits — matches the ASM (li r7,0; li r8,0).
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                            MacToolbox.GetDialogPortRect(window), MacToolbox.GetDialogPortRect(window), 0, 0);
    }
}
