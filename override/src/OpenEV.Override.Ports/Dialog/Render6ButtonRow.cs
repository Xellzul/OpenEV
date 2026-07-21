using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000ecf4 (EV Override-11.c lines 7777-7810): repaint the 6-button boarding
// row — each button's normal or selected PICT into its item rect. Items 1-4
// fill rects 0-3; items 6-7 fill rects 4-5 (item 5 is the text, skipped).
public static class Render6ButtonRow
{
    public static void Run(short activeButton)
    {
        short[][] itemRects =
        {
            new short[4], new short[4], new short[4],
            new short[4], new short[4], new short[4],
        };
        for (int i = 0; i < 4; i++)
        {
            MacToolbox.GetDialogItem(DialogScratch.BoardingDialogRecord, i + 1, 0, 0, itemRects[i]);
        }
        for (int i = 4; i < 6; i++)
        {
            MacToolbox.GetDialogItem(DialogScratch.BoardingDialogRecord, i + 2, 0, 0, itemRects[i]);
        }
        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (short index = 0; index < 6; index++)
        {
            if (activeButton == index)
            {
                MacToolbox.DrawPicture(DialogScratch.BoardingPicts[index * 2 + 1], itemRects[index]);
            }
            else
            {
                MacToolbox.DrawPicture(DialogScratch.BoardingPicts[index * 2], itemRects[index]);
            }
        }
    }
}
