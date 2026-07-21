using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000d7ac (EV Override-11.c lines 7042-7070): repaint the 2-button confirm
// row (DLOG 0x3fa) — each button's normal or selected PICT into its item rect.
public static class Render2ButtonRow
{
    public static void Run(short activeButton)
    {
        short[] itemRect = new short[4];

        int[] picts = DialogScratch.ConfirmButtonPicts;
        for (short index = 0; index < 2; index++)
        {
            MacToolbox.GetDialogItem(GameData.AlertDialog, index + 1, 0, 0, itemRect);
            if (activeButton == index)
            {
                MacToolbox.DrawPicture(picts[index * 2 + 1], itemRect);
            }
            else
            {
                MacToolbox.DrawPicture(picts[index * 2], itemRect);
            }
        }
    }
}
