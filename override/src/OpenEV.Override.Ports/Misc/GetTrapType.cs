namespace OpenEV.Override.Ports.Misc;

// Decompile: EV Override-11.c lines 51359-51366.
//
// Classic-Mac trap dispatch: bit 0x800 of a trap word selects the Toolbox (1) vs
// OS (0) trap table — the canonical Inside Macintosh GetTrapType routine. Sole
// caller IsMacTrapAvailable2 feeds the result into NGetTrapAddress's trapType arg.
public static class GetTrapType
{
    public static bool Run(uint trapWord)
    {
        return (trapWord & 0x800) != 0;
    }
}
