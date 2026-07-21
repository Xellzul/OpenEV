using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Mission;

// FUN_1000e518 (EV Override-11.c lines 7514-7559) — paint the single-mission
// OFFER dialog's button row: single-OK layout draws item 6 with the OK PICT
// pair (Picts[0]/[1]), accept/refuse layout draws items 1/2 with their
// normal/pressed pairs (Picts[btn*2]/[btn*2+1]); activeButton picks which
// button shows pressed (-1 = none).
public static class RenderSingleMissionButtonRow
{
    public static void Run(short activeButton)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        if (MissionBoardGlobals.OfferAcceptRefuseLayout == 0)
        {
            var okRect = new short[4];
            MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 6, itemType, itemHandle, okRect);
            if (activeButton == -1)
            {
                MacToolbox.DrawPicture(MissionBoardGlobals.Picts[0], okRect);
            }
            else
            {
                MacToolbox.DrawPicture(MissionBoardGlobals.Picts[1], okRect);
            }
        }
        else
        {
            var buttonRects = new[] { new short[4], new short[4] };
            for (short i = 0; i < buttonRects.Length; i = (short)(i + 1))
            {
                MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, i + 1, itemType, itemHandle, buttonRects[i]);
            }
            for (short btn = 0; btn < buttonRects.Length; btn = (short)(btn + 1))
            {
                if (activeButton == btn)
                {
                    MacToolbox.DrawPicture(MissionBoardGlobals.Picts[btn * 2 + 1], buttonRects[btn]);
                }
                else
                {
                    MacToolbox.DrawPicture(MissionBoardGlobals.Picts[btn * 2], buttonRects[btn]);
                }
            }
        }
    }
}
