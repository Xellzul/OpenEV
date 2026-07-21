using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000de8c (EV Override-11.c lines 7275-7324) — hit-test + press-track the
// mission-board's 2-button row. Returns 0 (accept, item 1), 1 (leave, item 7)
// or -1.
public static class TrackBbsButtonHit
{
    public static int Run(int clickPoint)
    {
        var acceptRect = new short[4];
        var leaveRect = new short[4];

        int window = MissionBoardGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 1, 0, 0, acceptRect);
        MacToolbox.GetDialogItem(window, 7, 0, 0, leaveRect);
        int hitIndex = -1;
        if (MacToolbox.PtInRect(clickPoint, acceptRect))
        {
            hitIndex = 0;
        }
        if (MacToolbox.PtInRect(clickPoint, leaveRect))
        {
            hitIndex = 1;
        }
        if ((short)hitIndex != -1)
        {
            DrawBbsButtonRow.Run((short)hitIndex);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();
                int currentIndex = -1;
                if (MacToolbox.PtInRect(mousePoint, acceptRect))
                {
                    currentIndex = 0;
                }
                if (MacToolbox.PtInRect(mousePoint, leaveRect))
                {
                    currentIndex = 1;
                }
                short prevIndex = (short)hitIndex;
                hitIndex = currentIndex;
                if ((short)currentIndex != prevIndex)
                {
                    DrawBbsButtonRow.Run((short)currentIndex);
                }
            }
        }
        return hitIndex;
    }
}
