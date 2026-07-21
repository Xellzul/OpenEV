using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000e7f8 (EV Override-11.c 7611-7638) — draws the player-info dialog's
// 4-tab row from the {normal, pressed} PICT pairs in PlayerInfoGlobals.Picts
// into the rects of DITL items 1..4; highlightTabA/B draw pressed art.
public static class RenderPlayerInfoTabRow
{
    public static void Run(short highlightTabA, short highlightTabB)
    {
        var tabRect = new short[4];

        int window = PlayerInfoGlobals.DialogWindow;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (short tab = 0; tab < 4; tab++)
        {
            MacToolbox.GetDialogItem(window, tab + 1, null, null, tabRect);
            bool pressed = highlightTabB == tab || highlightTabA == tab;
            MacToolbox.DrawPicture(PlayerInfoGlobals.Picts[tab * 2 + (pressed ? 1 : 0)], tabRect);
        }
    }
}
