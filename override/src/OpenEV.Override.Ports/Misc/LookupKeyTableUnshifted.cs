using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 49600-49610.
public static class LookupKeyTableUnshifted
{
    public static uint Run(uint keyCode)
    {
        if (keyCode == 0xffffffff)
        {
            return 0xffffffff;
        }
        return KeyTranslateTables.Unshifted[keyCode & 0xff];
    }
}
