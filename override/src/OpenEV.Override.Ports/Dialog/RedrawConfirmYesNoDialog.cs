using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1004120c (EV Override-11.c lines 26756-26788): repaint the capture-ship
// confirm dialog — black fill + frame, the escort-or-trade question text into
// item 3, and the un-pressed 2-button row.
public static class RedrawConfirmYesNoDialog
{
    // C string @0x100842c5 (GameToc-0x439b, dumped) — was StrncpyPad'd into a
    // 256-byte Pascal scratch buffer.
    private const string EscortOrCaptureQuestion =
        "Do you want to use this ship as an escort to yours, or would you rather "
        + "trade places with its captain and use it as your own?";

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
        MacToolbox.TETextBox(EscortOrCaptureQuestion, itemRect, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.BackColor(QuickDrawColor.White);
        Render2ButtonRow.Run(-1);
    }
}
