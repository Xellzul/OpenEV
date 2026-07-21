using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// FUN_1000f100 (EV Override-11.c lines 7904-7957) — tracks a mouse-down over
// the two bet/bribe buttons (items 4/5) of the two-button dialog at
// DialogScratch.BribeDialogPtr (the slot-machine bet / bribe offer dialog).
// While the button stays down it follows the mouse, gating button 0 on
// credits > 999 and button 1 on credits > 4999 (the 1000/5000 stakes),
// redrawing the pressed state via RenderBribeButtons on every change.
// Returns the 0/1 button index or -1. Called by RunSlotMachine.
public static class TrackBuyDialog
{
    public static int Run(int mousePoint)
    {
        var itemType = new short[1];
        var itemHandle = new int[1];
        var betRects = new short[2][] { new short[4], new short[4] };

        MacToolbox.ForeColor(QuickDrawColor.Black);
        for (int itemIndex = 0; itemIndex < betRects.Length; itemIndex = itemIndex + 1)
        {
            MacToolbox.GetDialogItem(
                DialogScratch.BribeDialogPtr,
                itemIndex + 4,
                itemType,
                itemHandle,
                betRects[(short)itemIndex]);
        }
        int selectedItem = -1;
        for (int itemIndex = 0; itemIndex < betRects.Length; itemIndex = itemIndex + 1)
        {
            if (MacToolbox.PtInRect(mousePoint, betRects[(short)itemIndex]))
            {
                selectedItem = itemIndex;
            }
        }
        if ((short)selectedItem != -1)
        {
            RenderBribeButtons.Run((short)selectedItem);
            while (MacToolbox.StillDown())
            {
                int livePoint = MacToolbox.GetMouse();
                int currentItem = -1;
                if (MacToolbox.PtInRect(livePoint, betRects[0]) && 999 < GameData.Player.Credits)
                {
                    currentItem = 0;
                }
                if (MacToolbox.PtInRect(livePoint, betRects[1]) && 4999 < GameData.Player.Credits)
                {
                    currentItem = 1;
                }
                short prevItem = (short)selectedItem;
                selectedItem = currentItem;
                if ((short)currentItem != prevItem)
                {
                    RenderBribeButtons.Run((short)currentItem);
                }
            }
        }
        return selectedItem;
    }
}
