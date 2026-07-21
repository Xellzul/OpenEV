using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.GalaxyMap;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000c06c (EV Override-11.c lines 6256-6315): paints one row of the
// 4-button galaxy-map / dialog strip. activeButton selects the lit button.
// Items 1/4/5/8's Rects are the DrawPicture / PaintRect destinations; buttons
// 2-4 are gated on GalaxyMapState.Plus/MinusEnabled/RouteActive — disabled
// slots are painted black instead of drawing a PICT.
public static class Render4ButtonRow
{
    public static void Run(short activeButton)
    {
        short[] rect1 = new short[4];
        short[] rect4 = new short[4];
        short[] rect5 = new short[4];
        short[] rect8 = new short[4];

        int dialogPtr = GalaxyMapState.MapDialog;
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.GetDialogItem(dialogPtr, 1, 0, 0, rect1);
        MacToolbox.GetDialogItem(dialogPtr, 4, 0, 0, rect4);
        MacToolbox.GetDialogItem(dialogPtr, 5, 0, 0, rect5);
        MacToolbox.GetDialogItem(dialogPtr, 8, 0, 0, rect8);
        if (activeButton == 0)
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[1], rect1);
        }
        else
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[0], rect1);
        }
        if (GalaxyMapState.PlusEnabled == 0)
        {
            MacToolbox.PaintRect(rect4);
        }
        else if (activeButton == 1)
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[3], rect4);
        }
        else
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[2], rect4);
        }
        if (GalaxyMapState.MinusEnabled == 0)
        {
            MacToolbox.PaintRect(rect5);
        }
        else if (activeButton == 2)
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[5], rect5);
        }
        else
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[4], rect5);
        }
        if (GalaxyMapState.RouteActive == 0)
        {
            MacToolbox.PaintRect(rect8);
        }
        else if (activeButton == 3)
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[7], rect8);
        }
        else
        {
            MacToolbox.DrawPicture(GalaxyMapState.ButtonPics[6], rect8);
        }
    }
}
