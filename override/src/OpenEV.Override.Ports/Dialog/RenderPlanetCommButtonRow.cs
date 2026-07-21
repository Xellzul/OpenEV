using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000dcb4 (EV Override-11.c lines 7222-7271): repaints the 3-button
// spaceport comm row PICTs for the given pressed-button state.
public static class RenderPlanetCommButtonRow
{
    public static void Run(short activeButton)
    {
        short[] itemRect = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 1, 0, 0, itemRect);
        if (activeButton == 0)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB2Sel, itemRect);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommButtonPicts[0], itemRect);
        }
        MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 3, 0, 0, itemRect);
        if (GameData.Spobs[GameData.Player.NavTargetSpob].TradingEnabled == 0)
        {
            if (activeButton == 2)
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB2ActSel, itemRect);
            }
            else
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB2Act, itemRect);
            }
        }
        else if (activeButton == 2)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictHail1, itemRect);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictHail0, itemRect);
        }
        MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 2, 0, 0, itemRect);
        if (DialogScratch.CommHailGateFlag == 0)
        {
            if (activeButton == 1)
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1ActSel, itemRect);
            }
            else
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1Act, itemRect);
            }
        }
        else if (activeButton == 1)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1Sel, itemRect);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1, itemRect);
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
    }
}
