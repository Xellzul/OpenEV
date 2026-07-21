using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007e73c (EV Override-11.c lines 54838-54881).
//
// Fills a GWorld slot record with a fresh 1-bit B&W offscreen GrafPort over
// `bounds`: opens the port, sets its rect/visRgn/clip, computes the 1-bit rowBytes,
// allocates the pixel buffer, erases it, and caches {bounds, basePtr, rowBytes}
// back into the record.
//
// The port is a managed MacGrafPort (was a raw NewPtr(0x6c) BitMap GrafPort) and is
// otherwise vestigial: this is the B&W MASK staging GWorld, and the host CopyMask
// path ignores Mac mask bitmaps (alpha comes from the colour PICT), so PixBase flows
// only into the ignored SpriteFrame.MaskBase. Its one live use is the host discard
// key Port+2 (LoadIconPairForSlot registers it; draws there are dropped, never
// sampled) — a managed Handle+2 is an equally-unique key, so nothing changes.
public static class AllocateGWorldPortStruct
{
    public static void Run(SlotGWorldRecord gworldRec, int boundsTopLeft, int boundsBotRight)
    {
        int[] savedPort = new int[5];
        MacToolbox.GetPort(savedPort);

        gworldRec.GDevice = 0;
        var port = MacGrafPorts.NewPort();
        gworldRec.Port = port.Handle;
        AssertAllocSucceeded.Run(port.Handle);   // no-op in the port (managed NewPort never returns 0)
        MacToolbox.OpenPort(port.Handle);

        port.SetPortRectPacked(boundsTopLeft, boundsBotRight);
        short[] portRect = port.PortRectShorts();
        MacToolbox.RectRgn(port.VisRgn, portRect);
        MacToolbox.ClipRect(portRect);

        // 1-bit rowBytes = ceil(width / 32) * 4. Packed bounds: low 16 = left/right,
        // high 16 = top/bottom. The `? 1 : 0` term is the srawi+addze truncating-÷32
        // correction — do not collapse it to a bare `>> 5`.
        short width = (short)((short)boundsBotRight - (short)boundsTopLeft);
        short height = (short)((short)(boundsBotRight >> 16) - (short)(boundsTopLeft >> 16));
        int paddedWidth = (short)(width + 0x1f);
        short rowBytes = (short)(((paddedWidth >> 5) + (paddedWidth < 0 && (paddedWidth & 0x1f) != 0 ? 1 : 0)) * 4);

        // Real heap block (Mac dispose symmetry); its bytes are never read.
        int basePtr = MacToolbox.NewPtr(height * rowBytes);
        AssertAllocSucceeded.Run(basePtr);
        MacToolbox.SetPort(port.Handle);
        MacToolbox.EraseRect(portRect);

        // Cache bounds/base/rowBytes into the record (the Mac read these straight
        // back off the port's BitMap; same values).
        gworldRec.BoundsTopLeftPacked = boundsTopLeft;
        gworldRec.BoundsBotRightPacked = boundsBotRight;
        gworldRec.PixBase = basePtr;
        gworldRec.RowBytes = rowBytes;
        MacToolbox.SetPort(savedPort[0]);
    }
}
