using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10015548 (EV Override-11.c 10461-10580) — the boarding/plunder panel of
// the boarding (6-button) dialog: "Select what to plunder from this ship:" with
// the victim's Cargo / Credits / Ammo / Fuel and the Capture Odds line. (The
// "RedrawPilotInfoPanel" auto-name is a misnomer.) Drawn into the backdrop
// GWorld and composited onto the boarding dialog window.
public static class RedrawPilotInfoPanel
{
    public static void Run()
    {
        var itemRect = new short[4]; // item-5 rect {top,left,bottom,right}

        int textColor = UiColors.Unexplored;
        int dimColor = UiColors.DialogFore;
        SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
        MacToolbox.TextFont(3);
        MacToolbox.TextSize(9);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));
        MacToolbox.RGBForeColor((uint)dimColor);
        MacToolbox.FrameRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(DialogScratch.BoardingDialogRecord, 5, null, null, itemRect);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.PaintRect(itemRect);
        MacToolbox.RGBForeColor((uint)textColor);
        MacToolbox.MoveTo(itemRect[1], itemRect[0] + 12);
        MacToolbox.DrawString("Select what to plunder from this ship:");
        MacToolbox.RGBForeColor((uint)textColor);
        MacToolbox.MoveTo(itemRect[1], itemRect[0] + 28);
        MacToolbox.DrawString("Cargo:");
        MacToolbox.MoveTo(itemRect[1], itemRect[0] + 42);
        MacToolbox.DrawString("Credits:");
        MacToolbox.MoveTo(itemRect[1], itemRect[0] + 56);
        MacToolbox.DrawString("Ammo:");
        MacToolbox.MoveTo(itemRect[1] + 50, itemRect[0] + 28);
        if (DialogScratch.BoardingSalvageCargoIndex == -1)
        {
            MacToolbox.RGBForeColor((uint)dimColor);
            MacToolbox.DrawString("None");
        }
        else
        {
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.DrawString(DialogScratch.BoardingSalvageCargoQty.ToString());
            MacToolbox.DrawString(" ton");
            if (DialogScratch.BoardingSalvageCargoQty > 1)
            {
                MacToolbox.DrawString("s");
            }
            MacToolbox.DrawString(" of ");
            MacToolbox.DrawString(ResourceGlobals.NamesStr0fa1[DialogScratch.BoardingSalvageCargoIndex]);
        }
        MacToolbox.MoveTo(itemRect[1] + 50, itemRect[0] + 42);
        if (DialogScratch.BoardingSalvageCredits < 1)
        {
            MacToolbox.RGBForeColor((uint)dimColor);
            MacToolbox.DrawString("None");
        }
        else
        {
            MacToolbox.ForeColor(QuickDrawColor.White);
            FormatCredits.Run(DialogScratch.BoardingSalvageCredits);
        }
        MacToolbox.MoveTo(itemRect[1] + 50, itemRect[0] + 56);
        if (DialogScratch.BoardingSalvageAmmoType == -1)
        {
            MacToolbox.RGBForeColor((uint)dimColor);
            MacToolbox.DrawString("None");
        }
        else
        {
            MacToolbox.ForeColor(QuickDrawColor.White);
            MacToolbox.DrawString(DialogScratch.BoardingSalvageAmmoQty.ToString());
            MacToolbox.DrawString(" ");
            for (short scanIndex = 0; scanIndex < OutfitTable.Count; scanIndex++)
            {
                var outfit = OutfitTable.Store[scanIndex];
                if (outfit.ModType[0] == OutfitModType.Ammo &&
                    DialogScratch.BoardingSalvageAmmoType == outfit.ModValue[0])
                {
                    MacToolbox.DrawString(outfit.Name);
                    break;
                }
                if (outfit.ModType[1] == OutfitModType.Ammo &&
                    DialogScratch.BoardingSalvageAmmoType == outfit.ModValue[1])
                {
                    MacToolbox.DrawString(outfit.Name);
                    break;
                }
            }
            if (DialogScratch.BoardingSalvageAmmoQty > 1)
            {
                MacToolbox.DrawString("s");
            }
        }
        MacToolbox.MoveTo(itemRect[1] + 1, itemRect[0] + 70);
        MacToolbox.RGBForeColor((uint)textColor);
        MacToolbox.DrawString("Fuel: ");
        MacToolbox.MoveTo(itemRect[1] + 50, itemRect[0] + 70);
        if (DialogScratch.BoardingSalvageFuel < 1)
        {
            MacToolbox.RGBForeColor((uint)dimColor);
            MacToolbox.DrawString("None");
        }
        else
        {
            MacToolbox.ForeColor(QuickDrawColor.White);
            FormatCredits.Run((int)DialogScratch.BoardingSalvageFuel);
        }
        MacToolbox.MoveTo(itemRect[1] + 120, itemRect[0] + 70);
        MacToolbox.RGBForeColor((uint)textColor);
        MacToolbox.DrawString("Capture Odds: ");
        MacToolbox.MoveTo(itemRect[1] + 195, itemRect[0] + 70);
        MacToolbox.ForeColor(QuickDrawColor.White);
        MacToolbox.DrawString(DialogScratch.BoardingCaptureChance.ToString());
        MacToolbox.DrawString("%");
        MacToolbox.ForeColor(QuickDrawColor.Black);
        Render6ButtonRow.Run(-1);
        SetGamePortAndDevice.Run();
        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
        MacToolbox.ForeColor(QuickDrawColor.Black);
        var portRect = MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord);
        MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, DialogScratch.BoardingDialogRecord + 2, portRect,
                            portRect, 0, MacToolbox.GetDialogVisRgn(DialogScratch.BoardingDialogRecord));
    }
}
