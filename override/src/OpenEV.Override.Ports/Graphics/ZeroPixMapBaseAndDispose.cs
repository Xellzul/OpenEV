using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10077f2c (decompile 50872-50890) — zero the GWorld's pixmap baseAddr, then dispose
// the sub-record. The port+2 write targets the separate PORT struct (the toolbox boundary),
// not the render-context record.
public static class ZeroPixMapBaseAndDispose
{
    // The GWorld sub-record {port, gdevice, rowTable} lives in GlobalState fields, threaded by ref.
    public static void Run(ref int port, ref int gdevice, ref int rowTable)
    {
        if (port != 0)
        {
            if (MacGrafPorts.IsHandle(port))
            {
                var p = MacGrafPorts.At(port);
                if (GlobalState.ColorQuickDrawFlag == 0)
                {
                    p.PixMapHandle = 0;   // port+2 = 0 (B&W BitMap baseAddr slot)
                }
                else
                {
                    // Zero the pixmap's baseAddr; the buffer is owned/freed by the
                    // GWorld-record dispose that follows.
                    var pm = MacPixMaps.At(p.PixMapHandle);
                    pm.Pixels = null;
                    pm.LegacyBaseAddr = 0;
                }
            }
            else if (GlobalState.ColorQuickDrawFlag == 0)
            {
                // Dead: ColorQuickDrawFlag is pinned 1 (InitRenderWindow) and never cleared —
                // the raw B&W baseAddr-slot zero can't run.
                throw new System.NotSupportedException(
                    "ZeroPixMapBase: raw B&W path (flag pinning changed?) — re-derive.");
            }
            else
            {
                // Dead: the two call sites (InitRenderWindow teardown + TeardownGlobalGWorlds)
                // pass ComposeScratchPort / SecondaryGWorldPort, which CallGWorldOpOrFatal
                // creates as managed MacGrafPorts, so the IsHandle branch above wins. A raw,
                // non-managed colour port here means a new GWorld kind appeared.
                throw new System.NotSupportedException(
                    "ZeroPixMapBase: raw colour-port path (non-managed GWorld?) — re-derive vs FUN_10077f2c.");
            }
            DisposeGWorldRecord.Run(ref port, ref gdevice, ref rowTable);
        }
    }
}
