using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000dfdc (EV Override-11.c lines 7330-7354) — draw the mission-board's
// 2-button row: button 0 (accept, item 1) and button 1 (leave, item 7) from
// the {normal, pressed} PICT pairs in MissionBoardGlobals.Picts. activeButton
// draws pressed art (-1 = none).
public static class DrawBbsButtonRow
{
    public static void Run(short activeButton)
    {
        var btnRects = new short[2][];
        for (int i = 0; i < 2; i++) btnRects[i] = new short[4];

        int window = MissionBoardGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(window, 1, 0, 0, btnRects[0]);
        MacToolbox.GetDialogItem(window, 7, 0, 0, btnRects[1]);
        for (short btn = 0; btn < 2; btn = (short)(btn + 1))
        {
            if (activeButton == btn)
            {
                MacToolbox.DrawPicture(MissionBoardGlobals.Picts[btn * 2 + 1], btnRects[btn]);
            }
            else
            {
                MacToolbox.DrawPicture(MissionBoardGlobals.Picts[btn * 2], btnRects[btn]);
            }
        }
    }
}
