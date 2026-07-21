using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10041320 (EV Override-11.c lines 26789-26809).
public static class DrawDefaultButtonOutline
{
    public static void Run(int dialog, short itemNo)
    {
        // Managed Rect out-param for GetDialogItem (top,left,bottom,right); typeOut/handleOut
        // are unused (the toolbox shim no-ops). Inset by 4 px and frame as a rounded button
        // outline. (This one-shot draw at dialog-open is erased by ModalDialog's entry
        // redraw; DrawDlgButton re-draws the identical ring with the items.)
        short[] itemRect = new short[4];
        MacToolbox.SetPort(dialog);
        MacToolbox.GetDialogItem(dialog, itemNo, 0, 0, itemRect);
        MacToolbox.PenSize(3, 3);
        MacToolbox.InsetRect(itemRect, -4, -4);
        MacToolbox.FrameRoundRect(itemRect[0], itemRect[1], itemRect[2], itemRect[3], 16, 16);
    }
}
