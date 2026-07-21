using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000eb40 (EV Override-11.c lines 7721-7776) — mouse-down tracker for the
// boarding six-button row (DialogScratch.BoardingDialogRecord): items 1..4
// then 6,7 (the row skips item 5). Returns the pressed cell index 0..5 or -1,
// repainting the pressed cell as the mouse drags while held.
public static class Track6ButtonMouseDown
{
    public static int Run(int mousePt)
    {
        var rects = new short[6][];
        for (int i = 0; i < 6; i = i + 1) rects[i] = new short[4];

        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (int i = 0; (short)i < 4; i = i + 1)
        {
            MacToolbox.GetDialogItem(DialogScratch.BoardingDialogRecord, i + 1, 0, 0, rects[i]);
        }
        for (int i = 4; (short)i < 6; i = i + 1)
        {
            MacToolbox.GetDialogItem(DialogScratch.BoardingDialogRecord, i + 2, 0, 0, rects[i]);
        }
        int selectedIndex = HitTest(mousePt, rects);
        if ((short)selectedIndex != -1)
        {
            Render6ButtonRow.Run((short)selectedIndex);
            while (MacToolbox.StillDown())
            {
                int loopPt = MacToolbox.GetMouse();
                int newIndex = HitTest(loopPt, rects);
                short prevIndex = (short)selectedIndex;
                selectedIndex = newIndex;
                if ((short)newIndex != prevIndex)
                {
                    Render6ButtonRow.Run((short)newIndex);
                }
            }
        }
        return selectedIndex;
    }

    // Last matching rect wins (no early break) — matches the decompile's
    // unconditional sequential PtInRect tests over all 6 rects.
    private static int HitTest(int packedPoint, short[][] rects)
    {
        int hit = -1;
        for (int i = 0; (short)i < 6; i = i + 1)
        {
            if (MacToolbox.PtInRect(packedPoint, rects[i]))
            {
                hit = i;
            }
        }
        return hit;
    }
}
