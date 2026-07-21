using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10078cdc — EV Override-11.c lines 51367-51391.
//
// DEVIATION (faithful): MacToolbox.NGetTrapAddress is currently an
// unconditional-zero stub primitive, so unimplementedTrapAddr and
// trapAddr always compare equal and Run() always returns false. Sole caller
// DrawPictResource's partialTrapAvailable check is already forced true by
// its own FreeMem() stub regardless (see DrawPictResource's header comment),
// so this is currently unobservable, not inert by design.
public static class IsMacTrapAvailable2
{
    // _Unimplemented trap word (0xA89F), sign-extended to 32 bits.
    private const int UnimplementedTrap = -22369;

    public static bool Run(uint trapWord)
    {
        int trapType = GetTrapType.Run(trapWord) ? 1 : 0;
        if (trapType == 1)
        {
            trapWord &= 0x7ff;
            short trapTableSize = NumToolboxTraps.Run();
            if (trapTableSize <= (short)trapWord)
            {
                trapWord = 0xffffa89f; // out of range => _Unimplemented (same bit pattern as UnimplementedTrap)
            }
        }
        int unimplementedTrapAddr = MacToolbox.NGetTrapAddress(UnimplementedTrap, 1);
        int trapAddr = MacToolbox.NGetTrapAddress((int)trapWord, trapType);
        return trapAddr != unimplementedTrapAddr;
    }
}
