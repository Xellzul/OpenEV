using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000d63c (EV Override-11.c lines 6992-7041): mouse-tracks the 2-button
// confirm (yes/no) alert row; returns the button index at release (-1 none).
public static class TrackDialogButtonClick
{
    public static int Run(int mousePtArg)
    {
        short[][] itemRects = { new short[4], new short[4] };
        for (int i = 0; i < 2; i++)
        {
            MacToolbox.GetDialogItem(GameData.AlertDialog, i + 1, 0, 0, itemRects[i]);
        }
        int selectedIndex = HitTest(mousePtArg, itemRects);
        if ((short)selectedIndex != -1)
        {
            Render2ButtonRow.Run((short)selectedIndex);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();   // packed (v<<16 | h)
                int newIndex = HitTest(mousePoint, itemRects);
                short prevIndex = (short)selectedIndex;
                selectedIndex = newIndex;
                if ((short)newIndex != prevIndex)
                {
                    Render2ButtonRow.Run((short)newIndex);
                }
            }
        }
        return selectedIndex;
    }

    // First of the button rects containing the packed point, else -1.
    private static int HitTest(int packedPoint, short[][] itemRects)
    {
        for (int i = 0; i < itemRects.Length; i++)
        {
            if (MacToolbox.PtInRect(packedPoint, itemRects[i])) return i;
        }
        return -1;
    }
}
