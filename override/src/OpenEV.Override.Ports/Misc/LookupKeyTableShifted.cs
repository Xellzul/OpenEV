using OpenEV.Override.Ports.Misc.Model;

namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 49611-49621.
public static class LookupKeyTableShifted
{
    public static uint Run(uint keyCode)
    {
        if (keyCode == 0xffffffff)
        {
            return 0xffffffff;
        }
        return KeyTranslateTables.Shifted[keyCode & 0xff];
    }
}
