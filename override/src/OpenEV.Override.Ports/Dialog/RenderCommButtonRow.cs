using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000d9e4 (EV Override-11.c lines 7120-7172): repaints the in-flight ship
// comm dialog's 3 button PICTs for the given pressed-button state.
// The decompile's TOC-relative addresses use an unassigned r2/RTOC artifact;
// the real base is GameToc.
public static class RenderCommButtonRow
{
    public static void Run(short selectedButton)
    {
        // The Mac frame's auStack_32/auStack_2a/auStack_22 (+ an unused 4th slot)
        // are ADJACENT 8-byte Rects the loop fills via `auStack_32 + i*8`.
        short[][] itemRects = { new short[4], new short[4], new short[4], new short[4] };
        for (int i = 0; i < 4; i++)
        {
            MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, i + 1, 0, 0, itemRects[i]);
        }

        if (selectedButton == 0)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictHail1, itemRects[0]);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictHail0, itemRects[0]);
        }
        byte commActive = (byte)(ShipAi.HasEngagedAllyOrCarrier(ShipTable.FromPtr(DialogScratch.DialogShipPtr)) ? 1 : 0);
        if (commActive == 0)
        {
            if (selectedButton == 1)
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1Sel, itemRects[1]);
            }
            else
            {
                MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1, itemRects[1]);
            }
        }
        else if (selectedButton == 1)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1ActSel, itemRects[1]);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB1Act, itemRects[1]);
        }
        if (selectedButton == 2)
        {
            MacToolbox.DrawPicture(DialogScratch.CommBtnPictB2Sel, itemRects[2]);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.CommButtonPicts[0], itemRects[2]);
        }
    }
}
