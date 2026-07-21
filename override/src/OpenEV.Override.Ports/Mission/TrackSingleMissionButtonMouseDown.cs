using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Mission;

// FUN_1000e350 (EV Override-11.c lines 7451-7513) — hit-test + press-track the
// single-mission OFFER dialog's button row: collects the two button rects
// (items 1/2 in accept/refuse layout; item 6 TWICE — both slots get the same
// rect — in single-OK layout, decompile 7472), finds the pressed button, then
// follows the mouse while the button is held, repainting the row on every
// transition. Returns the button index under the mouse at release (-1 = none).
public static class TrackSingleMissionButtonMouseDown
{
    public static int Run(int mousePt)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var buttonRects = new[] { new short[4], new short[4] };

        MacToolbox.ForeColor(QuickDrawColor.Black);
        if (MissionBoardGlobals.OfferAcceptRefuseLayout == 0)
        {
            // Single-OK layout: the original fills BOTH rect slots from item 6.
            for (short i = 0; i < buttonRects.Length; i = (short)(i + 1))
            {
                MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, 6, itemType, itemHandle, buttonRects[i]);
            }
        }
        else
        {
            for (short i = 0; i < buttonRects.Length; i = (short)(i + 1))
            {
                MacToolbox.GetDialogItem(MissionBoardGlobals.DialogWindow, i + 1, itemType, itemHandle, buttonRects[i]);
            }
        }
        int hit = HitTestButtons(mousePt, buttonRects);
        if ((short)hit != -1)
        {
            RenderSingleMissionButtonRow.Run((short)hit);
            while (MacToolbox.StillDown())
            {
                int mouseLoc = MacToolbox.GetMouse();
                int cur = HitTestButtons(mouseLoc, buttonRects);
                short prev = (short)hit;
                hit = cur;
                if ((short)cur != prev)
                {
                    RenderSingleMissionButtonRow.Run((short)cur);
                }
            }
        }
        return hit;
    }

    // FUN_1000e350 7483-7488 / 7494-7499 — both button-hit scans: no early break, so a
    // later overlapping rect wins (matches the decompile exactly; buttons never overlap
    // in practice). -1 = no button under pt.
    private static int HitTestButtons(int pt, short[][] rects)
    {
        int hit = -1;
        for (short i = 0; i < rects.Length; i = (short)(i + 1))
        {
            if (MacToolbox.PtInRect(pt, rects[i]))
            {
                hit = i;
            }
        }
        return hit;
    }
}
