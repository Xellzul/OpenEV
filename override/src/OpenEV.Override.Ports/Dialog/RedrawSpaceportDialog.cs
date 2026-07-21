using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10037bb4 (EV Override-11.c lines 22870-22946) — redraw the spaceport HUB
// dialog's content: paints the dialog background into the BACKDROP GWorld,
// draws item 3 (spob name, Times 18), item 5 (spob picture + the CheatShowAll
// free-mem overlay), item 6 (description TETextBox), the tab bar, then blits
// backdrop -> dialog window over the portRect masked by the visRgn.
public static class RedrawSpaceportDialog
{
    public static void Run()
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var itemRect = new short[4];

        int window = SpaceportGlobals.DialogWindow;
        SetPortAndDevice.Run(Graphics.Model.RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.RGBForeColor((uint)Graphics.Model.UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(window));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 3, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.MoveTo(itemRect[1], itemRect[0] + 18);
            MacToolbox.TextFont(20);
            MacToolbox.TextSize(18);
            // Render via the typed record's string (name at +0x21 Pascal).
            MacToolbox.DrawString(Systems.Model.CurrentSpob.Rec.Name);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }
        MacToolbox.GetDialogItem(window, 5, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            MacToolbox.DrawPicture(SpaceportGlobals.SpobPictHandle, itemRect);
            if (Core.Model.WorldState.CheatShowAll != 0)
            {
                // free-mem overlay Rect: (top+3, left+3)-(top+14, left+67).
                var freeRect = new short[4]
                {
                    (short)(itemRect[0] + 3), (short)(itemRect[1] + 3),
                    (short)(itemRect[0] + 14), (short)(itemRect[1] + 67),
                };
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.PaintRect(freeRect);
                MacToolbox.MoveTo(freeRect[1] + 3, freeRect[0] + 9);
                MacToolbox.TextFont(3);
                MacToolbox.TextSize(9);
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.DrawString((MacToolbox.FreeMem() / 1000).ToString());
                MacToolbox.DrawString("K free");                            // Pascal at toc-0x47fd
                MacToolbox.ForeColor(QuickDrawColor.Black);
            }
        }
        MacToolbox.GetDialogItem(window, 6, itemType, itemHandle, itemRect);
        if (MacToolbox.RectInRgn(itemRect, MacToolbox.GetPortVisRgn(window)))
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.TETextBox(SpaceportGlobals.Description, itemRect, 0);
            MacToolbox.InvertRect(itemRect[0], itemRect[1], itemRect[2], itemRect[3]);
        }
        Graphics.DrawSpaceportTabBar.Run(-8);
        Graphics.SetGamePortAndDevice.Run();
        MacToolbox.SetPort(window);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(Graphics.Model.RenderGlobals.BackdropGWorld + 2, window + 2,
                        MacToolbox.GetDialogPortRect(window), MacToolbox.GetDialogPortRect(window), 0, MacToolbox.GetPortVisRgn(window));
    }
}
