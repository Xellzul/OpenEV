using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10078944 (decompile 51213-51255) — tear down a {port, GDevice} pair made by
// NewOffscreenColorPort: deselect it if active, dispose the GDevice, free the pixmap's pixel
// buffer (unwinding the depth-alignment nudge) and colour table, then close + free the port.
public static class DisposeOffscreenGWorld
{
    public static void Run(int cPort, int gDevice)
    {
        int[] savedPort = new int[3];
        MacToolbox.GetPort(savedPort);
        if (savedPort[0] == cPort)
        {
            MacToolbox.GetCWMgrPort(out savedPort);
            MacToolbox.SetPort(savedPort[0]);
        }
        if (MacToolbox.GetGDevice() == gDevice)
        {
            MacToolbox.GetMainDevice();
            MacToolbox.SetGDevice();
        }
        MacToolbox.ClearDevicePixMap(gDevice);   // *(*gDevice + 0x16) = 0 (gdPMap)
        if (MacGDevices.IsHandle(gDevice))
            MacGDevices.Dispose(gDevice);        // managed device (NewGDeviceForPixmap)
        else
            MacToolbox.DisposeGDevice(gDevice);  // raw legacy handle (shim)

        // The original unwinds the depth-alignment baseAddr nudge then DisposePtr's the
        // buffer — with managed Pixels that collapses to dropping the reference.
        var pixMap = MacPixMaps.At(MacToolbox.GetPortPixMap(cPort));
        pixMap.Pixels = null;
        if (pixMap.ColorTableHandle != 0)
            MacToolbox.DisposeCTable(pixMap.ColorTableHandle);
        if (MacGrafPorts.IsHandle(cPort))
        {
            MacGrafPorts.Dispose(cPort);         // CloseCPort + DisposePtr equivalent
        }
        else
        {
            MacToolbox.CloseCPort(cPort);
            MacToolbox.DisposePtr(cPort);
        }
    }
}
