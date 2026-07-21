using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000ea3c (EV Override-11.c 7692-7720) — draws the two game-speed dialog
// buttons (DITL items 1 and 2) from DialogScratch.GameSpeedButtonPicts, the
// selected one in its pressed art.
public static class DrawGameSpeedDialogButtons
{
    public static void Run(short selectedButton)
    {
        var buttonRects = new[] { new short[4], new short[4] };

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(DialogScratch.BuyShipDialogRecord, 1, null, null, buttonRects[0]);
        MacToolbox.GetDialogItem(DialogScratch.BuyShipDialogRecord, 2, null, null, buttonRects[1]);
        for (short buttonIndex = 0; buttonIndex < 2; buttonIndex++)
        {
            bool pressed = selectedButton == buttonIndex;
            MacToolbox.DrawPicture(DialogScratch.GameSpeedButtonPicts[buttonIndex * 2 + (pressed ? 1 : 0)], buttonRects[buttonIndex]);
        }
    }
}
