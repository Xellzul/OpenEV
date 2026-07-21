using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Graphics;

// FUN_100785c8 (decompile 51106-51212) — create one offscreen colour CGrafPort: open the
// port, set its portRect/visRgn/clip to `bounds`, build its PixMap via NewGWorld (cloned
// colour table + depth-sized pixel buffer), then wrap that pixmap in a GDevice
// (NewGDeviceForPixmap). Returns 0 with the new {port, GDevice handle}, or a Mac OSErr after
// disposing whatever was allocated (the out params stay 0; every caller consumes them only
// on success).
//
// Bounds arrive as packed {top,left}/{bottom,right}. The port is a MacGrafPorts registry
// object (was NewPtr(0x6c)+OpenCPort), the GDevice a MacGDevices object, the visRgn a real
// MacRegion BBox.
public static class NewOffscreenColorPort
{
    public static int Run(int boundsTopLeft, int boundsBotRight, int depth, int ctabHandle,
                          out int outPort, out int outGDevice)
    {
        outPort = 0;
        outGDevice = 0;

        MacGrafPort? cPort = null;
        int pixMapHandle = 0;
        int gDeviceHandle = 0;
        byte savedHandleState = 0;
        if (ctabHandle != 0)
        {
            savedHandleState = MacToolbox.HGetState(ctabHandle);
            MacToolbox.HNoPurge(ctabHandle);
        }

        short top = (short)(boundsTopLeft >> 16);
        short left = (short)boundsTopLeft;
        short bottom = (short)(boundsBotRight >> 16);
        short right = (short)boundsBotRight;

        short depthShort = (short)depth;
        // rowBytes = ((depth*width + 0x1f) signed-divided by 32) * 4. The >>5 + addze rounding
        // is the srawi/addze truncate-toward-zero idiom — keep the explicit form (do not
        // collapse to a bare shift or `/`).
        uint rowBits = (uint)(short)(depthShort * (right - left) + 0x1f);
        int rowBytes = (int)((((int)rowBits >> 5) + (uint)(((int)rowBits < 0 && (rowBits & 0x1f) != 0) ? 1 : 0)) * 4);
        MacToolbox.Gestalt(0x71642020, out int qdVersion);   // 'qd  ' — Color QuickDraw version

        // Depth must be 1/2/4/8 (or 16/32 with Color QuickDraw >= 0x200), rowBytes must fit,
        // and indexed depths require a colour table.
        short errCode = 0;
        if (depthShort == 1 || depthShort == 2 || depthShort == 4 || depthShort == 8 ||
            (depthShort == 16 || depthShort == 32) && 0x1ff < qdVersion)
        {
            if ((short)rowBytes < 0x3fff)
            {
                if (depthShort < 9 && ctabHandle == 0)
                    errCode = -50;   // paramErr
            }
            else
            {
                errCode = -50;   // paramErr
            }
        }
        else
        {
            errCode = -50;   // paramErr
        }

        if (errCode == 0)
        {
            int[] savedPort = new int[4];
            MacToolbox.GetPort(savedPort);
            // The original's NewPtr(0x6c)+OpenCPort alloc can fail with MemError; the
            // registry port cannot (and MemError is a 0-stub).
            cPort = MacGrafPorts.NewPort();
            cPort.SetPortRectPacked(boundsTopLeft, boundsBotRight);
            short[] bounds = { top, left, bottom, right };
            MacToolbox.RectRgn(cPort.VisRgn, bounds);
            MacToolbox.ClipRect(bounds);
            errCode = (short)NewGWorld.Run(depthShort, boundsTopLeft, boundsBotRight,
                                           ctabHandle, (ushort)rowBytes,
                                           cPort.PixMapHandle);
            if (errCode == 0)
            {
                // decompile 51173-51174 are self-assignment no-ops (a decompiler artifact), dropped.
                pixMapHandle = cPort.PixMapHandle;
                errCode = (short)NewGDeviceForPixmap.Run(pixMapHandle, out gDeviceHandle);
            }
            MacToolbox.SetPort(savedPort[0]);
        }

        if (ctabHandle != 0)
            MacToolbox.HSetState(ctabHandle, savedHandleState);

        if (errCode == 0)
        {
            outPort = cPort!.Handle;
            outGDevice = gDeviceHandle;
        }
        else
        {
            if (pixMapHandle != 0)
            {
                // Dispose the cloned colour table and drop the buffer (the original's
                // alignment-unwind + DisposePtr dance).
                var pixMap = MacPixMaps.At(pixMapHandle);
                MacToolbox.DisposeCTable(pixMap.ColorTableHandle);
                pixMap.Pixels = null;
            }
            if (gDeviceHandle != 0)
            {
                MacToolbox.DisposeHandle(MacToolbox.GetDeviceITable(gDeviceHandle));
                MacGDevices.Dispose(gDeviceHandle);
            }
            if (cPort is not null)
                MacGrafPorts.Dispose(cPort.Handle);   // CloseCPort + DisposePtr
        }
        return errCode;
    }
}
