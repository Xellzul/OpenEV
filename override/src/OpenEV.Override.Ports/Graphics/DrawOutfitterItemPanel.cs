using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10010a2c (EV Override-11.c 8639-8761). NOTE: the auto-name is a MISNOMER —
// this is the COMM/HAIL dialog's ship-info panel: the hailed ship's portrait
// PICT (item 11), its "Alignment & Type:" / "Class:" / "Status:" text block
// (item 12, from the DialogShipPtr ship record), and the name box (item 10),
// drawn into the backdrop GWorld and composited onto the dialog window.
public static class DrawOutfitterItemPanel
{
    public static void Run()
    {
        var itemRect = new short[4];
        var itemRectCopy = new short[4]; // inset text-box copy

        int highlightColor = UiColors.DialogFore;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        int dlg = DialogScratch.SpaceportCommDialogRecord;
        var hailed = ShipTable.FromPtr(DialogScratch.DialogShipPtr);

        var portRect = MacToolbox.GetDialogPortRect(dlg);
        itemRect[0] = portRect[0];
        itemRect[1] = portRect[1];
        itemRect[2] = portRect[2];
        itemRect[3] = portRect[3];
        MacToolbox.InsetRect(itemRect, 1, 1);
        MacToolbox.PaintRect(itemRect);
        MacToolbox.RGBForeColor((uint)highlightColor);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(dlg));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        RenderCommButtonRow.Run(-1);

        MacToolbox.GetDialogItem(dlg, 11, null, null, itemRect);
        bool visible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (visible)
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.DrawPicture(DialogScratch.SpaceportPersonPict, itemRect);
            MacToolbox.InsetRect(itemRect, -5, -2);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }

        MacToolbox.GetDialogItem(dlg, 12, null, null, itemRect);
        visible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (visible)
        {
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(itemRect);
            MacToolbox.RGBForeColor((uint)highlightColor);
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            if (hailed.PersIndex == ShipRecord.KamikazePersIndex)
            {
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                MacToolbox.DrawString("Alignment & Type:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo(itemRect[1] + 5, itemRect[0] + 25);
                MacToolbox.DrawString("Ambrosia Mascot");
            }
            else if (hailed.Govt == -1)
            {
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                MacToolbox.DrawString("Class: ");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.DrawString(DialogScratch.SpaceportNameText);
            }
            else
            {
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
                MacToolbox.DrawString("Alignment & Type:");
                MacToolbox.ForeColor(QuickDrawColor.White);
                MacToolbox.MoveTo(itemRect[1] + 5, itemRect[0] + 25);
                MacToolbox.DrawString(DialogScratch.SpaceportGovtText);
                MacToolbox.DrawString(" ");
                MacToolbox.DrawString(DialogScratch.SpaceportNameText);
            }
            bool engaged = ShipAi.HasEngagedAllyOrCarrier(hailed);
            if (!engaged)
            {
                if (hailed.OwnerSlot == 0 && DialogScratch.SpaceportHiredFlag == 0)
                {
                    MacToolbox.MoveTo(itemRect[1], itemRect[0] + 37);
                    MacToolbox.RGBForeColor((uint)highlightColor);
                    MacToolbox.DrawString("Status: ");
                    MacToolbox.RGBForeColor((uint)UiColors.Neutral);
                    MacToolbox.DrawString(hailed.IsCarriedFighter == 0 ? "Escort" : "Hired Escort");
                }
            }
            else
            {
                MacToolbox.MoveTo(itemRect[1], itemRect[0] + 37);
                MacToolbox.RGBForeColor((uint)highlightColor);
                MacToolbox.DrawString("Status: ");
                MacToolbox.ForeColor(QuickDrawColor.Red);
                MacToolbox.TextFace(1);
                MacToolbox.DrawString("Hostile");
                MacToolbox.TextFace(0);
            }
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }

        MacToolbox.GetDialogItem(dlg, 10, null, null, itemRect);
        visible = MacToolbox.RectInRgn(itemRect, MacToolbox.GetDialogVisRgn(dlg));
        if (visible)
        {
            string hailedName = DialogScratch.SpaceportHailText;
            MacToolbox.TextFont(3);
            MacToolbox.TextSize(9);
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.PaintRect(itemRect);
            itemRectCopy[0] = itemRect[0]; itemRectCopy[1] = itemRect[1];
            itemRectCopy[2] = itemRect[2]; itemRectCopy[3] = itemRect[3];
            MacToolbox.InsetRect(itemRectCopy, 4, 2);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.TETextBox(hailedName, itemRectCopy, 0);
            MacToolbox.InvertRect(itemRect);
            MacToolbox.RGBForeColor((uint)UiColors.Frame);
            MacToolbox.FrameRect(itemRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
        }

        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(dlg);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, dlg + 2,
            MacToolbox.GetDialogPortRect(dlg), MacToolbox.GetDialogPortRect(dlg), 0, MacToolbox.GetDialogVisRgn(dlg));
    }
}
