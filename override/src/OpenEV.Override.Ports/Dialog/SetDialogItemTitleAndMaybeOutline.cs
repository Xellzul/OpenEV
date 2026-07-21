using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// FUN_10077328 (EV Override-11.c lines 50427-50474) — set a multi-button-modal
// button's title and, when asked, draw the Mac default-button ring around it.
// SetControlTitle routes through the managed SetDialogItemText seam: a dialog item holds its
// button label in DlgItem.Text — the same field the DrawDialog button painter reads — so there
// is no separate ControlRecord title to set.
public static class SetDialogItemTitleAndMaybeOutline
{
    public static void Run(int dialog, short itemNo, string title, byte outline)
    {
        var itemHandle = new int[1];
        var itemRect = new short[4];

        MacToolbox.GetDialogItem(dialog, itemNo, null, itemHandle, itemRect);
        MacToolbox.SetDialogItemText(itemHandle[0], title);
        if (outline != 0)
        {
            var penState = new short[13];   // 26-byte PenState record (Get/SetPenState are no-op stubs)
            MacToolbox.GetPenState(penState);
            MacToolbox.PenNormal();
            MacToolbox.PenSize(3, 3);   // records the pen width, but FrameRoundRect bakes its own const 3px and ignores it
            MacToolbox.InsetRect(itemRect, -4, -4);
            MacToolbox.FrameRoundRect(itemRect, 16, 16);   // oval corner width/height (px)
            MacToolbox.SetPenState(penState);
        }
    }
}
