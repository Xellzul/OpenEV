using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10073418 (EV Override-11.c lines 47650-47668): frame the default button's
// item rect with the 3px round-rect outline.
public static class DefaultButtonOutline
{
    public static void Run(int theDialog, int itemNo)
    {
        short[] itemRect = new short[4];

        MacToolbox.PenNormal();
        MacToolbox.GetDialogItem(theDialog, itemNo, 0, 0, itemRect);
        MacToolbox.PenSize(3, 3);
        MacToolbox.FrameRoundRect(itemRect, 16, 16);   // oval corner width/height
        MacToolbox.PenSize(1, 1);
    }
}
