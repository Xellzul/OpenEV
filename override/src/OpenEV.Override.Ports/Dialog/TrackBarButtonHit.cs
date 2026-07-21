using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000ee44 (EV Override-11.c lines 7811-7862) — hit-test + press-track the
// BAR dialog's 6-button row ("BribeDialogHitTest" was an early transcription misname). Maps
// a local click point onto the 6 item rects, then while the button stays down
// re-tests the mouse and redraws the row with the hovered button pressed.
// Returns the final button index 0..5, or -1.
public static class TrackBarButtonHit
{
    public static int Run(int clickPoint)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var btnRects = new short[DrawBarButtonRow.ButtonCount][];   // auStack_4e: 6 contiguous stack Rects
        for (int i = 0; i < btnRects.Length; i++) btnRects[i] = new short[4];

        int window = DialogScratch.SpaceportDialogRecord;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (int i = 0; i < btnRects.Length; i++)
        {
            MacToolbox.GetDialogItem(window, i + 1, itemType, itemHandle, btnRects[i]);
        }
        int hitIndex = -1;
        for (int i = 0; i < btnRects.Length; i++)
        {
            if (MacToolbox.PtInRect(clickPoint, btnRects[i]))
            {
                hitIndex = i;
            }
        }
        if ((short)hitIndex != -1)
        {
            DrawBarButtonRow.Run((short)hitIndex);
            while (MacToolbox.StillDown())
            {
                int mousePoint = MacToolbox.GetMouse();
                int curIndex = -1;
                for (int i = 0; i < btnRects.Length; i++)
                {
                    if (MacToolbox.PtInRect(mousePoint, btnRects[i]))
                    {
                        curIndex = i;
                    }
                }
                short prevIndex = (short)hitIndex;
                hitIndex = curIndex;
                if ((short)curIndex != prevIndex)
                {
                    DrawBarButtonRow.Run((short)curIndex);
                }
            }
        }
        return hitIndex;
    }
}
