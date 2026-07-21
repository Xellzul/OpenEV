using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10078c4c — EV Override-11.c lines 51339-51358.
//
// DEVIATION (faithful): MacToolbox.NGetTrapAddress is currently an
// unconditional-zero stub primitive, so addrA/addrB always compare
// equal and this always returns 512. Sole caller IsMacTrapAvailable2 feeds
// that into DrawPictResource's partialTrapAvailable check, but that branch
// is already forced true by the FreeMem() stub regardless (see
// DrawPictResource's own header comment) — currently unobservable, not inert
// by design.
public static class NumToolboxTraps
{
    public static short Run()
    {
        // 0xAA6E/0xA86E are Mac OS toolbox trap words (sign-extended to int as the decompile passes them).
        var addrA = MacToolbox.NGetTrapAddress(0xffffaa6e, 1);
        var addrB = MacToolbox.NGetTrapAddress(0xffffa86e, 1);
        // Equal addresses => the unimplemented trap stub is shared => 512-entry table; else 1024.
        return addrB == addrA ? (short)512 : (short)1024;
    }
}
