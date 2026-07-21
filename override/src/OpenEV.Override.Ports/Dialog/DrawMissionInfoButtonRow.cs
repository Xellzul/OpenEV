using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000e254 (EV Override-11.c lines 7417-7450) — draw the mission-info
// dialog's 2-button row: button 0 (leave, item 1) and button 1 (abort, item 5)
// from the {normal, pressed} PICT pairs in MissionInfoGlobals.Picts. The abort
// button is painted out while SpaceportGlobals.BbsSelectedRow == -1 (shared
// with the BBS dialog). activeButton draws pressed art (-1 = none).
public static class DrawMissionInfoButtonRow
{
    public static void Run(short activeButton)
    {
        var leaveRect = new short[4];
        var abortRect = new short[4];

        int window = MissionInfoGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 1, 0, 0, leaveRect);
        MacToolbox.GetDialogItem(window, 5, 0, 0, abortRect);
        if (activeButton == 0)
        {
            MacToolbox.DrawPicture(MissionInfoGlobals.Picts[1], leaveRect);
        }
        else
        {
            MacToolbox.DrawPicture(MissionInfoGlobals.Picts[0], leaveRect);
        }
        if (SpaceportGlobals.BbsSelectedRow == -1)
        {
            MacToolbox.PaintRect(abortRect);
        }
        else if (activeButton == 1)
        {
            MacToolbox.DrawPicture(MissionInfoGlobals.Picts[3], abortRect);
        }
        else
        {
            MacToolbox.DrawPicture(MissionInfoGlobals.Picts[2], abortRect);
        }
    }
}
