using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_10078398 (decompile 51030-51105) — build a Mac GDevice wrapping `pixMapHandle`:
// gdType from the pixmap depth (0 = indexed for <8-bit, 2 = direct), an empty inverse table,
// gdPMap = the pixmap, gdRect from the pixmap bounds, then (for indexed depths) build the
// inverse table. On success returns 0 with the device handle in `gdHandleOut`; else a Mem/QD
// error (disposing what was allocated, gdHandleOut = 0).
//
// The GDevice is a MacGDevices registry object (was NewHandle(0x3e) + raw field writes). The
// inverse table stays a raw Mac handle (opaque; MakeITable is a shim).
public static class NewGDeviceForPixmap
{
    public static int Run(int pixMapHandle, out int gdHandleOut)
    {
        gdHandleOut = 0;
        // The original's NewHandle(0x3e) record alloc can fail with MemError; the registry
        // allocation cannot (and MemError is a 0-stub).
        int iTableHandle = MacToolbox.NewHandleClear(2);
        short errCode = 0;
        MacGDevice? dev = null;
        if (iTableHandle == 0)
        {
            errCode = (short)MacToolbox.MemError();
        }
        else
        {
            var pixMap = MacPixMaps.At(pixMapHandle);         // managed MacPixMap
            short depth = pixMap.PixelSize;

            dev = MacGDevices.New();
            dev.GdType = (short)(depth < 9 ? 0 : 2);          // 0=indexed, 2=direct
            dev.ITableHandle = iTableHandle;
            dev.ResPref = 4;
            dev.PMapHandle = pixMapHandle;
            dev.RectTop = pixMap.BoundsTop;                   // gdRect = pixmap bounds
            dev.RectLeft = pixMap.BoundsLeft;
            dev.RectBottom = pixMap.BoundsBottom;
            dev.RectRight = pixMap.BoundsRight;
            dev.GdMode = -1;

            if (depth > 1)
                MacToolbox.SetDeviceAttribute(dev.Handle, 0, 1);
            MacToolbox.SetDeviceAttribute(dev.Handle, 0xe, 1);

            if (depth < 9)
            {
                MacToolbox.MakeITable(pixMap.ColorTableHandle, dev.ITableHandle, dev.ResPref);
                errCode = (short)MacToolbox.QDError();
            }
        }

        if (errCode == 0 && dev is not null)
        {
            gdHandleOut = dev.Handle;
            return 0;
        }
        if (iTableHandle != 0)
            MacToolbox.DisposeHandle(iTableHandle);
        if (dev is not null)
            MacGDevices.Dispose(dev.Handle);
        return errCode;
    }
}
