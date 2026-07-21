using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_1003e83c (EV Override-11.c lines 25677-25711): repaint the generic alert —
// black fill + frame, the alert message into item 3, the un-pressed OK button.
public static class RedrawGenericAlertDialog
{
    public static void Run()
    {
        short[] itemRect = new short[4];

        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(GameData.AlertDialog));
        MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(GameData.AlertDialog));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.ForeColor(QuickDrawColor.White);
        MacToolbox.BackColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(GameData.AlertDialog, 3, 0, 0, itemRect);
        MacToolbox.TETextBox(AlertText.Message, itemRect, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.BackColor(QuickDrawColor.White);
        MacToolbox.GetDialogItem(GameData.AlertDialog, 1, 0, 0, itemRect);
        DrawButtonPressed.Run(itemRect, false);
    }
}
