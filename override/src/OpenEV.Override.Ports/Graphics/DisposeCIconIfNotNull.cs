using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_10079c88 (EV Override-11.c lines 51920-51930).
public static class DisposeCIconIfNotNull
{
    public static void Run(int cIconHandle)
    {
        if (cIconHandle != 0)
        {
            MacToolbox.DisposeCIcon(cIconHandle);
        }
        return;
    }
}
