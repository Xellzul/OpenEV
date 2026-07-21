using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_10013800 (EV Override-11.c 9766-9781): loads an indexed spaceport
// hail string (STR# 3000 for index < 38, else STR# 3001) into
// DialogScratch.SpaceportHailText.
public static class LoadIndexedSpobString
{
    public static void Run(int index)
    {
        if ((short)index < 38)
        {
            DialogScratch.SpaceportHailText =
                MacToolbox.GetIndString(3000, (short)(DialogScratch.SpaceportGreetIndex + index * 5 + 1));
        }
        else
        {
            DialogScratch.SpaceportHailText =
                MacToolbox.GetIndString(3001, (short)(DialogScratch.SpaceportGreetIndex + index * 5 - 189));
        }
    }
}
