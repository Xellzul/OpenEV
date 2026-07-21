using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10078058 (decompile 50917-50936) — dispose a GWorld sub-record {port, gdevice,
// rowTable}: free the row table, tear down the offscreen GWorld, zero the fields.
public static class DisposeGWorldRecord
{
    // The triple lives in GlobalState fields, threaded by ref (was a base+0/+4/+8 cell pointer).
    public static void Run(ref int port, ref int gdevice, ref int rowTable)
    {
        if (port != 0)
        {
            if (rowTable != 0) MacToolbox.DisposePtr(rowTable);
            rowTable = 0;
            DisposeOffscreenGWorld.Run(port, gdevice);
            port = 0;
            // Second dispose is dead (rowTable already 0) — kept faithful to the decompile.
            if (rowTable != 0) MacToolbox.DisposePtr(rowTable);
            rowTable = 0;
        }
    }
}
