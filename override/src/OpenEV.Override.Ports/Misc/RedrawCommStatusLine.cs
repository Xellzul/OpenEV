using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Misc;

// FUN_10013614 from EV Override-11.c lines 9714-9765 — the mission-PAYMENT comm
// status line (drawn into the spaceport/hail dialog whose record is DialogScratch.
// BuyShipDialogRecord). The "BuyShip*" DialogScratch names are misnomers inherited
// from an earlier transcription: this function draws the mission-pay prompt ("The pay for having
// completed this mission is ", "I'll pay you ", "Pay me/us " + N + " credits."),
// switched by BuyShipMode.
public static class RedrawCommStatusLine
{
    public static void Run()
    {
        var dialog = DialogScratch.BuyShipDialogRecord;
        // GetDialogItem item 3 rect: {top, left, bottom, right}.
        short[] itemRect = new short[4];

        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(dialog));
        MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(dialog));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.RGBForeColor((uint)UiColors.Frame);
        MacToolbox.GetDialogItem(dialog, 3, 0, 0, itemRect);
        MacToolbox.FrameRect(itemRect);
        MacToolbox.ForeColor(QuickDrawColor.White);
        MacToolbox.MoveTo(itemRect[1] + 4, itemRect[0] + 12);
        if (DialogScratch.BuyShipMode == 0)
        {
            MacToolbox.DrawString("The pay for having completed this mission is ");  // GameToc-0x5cbb
        }
        if (DialogScratch.BuyShipMode == 1)
        {
            MacToolbox.DrawString("I'll pay you ");                                  // GameToc-0x5c8d
        }
        if (DialogScratch.BuyShipMode == 2)
        {
            if (GameData.Player.TargetSlot == -1)
            {
                MacToolbox.DrawString("Pay us ");                                   // GameToc-0x5c77
            }
            else
            {
                MacToolbox.DrawString("Pay me ");                                   // GameToc-0x5c7f
            }
        }
        FormatCredits.Run(GameData.BuyShipPriceCell);
        MacToolbox.DrawString(" credits.");                                         // GameToc-0x5c6f
        MacToolbox.ForeColor(QuickDrawColor.Black);
        DrawGameSpeedDialogButtons.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(dialog);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        var portRect = MacToolbox.GetDialogPortRect(dialog);   // src == dst == the window's own content rect
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, dialog + 2, portRect,
                        portRect, 0, MacToolbox.GetDialogVisRgn(dialog));
        return;
    }
}
