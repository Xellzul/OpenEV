using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_1000f498 (EV Override-11.c 8028-8050): draws a dialog button's normal or
// pressed PICT (the DialogScratch.ButtonPictPair) into the button rect.
public static class DrawButtonPressed
{
    public static void Run(short[] rect, bool pressed)
    {
        int[] picts = DialogScratch.ButtonPictPair;
        MacToolbox.DrawPicture(pressed ? picts[1] : picts[0], rect);
    }
}
