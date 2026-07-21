using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000f278 (EV Override-11.c lines 7958-7995): repaint the two bribe-amount
// buttons (items 4-5) — blacked out when the player can't afford the tier.
public static class RenderBribeButtons
{
    public static void Run(short activeButton)
    {
        short[][] itemRects = { new short[4], new short[4] };
        for (int i = 0; i < itemRects.Length; i++)
        {
            MacToolbox.GetDialogItem(DialogScratch.BribeDialogPtr, i + 4, 0, 0, itemRects[i]);
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        if (GameData.Player.Credits < 1000)
        {
            MacToolbox.PaintRect(itemRects[0]);
        }
        else if (activeButton == 0)
        {
            MacToolbox.DrawPicture(DialogScratch.BribeBtnPictB0Sel, itemRects[0]);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.BribeBtnPictB0, itemRects[0]);
        }
        if (GameData.Player.Credits < 5000)
        {
            MacToolbox.PaintRect(itemRects[1]);
        }
        else if (activeButton == 1)
        {
            MacToolbox.DrawPicture(DialogScratch.BribeBtnPictB1Sel, itemRects[1]);
        }
        else
        {
            MacToolbox.DrawPicture(DialogScratch.BribeBtnPictB1, itemRects[1]);
        }
    }
}
