using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Dialog.Model;

namespace OpenEV.Override.Ports.Text;

// Port of FUN_10013880 (EV Override-11.c 9782-9795): sibling of LoadIndexedSpobString
// that loads an indexed rebellion hail string (STR# 3002) into
// DialogScratch.SpaceportHailText.
public static class LoadIndexedRebellionString
{
    public static void Run(short index)
    {
        DialogScratch.SpaceportHailText =
            MacToolbox.GetIndString(3002, (short)(DialogScratch.SpaceportGreetIndex + index * 5 + 1));
    }
}
