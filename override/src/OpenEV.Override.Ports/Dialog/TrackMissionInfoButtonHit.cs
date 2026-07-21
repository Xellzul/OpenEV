using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000e0e0 (EV Override-11.c lines 7360-7411) — hit-test + press-track the
// mission-info dialog's 2-button row. Button 1 (abort, item 5) only hits while
// SpaceportGlobals.BbsSelectedRow != -1 (shared with the BBS dialog). Returns
// 0 (leave), 1 (abort) or -1.
public static class TrackMissionInfoButtonHit
{
    public static int Run(int clickPoint)
    {
        var leaveRect = new short[4];
        var abortRect = new short[4];

        int window = MissionInfoGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 1, 0, 0, leaveRect);
        MacToolbox.GetDialogItem(window, 5, 0, 0, abortRect);
        int hitIndex = -1;
        if (MacToolbox.PtInRect(clickPoint, leaveRect))
        {
            hitIndex = 0;
        }
        if (MacToolbox.PtInRect(clickPoint, abortRect) && SpaceportGlobals.BbsSelectedRow != -1)
        {
            hitIndex = 1;
        }
        if ((short)hitIndex != -1)
        {
            DrawMissionInfoButtonRow.Run((short)hitIndex);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();
                int currentIndex = -1;
                if (MacToolbox.PtInRect(mousePoint, leaveRect))
                {
                    currentIndex = 0;
                }
                if (MacToolbox.PtInRect(mousePoint, abortRect) && SpaceportGlobals.BbsSelectedRow != -1)
                {
                    currentIndex = 1;
                }
                short prevIndex = (short)hitIndex;
                hitIndex = currentIndex;
                if ((short)currentIndex != prevIndex)
                {
                    DrawMissionInfoButtonRow.Run((short)currentIndex);
                }
            }
        }
        return hitIndex;
    }
}
